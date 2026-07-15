using System.Text.Json;
using EgyptOnline.Data;
using EgyptOnline.Dtos.Contract;
using EgyptOnline.Domain.Models;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using ContractModel = EgyptOnline.Models.Contract;
using ContractDayModel = EgyptOnline.Domain.Models.ContractDay;
using EgyptOnline.Application.Services.Complaint;

namespace EgyptOnline.Application.Services.Contract
{
    public class ContractService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly Wallet.WalletService _walletService;
        private readonly Complaint.ComplaintService _complaintService;
        private readonly ILogger<ContractService> _logger;

        public ContractService(ApplicationDbContext context, INotificationService notificationService, Wallet.WalletService walletService, Complaint.ComplaintService complaintService, ILogger<ContractService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _walletService = walletService;
            _complaintService = complaintService;
            _logger = logger;
        }

        #region 2-Party Contract System (New Simplified Logic)
        private string NormalizeEgyptianPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return phoneNumber;

            // Remove spaces and dashes
            var cleaned = phoneNumber.Replace(" ", "").Replace("-", "");

            // If it doesn't already start with +20, add it
            if (!cleaned.StartsWith("+20"))
            {
                cleaned = "+2" + cleaned;
            }

            return cleaned;
        }
        public async Task<ContractModel> CreateContractAsync(ContractModel contract)
        {
            var phoneNumberNormalized = NormalizeEgyptianPhoneNumber(contract.ServiceProviderPhoneNumber);
            contract.ServiceProviderPhoneNumber = phoneNumberNormalized;
            var clientUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == contract.ClientUserId);
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

            if (clientUser == null)
                throw new InvalidOperationException($"مستخدم العميل غير موجود: {contract.ClientUserId}");
            if (providerUser == null)
                throw new InvalidOperationException($"مقدم الخدمة غير موجود برقم الهاتف: {contract.ServiceProviderPhoneNumber}");
            if (clientUser.Id == providerUser.Id)
            {
                throw new InvalidOperationException("لا يمكن إنشاء عقد مع نفسك كمقدم خدمة.");
            }
            var totalRequired = contract.TotalAmount + contract.PenaltyAmount;
            var hasSufficientBalance = await _walletService.HasSufficientFreeBalanceAsync(contract.ClientUserId, totalRequired);
            if (!hasSufficientBalance)
                throw new InvalidOperationException($"رصيد العميل المتاح غير كافٍ. المبلغ المطلوب: {totalRequired}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Freeze client's total (daily salary + client's penalty)
                await _walletService.TransferFreeToFrozenAsync(contract.ClientUserId, contract.TotalAmount + contract.PenaltyAmount);

                contract.Status = "pending";
                contract.CreatedAt = DateTime.UtcNow;

                _context.Contracts.Add(contract);
                await _context.SaveChangesAsync();

                var contractDays = new List<ContractDayModel>();
                for (int day = 1; day <= contract.TotalDays; day++)
                {
                    var contractDay = new ContractDayModel
                    {
                        ContractId = contract.Id,
                        DayNumber = day,
                        Date = DateTime.SpecifyKind(contract.StartDate.AddDays(day - 1), DateTimeKind.Utc),
                        ProviderArrived = false,
                        Status = ContractDayStatus.Pending,
                        IsProcessed = false
                    };
                    contractDays.Add(contractDay);
                }

                _context.ContractDays.AddRange(contractDays);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Created 2-party contract {ContractId} for client {ClientId} and provider phone {ProviderPhone}. Total: {TotalAmount}",
                    contract.Id, contract.ClientUserId, contract.ServiceProviderPhoneNumber, contract.TotalAmount);

                // Send notification to service provider
                await _notificationService.SendNotificationToUser(
                    providerUser.Id,
                    "عقد جديد بانتظار توقيعك",
                    $"تم إنشاء عقد جديد #{contract.Id} بقيمة {contract.TotalAmount} جنيه. يرجى مراجعة التفاصيل والتوقيع",
                    "contract",
                    contract.ClientUserId
                );

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ContractModel> ProviderRejectContractAsync(int contractId, string providerUserId)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"العقد غير موجود: {contractId}");

            // Verify the caller is the service provider by phone number
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == providerUserId);
            if (providerUser == null || providerUser.PhoneNumber != contract.ServiceProviderPhoneNumber)
                throw new UnauthorizedAccessException("ليس لديك صلاحية لرفض هذا العقد");

            if (contract.Status != "pending")
                throw new InvalidOperationException($"العقد ليس في حالة معلق. الحالة الحالية: {contract.Status}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Return client's frozen funds (daily salary + client's penalty)
                await _walletService.TransferFrozenToFreeAsync(contract.ClientUserId, contract.TotalAmount + contract.PenaltyAmount);

                contract.Status = "cancelled";
                contract.CancelledAt = DateTime.UtcNow;
                contract.CancelledBy = contract.ServiceProviderPhoneNumber;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Provider rejected contract {ContractId}. Funds restored to client {ClientId}", contractId, contract.ClientUserId);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ContractModel> ProviderAcceptContractAsync(int contractId, string providerUserId)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"العقد غير موجود: {contractId}");

            // Verify the caller is the service provider by phone number
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == providerUserId);
            if (providerUser == null || providerUser.PhoneNumber != contract.ServiceProviderPhoneNumber)
                throw new UnauthorizedAccessException("ليس لديك صلاحية لقبول هذا العقد");

            if (contract.Status != "pending")
                throw new InvalidOperationException($"العقد ليس في حالة معلق. الحالة الحالية: {contract.Status}");

            var hasSufficientBalance = await _walletService.HasSufficientFreeBalanceAsync(providerUserId, contract.PenaltyAmount);
            if (!hasSufficientBalance)
                throw new InvalidOperationException($"الرصيد المتاح غير كافي للشرط الجزائي. المبلغ المطلوب: {contract.PenaltyAmount}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _walletService.TransferFreeToFrozenAsync(providerUserId, contract.PenaltyAmount);

                contract.Status = "active";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Provider accepted contract {ContractId}. Status changed to Active, Worker Penalty frozen", contractId);

                // Send notification to client
                await _notificationService.SendNotificationToUser(
                    contract.ClientUserId,
                    "تم قبول العقد",
                    $"تم قبول العقد #{contract.Id} من قبل مقدم الخدمة",
                    "contract",
                    providerUserId
                );

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ContractDayModel> RegisterArrivalAsync(int contractId, int dayNumber, string providerUserId)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"العقد غير موجود: {contractId}");

            // Verify the caller is the service provider by phone number
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == providerUserId);
            if (providerUser == null || providerUser.PhoneNumber != contract.ServiceProviderPhoneNumber)
                throw new UnauthorizedAccessException("ليس لديك صلاحية لتسجيل الوصول لهذا العقد");

            if (contract.Status != "active")
                throw new InvalidOperationException($"العقد ليس في حالة نشط. الحالة الحالية: {contract.Status}");

            var contractDay = contract.ContractDays.FirstOrDefault(cd => cd.DayNumber == dayNumber);
            if (contractDay == null)
                throw new InvalidOperationException($"يوم العقد رقم {dayNumber} غير موجود للعقد {contractId}");

            if (contractDay.ProviderArrived)
                throw new InvalidOperationException($"مقدم الخدمة قد سجل وصوله بالفعل لليوم {dayNumber}");

            // Validate arrival is within the shift time span (with 30-minute grace period)
            var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            var currentTimeUtc = DateTime.UtcNow;
            var currentTimeEgypt = TimeZoneInfo.ConvertTimeFromUtc(currentTimeUtc, egyptTimeZone);

            // Ensure contract day date is valid and has correct DateTimeKind
            if (contractDay.Date == DateTime.MinValue)
                throw new InvalidOperationException($"تاريخ يوم العقد غير صالح لليوم {dayNumber}");

            // Convert contract day date (UTC) to Egypt local time for shift calculations
            var contractDayEgyptLocal = TimeZoneInfo.ConvertTimeFromUtc(contractDay.Date, egyptTimeZone);

            // Validate shift start/end times are valid
            if (contract.ShiftStartTime < TimeSpan.Zero || contract.ShiftStartTime >= TimeSpan.FromDays(1))
                throw new InvalidOperationException($"وقت بدء الوردية غير صالح: {contract.ShiftStartTime}");

            // Shift times are stored as TimeSpan representing Egypt local time
            var shiftStart = contractDayEgyptLocal.Date.Add(contract.ShiftStartTime);
            var gracePeriod = TimeSpan.FromMinutes(30);

            // Provider can confirm arrival until midnight Egypt time of the contract day
            var midnightEgypt = contractDayEgyptLocal.Date.AddDays(1).AddTicks(-1);

            if (currentTimeEgypt < shiftStart.Subtract(gracePeriod))
                throw new InvalidOperationException($"لا يمكن التسجيل قبل بدء الوردية. تبدأ الوردية عند {shiftStart:HH:mm} (مع فترة سماح 30 دقيقة)");

            if (currentTimeEgypt > midnightEgypt)
                throw new InvalidOperationException($"لا يمكن التسجيل بعد منتصف ليل يوم العقد. يوم العقد: {contractDayEgyptLocal:yyyy-MM-dd}");

            contractDay.ProviderArrived = true;
            contractDay.ArrivalTime = DateTime.UtcNow;
            contractDay.Status = ContractDayStatus.Pending;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Provider arrived for contract {ContractId}, day {DayNumber}", contractId, dayNumber);

            // Send notification to client
            await _notificationService.SendNotificationToUser(
                contract.ClientUserId,
                "وصول مقدم الخدمة",
                $"وصل مقدم الخدمة لموقع العمل - يوم {dayNumber} من العقد #{contractId}",
                "contract",
                providerUserId
            );

            return contractDay;
        }

        public async Task<ContractDayModel> ClientConfirmAttendanceAsync(int contractId, int dayNumber, string clientUserId)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.ClientUserId != clientUserId)
                throw new UnauthorizedAccessException("أنت لست طرفاً مصرحاً له بتأكيد الحضور لهذا العقد");

            if (contract.Status != "active")
                throw new InvalidOperationException($"العقد ليس نشطاً. الحالة الحالية: {contract.Status}");

            var contractDay = contract.ContractDays.FirstOrDefault(cd => cd.DayNumber == dayNumber)
                ?? throw new KeyNotFoundException($"يوم العقد رقم {dayNumber} غير موجود");

            if (!contractDay.ProviderArrived)
                throw new InvalidOperationException("لا يمكن تأكيد الحضور قبل أن يسجل مقدم الخدمة وصوله أولاً");

            if (contractDay.ClientConfirmed)
                throw new InvalidOperationException("لقد قمت بالفعل بتأكيد الحضور لهذا اليوم");

            if (contractDay.Status == ContractDayStatus.AbsentDisputed)
                throw new InvalidOperationException("هذا اليوم معلق بنزاع أو إبلاغ غياب");

            contractDay.ClientConfirmed = true;
            contractDay.ClientConfirmedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Client confirmed attendance for contract {ContractId}, day {DayNumber}", contractId, dayNumber);
            return contractDay;
        }

        public async Task<ContractModel> ReportDisputeAsync(int contractId, int dayNumber, string reason, string reporterUserId)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"العقد غير موجود: {contractId}");

            if (contract.Status != "active")
                throw new InvalidOperationException($"العقد ليس في حالة نشط. الحالة الحالية: {contract.Status}");

            // Verify the reporter is a party to this contract
            var reporterUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == reporterUserId)
                ?? throw new InvalidOperationException("المستخدم غير موجود");

            bool isClient = contract.ClientUserId == reporterUserId;
            bool isProvider = reporterUser.PhoneNumber == contract.ServiceProviderPhoneNumber;

            if (!isClient && !isProvider)
                throw new UnauthorizedAccessException("أنت لست طرفاً في هذا العقد");

            var contractDay = contract.ContractDays.FirstOrDefault(cd => cd.DayNumber == dayNumber);
            if (contractDay == null)
                throw new InvalidOperationException($"يوم العقد رقم {dayNumber} غير موجود");

            // Restrict dispute reporting to current day or previous days only (not future days)
            var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            var contractDayDateInEgypt = TimeZoneInfo.ConvertTimeFromUtc(contractDay.Date, egyptTimeZone).Date;
            var todayInEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptTimeZone).Date;

            if (contractDayDateInEgypt > todayInEgypt)
                throw new InvalidOperationException($"لا يمكن الإبلاغ عن نزاع في الأيام القادمة. يوم العقد: {contractDayDateInEgypt:yyyy-MM-dd}, اليوم: {todayInEgypt:yyyy-MM-dd}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                contract.Status = "suspended";

                contractDay.Status = ContractDayStatus.AbsentDisputed;
                contractDay.DisputeReportedAt = DateTime.UtcNow;
                contractDay.DisputeReason = reason;

                await _context.SaveChangesAsync();

                // Create complaint for admin review with appropriate reason based on reporter
                string complaintReason = isClient ? "غياب مقدم الخدمة" : "شكوى من مقدم الخدمة";
                string complaintDescription = isClient
                    ? $"تم الإبلاغ عن غياب مقدم الخدمة في يوم {dayNumber}. السبب: {reason}"
                    : $"قدم مقدم الخدمة شكوى في يوم {dayNumber}. السبب: {reason}";

                await _complaintService.FileComplaintAsync(
                    reporterUserId,
                    contractId,
                    complaintReason,
                    complaintDescription,
                    "daily_dispute"
                );

                // Notify admins about the new complaint
                await _notificationService.SendNotificationToAdmins(
                    $"شكوى جديدة - {complaintReason}",
                    $"تم الإبلاغ عن مشكلة في العقد #{contractId}، يوم {dayNumber}. السبب: {reason}"
                );

                // Notify both parties about the dispute
                var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);
                if (providerUser != null)
                {
                    await _notificationService.SendNotificationToUser(
                        providerUser.Id,
                        "تم رفع نزاع على العقد",
                        $"تم رفع نزاع على العقد #{contractId}، يوم {dayNumber}. السبب: {reason}",
                        "contract"
                    );
                }

                await _notificationService.SendNotificationToUser(
                    contract.ClientUserId,
                    "تم رفع نزاع على العقد",
                    $"تم رفع نزاع على العقد #{contractId}، يوم {dayNumber}. السبب: {reason}",
                    "contract"
                );

                await transaction.CommitAsync();

                _logger.LogInformation("Dispute reported for contract {ContractId}, day {DayNumber} by {Reporter}. Reason: {Reason}",
                    contractId, dayNumber, isClient ? "client" : "provider", reason);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /*
        public async Task<ContractModel> MutualTerminationAsync(int contractId, string reason)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"العقد غير موجود: {contractId}");

            if (contract.Status != "active" && contract.Status != "suspended")
                throw new InvalidOperationException($"Contract must be Active or Suspended for mutual termination. Current status: {contract.Status}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Keep all funds frozen - no auto-distribution
                // Admin will manually resolve via balance override

                contract.Status = "terminated";
                contract.TerminatedAt = DateTime.UtcNow;
                contract.TerminatedBy = "Mutual";
                contract.CancelledBy = "Mutual";
                contract.CancelledAt = DateTime.UtcNow;
                contract.TerminationReason = reason;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Mutual termination for contract {ContractId}. Reason: {Reason}. Funds remain frozen for admin resolution",
                    contractId, reason);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        */

        /*
        public async Task<ContractModel> ClientUnilateralTerminationAsync(int contractId, string reason)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"العقد غير موجود: {contractId}");

            if (contract.Status != "active" && contract.Status != "suspended")
                throw new InvalidOperationException($"Contract must be Active or Suspended for unilateral termination. Current status: {contract.Status}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Keep all funds frozen - no auto-distribution
                // Admin will manually resolve via balance override

                contract.Status = "terminated";
                contract.TerminatedAt = DateTime.UtcNow;
                contract.TerminatedBy = contract.ClientUserId;
                contract.CancelledBy = contract.ClientUserId;
                contract.CancelledAt = DateTime.UtcNow;
                contract.TerminationReason = reason;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Client unilateral termination for contract {ContractId}. Reason: {Reason}. Funds remain frozen for admin resolution",
                    contractId, reason);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        */

        /*
        public async Task<ContractModel> ProviderUnilateralTerminationAsync(int contractId, string providerUserId, string reason)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"العقد غير موجود: {contractId}");

            // Verify the caller is the service provider by phone number
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == providerUserId);
            if (providerUser == null || providerUser.PhoneNumber != contract.ServiceProviderPhoneNumber)
                throw new UnauthorizedAccessException("You are not authorized to terminate this contract");

            if (contract.Status != "active" && contract.Status != "suspended")
                throw new InvalidOperationException($"Contract must be Active or Suspended for unilateral termination. Current status: {contract.Status}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Keep all funds frozen - no auto-distribution
                // Admin will manually resolve via balance override

                contract.Status = "terminated";
                contract.TerminatedAt = DateTime.UtcNow;
                contract.TerminatedBy = contract.ServiceProviderPhoneNumber;
                contract.CancelledBy = contract.ServiceProviderPhoneNumber;
                contract.CancelledAt = DateTime.UtcNow;
                contract.TerminationReason = reason;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Provider unilateral termination for contract {ContractId}. Reason: {Reason}. Funds remain frozen for admin resolution",
                    contractId, reason);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        */

        public async Task<object?> GetContractByIdAsync(int contractId, string userId, bool includeDays = false)
        {
            IQueryable<ContractModel> query = _context.Contracts
                .Include(c => c.ClientUser);

            if (includeDays)
            {
                query = query.Include(c => c.ContractDays);
            }

            var contract = await query.FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                return null;

            // Only parties to the contract may view its details
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            bool isParty = contract.ClientUserId == userId || (currentUser != null && currentUser.PhoneNumber == contract.ServiceProviderPhoneNumber);
            if (!isParty)
                throw new UnauthorizedAccessException("ليس لديك صلاحية لعرض هذا العقد");

            // Load provider user by phone number
            var providerUser = await _context.Users
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

            var result = new
            {
                contract.Id,
                contract.ClientUserId,
                contract.ServiceProviderPhoneNumber,
                contract.StartDate,
                contract.ShiftStartTime,
                contract.ShiftEndTime,
                contract.TotalDays,
                contract.DailySalary,
                contract.TotalAmount,
                contract.PenaltyAmount,
                contract.Status,
                contract.Governorate,
                contract.City,
                contract.District,
                contract.DetailedAddress,
                contract.Notes,
                contract.RestrictedTerms,
                client = new
                {
                    id = contract.ClientUser.Id,
                    firstName = contract.ClientUser.FirstName,
                    lastName = contract.ClientUser.LastName,
                    phoneNumber = contract.ClientUser.PhoneNumber
                },
                provider = new
                {
                    id = providerUser?.Id,
                    firstName = providerUser?.FirstName,
                    lastName = providerUser?.LastName,
                    phoneNumber = providerUser?.PhoneNumber,
                    specialization = providerUser?.ServiceProvider?.GetSpecialization()
                }
            };

            if (includeDays)
            {
                var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
                var contractDaysWithEgyptTime = contract.ContractDays
                    .OrderBy(cd => cd.DayNumber)
                    .Select(cd => new
                    {
                        cd.Id,
                        cd.ContractId,
                        cd.DayNumber,
                        Date = TimeZoneInfo.ConvertTimeFromUtc(cd.Date, egyptTimeZone),
                        cd.ProviderArrived,
                        cd.ClientConfirmed,
                        ClientConfirmedAt = cd.ClientConfirmedAt.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(cd.ClientConfirmedAt.Value, egyptTimeZone) : (DateTime?)null,
                        cd.Status,
                        cd.IsProcessed,
                        ProcessedAt = cd.ProcessedAt.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(cd.ProcessedAt.Value, egyptTimeZone) : (DateTime?)null,
                        ArrivalTime = cd.ArrivalTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(cd.ArrivalTime.Value, egyptTimeZone) : (DateTime?)null,
                        DisputeReportedAt = cd.DisputeReportedAt.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(cd.DisputeReportedAt.Value, egyptTimeZone) : (DateTime?)null,
                        cd.DisputeReason
                    }).ToList();

                return new
                {
                    result.Id,
                    result.ClientUserId,
                    result.ServiceProviderPhoneNumber,
                    result.StartDate,
                    result.ShiftStartTime,
                    result.ShiftEndTime,
                    result.TotalDays,
                    result.DailySalary,
                    result.TotalAmount,
                    result.PenaltyAmount,
                    result.Status,
                    result.Governorate,
                    result.City,
                    result.District,
                    result.DetailedAddress,
                    result.Notes,
                    result.RestrictedTerms,
                    ContractDays = contractDaysWithEgyptTime,
                    result.client,
                    result.provider
                };
            }

            return result;
        }

        public async Task<List<object>> GetContractsByUserIdAsync(string userId, string? status = null, int pageNumber = 1, int pageSize = 20, bool includeDays = false)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            // Get the user's phone number to match against ServiceProviderPhoneNumber
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return new List<object>();

            IQueryable<ContractModel> query = _context.Contracts
                .Include(c => c.ClientUser)
                .Where(c => c.ServiceProviderPhoneNumber == user.PhoneNumber || c.ClientUserId == userId);

            if (includeDays)
            {
                query = query.Include(c => c.ContractDays);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            var contracts = await query
                .OrderBy(c => c.Status == "active" ? 0 : c.Status == "pending" ? 1 : c.Status == "suspended" ? 2 : 3)
                .ThenByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new List<object>();
            foreach (var contract in contracts)
            {
                var providerUser = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

                var contractResult = new
                {
                    contract.Id,
                    contract.ClientUserId,
                    contract.ServiceProviderPhoneNumber,
                    contract.StartDate,
                    contract.ShiftStartTime,
                    contract.ShiftEndTime,
                    contract.TotalDays,
                    contract.DailySalary,
                    contract.TotalAmount,
                    contract.PenaltyAmount,
                    contract.Status,
                    contract.Governorate,
                    contract.City,
                    contract.District,
                    contract.DetailedAddress,
                    contract.Notes,
                    contract.RestrictedTerms,
                    client = new
                    {
                        id = contract.ClientUser.Id,
                        firstName = contract.ClientUser.FirstName,
                        lastName = contract.ClientUser.LastName,
                        phoneNumber = contract.ClientUser.PhoneNumber
                    },
                    provider = new
                    {
                        id = providerUser?.Id,
                        firstName = providerUser?.FirstName,
                        lastName = providerUser?.LastName,
                        phoneNumber = providerUser?.PhoneNumber,
                        specialization = providerUser?.ServiceProvider?.GetSpecialization()
                    }
                };

                if (includeDays)
                {
                    var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
                    var contractDaysWithEgyptTime = contract.ContractDays
                        .OrderBy(cd => cd.DayNumber)
                        .Select(cd => new
                        {
                            cd.Id,
                            cd.ContractId,
                            cd.DayNumber,
                            Date = TimeZoneInfo.ConvertTimeFromUtc(cd.Date, egyptTimeZone),
                            cd.ProviderArrived,
                            cd.ClientConfirmed,
                            ClientConfirmedAt = cd.ClientConfirmedAt.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(cd.ClientConfirmedAt.Value, egyptTimeZone) : (DateTime?)null,
                            cd.Status,
                            cd.IsProcessed,
                            ProcessedAt = cd.ProcessedAt.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(cd.ProcessedAt.Value, egyptTimeZone) : (DateTime?)null,
                            ArrivalTime = cd.ArrivalTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(cd.ArrivalTime.Value, egyptTimeZone) : (DateTime?)null,
                            DisputeReportedAt = cd.DisputeReportedAt.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(cd.DisputeReportedAt.Value, egyptTimeZone) : (DateTime?)null,
                            cd.DisputeReason
                        }).ToList();

                    result.Add(new
                    {
                        contractResult.Id,
                        contractResult.ClientUserId,
                        contractResult.ServiceProviderPhoneNumber,
                        contractResult.StartDate,
                        contractResult.ShiftStartTime,
                        contractResult.ShiftEndTime,
                        contractResult.TotalDays,
                        contractResult.DailySalary,
                        contractResult.TotalAmount,
                        contractResult.PenaltyAmount,
                        contractResult.Status,
                        contractResult.Governorate,
                        contractResult.City,
                        contractResult.District,
                        contractResult.DetailedAddress,
                        contractResult.Notes,
                        contractResult.RestrictedTerms,
                        ContractDays = contractDaysWithEgyptTime,
                        contractResult.client,
                        contractResult.provider
                    });
                }
                else
                {
                    result.Add(contractResult);
                }
            }

            return result;
        }

        public async Task<List<ContractModel>> GetActiveContractsForAutoPayoutAsync(DateTime currentTime)
        {
            return await _context.Contracts
                .Include(c => c.ContractDays)
                .Where(c => c.Status == "active")
                .ToListAsync();
        }

        public async Task<ContractModel> AdminAdjustAndResumeAsync(
            int contractId,
            int daysWorked,
            string direction, // "client_to_free" or "client_to_worker"
            DateTime? newStartDate,
            string adminUserId,
            string comment)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber)
                ?? throw new InvalidOperationException("مقدم الخدمة غير موجود");

            // Count only the days within the completed range that have not been paid yet
            var unpaidDaysCount = contract.ContractDays
                .Count(cd => cd.DayNumber <= daysWorked && !cd.IsProcessed);
            
            var adjustmentAmount = unpaidDaysCount * contract.DailySalary;


            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (adjustmentAmount > 0)
                {
                    if (direction == "client_to_free")
                    {
                        // Disputed days ruled against provider → refund client, days are closed
                        await _walletService.TransferFrozenToFreeAsync(contract.ClientUserId, adjustmentAmount);
                    }
                    else if (direction == "client_to_worker")
                    {
                        // Days confirmed as worked → transfer to provider
                        await _walletService.SubtractFromFrozenBalanceAsync(contract.ClientUserId, adjustmentAmount);
                        await _walletService.AddToFreeBalanceAsync(providerUser.Id, adjustmentAmount);
                    }

                    // ── CRITICAL: stamp every settled day as processed so the
                    // AutoPayoutBackgroundService never pays them a second time. ──
                    var settledDays = contract.ContractDays
                        .Where(cd => cd.DayNumber <= daysWorked && !cd.IsProcessed)
                        .ToList();

                    foreach (var day in settledDays)
                    {
                        day.IsProcessed   = true;
                        day.ProcessedAt   = DateTime.UtcNow;
                        day.Status        = ContractDayStatus.Completed;
                    }
                }


                // Shift remaining days if newStartDate is provided
                if (newStartDate.HasValue && newStartDate.Value != default(DateTime))
                {
                    var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
                    DateTime startDateUtc;
                    
                    if (newStartDate.Value.Kind == DateTimeKind.Utc)
                    {
                        startDateUtc = newStartDate.Value;
                    }
                    else if (newStartDate.Value.Kind == DateTimeKind.Local)
                    {
                        startDateUtc = TimeZoneInfo.ConvertTimeToUtc(newStartDate.Value);
                    }
                    else
                    {
                        startDateUtc = TimeZoneInfo.ConvertTimeToUtc(newStartDate.Value, egyptTimeZone);
                    }

                    // Validate newStartDate is not in the past
                    if (startDateUtc < DateTime.UtcNow.Date)
                    {
                        throw new InvalidOperationException("تاريخ البدء الجديد يجب أن يكون في المستقبل أو اليوم الحالي على الأقل");
                    }

                    // Validate daysWorked doesn't exceed total days
                    if (daysWorked >= contract.TotalDays)
                    {
                        throw new InvalidOperationException($"عدد الأيام المدفوع ({daysWorked}) يجب أن يكون أقل من إجمالي أيام العقد ({contract.TotalDays})");
                    }

                    // Validate newStartDate is not before the last worked day
                    var lastWorkedDay = contract.ContractDays.FirstOrDefault(cd => cd.DayNumber == daysWorked);
                    if (lastWorkedDay != null && startDateUtc < lastWorkedDay.Date.Date)
                    {
                        throw new InvalidOperationException($"تاريخ البدء الجديد يجب أن يكون بعد أو مساوٍ لتاريخ آخر يوم عمل ({lastWorkedDay.Date:yyyy-MM-dd})");
                    }

                    // Update contract start date
                    contract.StartDate = startDateUtc;

                    // Shift remaining days (daysWorked + 1 to totalDays)
                    var remainingDays = contract.ContractDays.Where(cd => cd.DayNumber > daysWorked).ToList();
                    for (int i = 0; i < remainingDays.Count; i++)
                    {
                        var day = remainingDays[i];
                        var newDayDate = startDateUtc.AddDays(i);
                        day.Date = DateTime.SpecifyKind(newDayDate, DateTimeKind.Utc);
                    }
                }

                // Resume the contract
                contract.Status = "active";

                // Resolve related open complaints
                var complaints = await _context.Complaints
                    .Where(c => c.ContractId == contractId && c.Status == "open")
                    .ToListAsync();
                foreach (var comp in complaints)
                {
                    comp.Status = "resolved";
                    comp.ResolvedByAdminId = adminUserId;
                    comp.AdminNote = comment;
                    comp.ResolvedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Admin {AdminId} resolved contract {ContractId} via Adjust & Resume for {DaysWorked} days", adminUserId, contractId, daysWorked);
                return contract;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to execute AdminAdjustAndResume for contract {ContractId}", contractId);
                throw;
            }
        }

        #endregion
    }
}
