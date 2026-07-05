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

        public async Task<ContractModel> CreateContractAsync(ContractModel contract)
        {
            var clientUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == contract.ClientUserId);
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

            if (clientUser == null)
                throw new InvalidOperationException($"مستخدم العميل غير موجود: {contract.ClientUserId}");
            if (providerUser == null)
                throw new InvalidOperationException($"مقدم الخدمة غير موجود برقم الهاتف: {contract.ServiceProviderPhoneNumber}");

            var totalRequired = contract.TotalAmount + contract.PenaltyAmount;
            var hasSufficientBalance = await _walletService.HasSufficientFreeBalanceAsync(contract.ClientUserId, totalRequired);
            if (!hasSufficientBalance)
                throw new InvalidOperationException($"رصيد العميل المتاح غير كافٍ. المبلغ المطلوب: {totalRequired}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _walletService.TransferFreeToFrozenAsync(contract.ClientUserId, totalRequired);

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
                var totalRequired = contract.TotalAmount + contract.PenaltyAmount;
                await _walletService.TransferFrozenToFreeAsync(contract.ClientUserId, totalRequired);

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

            // If ShiftEndTime is provided, validate it as well
            if (contract.ShiftEndTime.HasValue)
            {
                if (contract.ShiftEndTime.Value < TimeSpan.Zero || contract.ShiftEndTime.Value >= TimeSpan.FromDays(1))
                    throw new InvalidOperationException($"وقت انتهاء الوردية غير صالح: {contract.ShiftEndTime.Value}");

                var shiftEnd = contractDayEgyptLocal.Date.Add(contract.ShiftEndTime.Value);

                if (currentTimeEgypt < shiftStart.Subtract(gracePeriod))
                    throw new InvalidOperationException($"لا يمكن التسجيل قبل بدء الوردية. تبدأ الوردية عند {shiftStart:HH:mm} (مع فترة سماح 30 دقيقة)");

                if (currentTimeEgypt > shiftEnd.Add(gracePeriod))
                    throw new InvalidOperationException($"لا يمكن التسجيل بعد انتهاء الوردية. انتهت الوردية عند {shiftEnd:HH:mm} (مع فترة سماح 30 دقيقة)");
            }
            else
            {
                // No shift end time, only validate against shift start
                if (currentTimeEgypt < shiftStart.Subtract(gracePeriod))
                    throw new InvalidOperationException($"لا يمكن التسجيل قبل بدء الوردية. تبدأ الوردية عند {shiftStart:HH:mm} (مع فترة سماح 30 دقيقة)");
            }

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

            // Dispute can be reported at any time during the day (no time restriction)
            // var shiftEndTime = contract.ShiftEndTime ?? contract.ShiftStartTime;
            // var gracePeriodEnd = contractDay.Date.Add(shiftEndTime).AddHours(3);
            // var currentTime = DateTime.UtcNow;
            // if (currentTime > gracePeriodEnd)
            //     throw new InvalidOperationException($"Dispute cannot be reported after 3-hour grace period expired. Grace period ended at: {gracePeriodEnd}");

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

        public async Task<object?> GetContractByIdAsync(int contractId, string userId)
        {
            var contract = await _context.Contracts
                .Include(c => c.ClientUser)
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

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

            return new
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
                contract.ContractDays,
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
        }

        public async Task<List<object>> GetContractsByUserIdAsync(string userId, string? status = null, int pageNumber = 1, int pageSize = 20)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            // Get the user's phone number to match against ServiceProviderPhoneNumber
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return new List<object>();

            var query = _context.Contracts
                .Include(c => c.ContractDays)
                .Include(c => c.ClientUser)
                .Where(c => c.ServiceProviderPhoneNumber == user.PhoneNumber || c.ClientUserId == userId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            var contracts = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new List<object>();
            foreach (var contract in contracts)
            {
                var providerUser = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

                result.Add(new
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
                    contract.ContractDays,
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
                });
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

            // Calculate adjustment amount based on days worked
            var adjustmentAmount = daysWorked * contract.DailySalary;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (adjustmentAmount > 0)
                {
                    if (direction == "client_to_free")
                    {
                        await _walletService.TransferFrozenToFreeAsync(contract.ClientUserId, adjustmentAmount);
                    }
                    else if (direction == "client_to_worker")
                    {
                        await _walletService.SubtractFromFrozenBalanceAsync(contract.ClientUserId, adjustmentAmount);
                        await _walletService.AddToFreeBalanceAsync(providerUser.Id, adjustmentAmount);
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
