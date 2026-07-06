using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using EgyptOnline.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EgyptOnline.Application.Services.Complaint
{
    public class ComplaintService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public ComplaintService(ApplicationDbContext context, INotificationService notificationService, IEmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _notificationService = notificationService;
            _emailService = emailService;
            _configuration = configuration;
        }

        // ── USER ACTIONS ──────────────────────────────────────────────────────

        /// <summary>
        /// File a new complaint against a contract.
        /// The contract must be active and the reporter must be one of its parties.
        /// </summary>
        public async Task<Models.Complaint> FileComplaintAsync(
            string reporterUserId,
            int contractId,
            string reason,
            string description,
            string reportType = "dispute")
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            // Verify the reporter is a party to this contract
            var reporterUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == reporterUserId)
                ?? throw new InvalidOperationException("المستخدم غير موجود");

            bool isParty = contract.ClientUserId == reporterUserId
                        || contract.ServiceProviderPhoneNumber == reporterUser.PhoneNumber;

            if (!isParty)
                throw new UnauthorizedAccessException("أنت لست طرفاً في هذا العقد");

            if (contract.Status == "completed" || contract.Status == "cancelled")
                throw new InvalidOperationException("لا يمكن تقديم شكوى على عقد منتهٍ أو ملغا");

            // Check if there's already an open complaint on this contract by this user
            var existingOpen = await _context.Complaints
                .AnyAsync(c => c.ContractId == contractId
                            && c.ReporterUserId == reporterUserId
                            && c.Status == "open");

            if (existingOpen)
                throw new InvalidOperationException("لديك شكوى مفتوحة بالفعل على هذا العقد");

            var complaint = new Models.Complaint
            {
                ReporterUserId = reporterUserId,
                ContractId = contractId,
                Reason = reason,
                Description = description,
                ReportType = reportType,
                Status = "open"
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();

            // Notify the other party
            string otherPartyId;
            if (contract.ClientUserId == reporterUserId)
            {
                // Reporter is client, notify provider by phone number
                var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);
                otherPartyId = providerUser?.Id;
            }
            else
            {
                // Reporter is provider, notify client
                otherPartyId = contract.ClientUserId;
            }

            if (!string.IsNullOrEmpty(otherPartyId))
            {
                await SafeNotifyByUserId(otherPartyId, reporterUserId, "شكوى جديدة", $"تم تقديم شكوى على العقد #{contractId}");
            }

            // Send email to admin
            try
            {
                var adminEmail = _configuration["Admin:Email"];
                if (!string.IsNullOrEmpty(adminEmail))
                {
                    var subject = "شكوى جديدة - معاك";
                    var body = $"تم استلام شكوى جديدة:\n\n" +
                              $"اسم المبلغ: {reporterUser.FirstName} {reporterUser.LastName}\n" +
                              $"رقم هاتف المبلغ: {reporterUser.PhoneNumber}\n" +
                              $"سبب الشكوى: {reason}\n" +
                              $"وصف الشكوى: {description}\n" +
                              $"نوع التقرير: {reportType}\n" +
                              $"معرف العقد: {contractId}\n" +
                              $"تاريخ التقديم: {complaint.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
                              $"معرف المستخدم: {reporterUserId}\n" +
                              $"معرف الشكوى: {complaint.Id}\n\n" +
                              $"يرجى مراجعة الشكوى في لوحة التحكم.";

                    await _emailService.SendEmailAsync(adminEmail, subject, body);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send complaint email to admin");
            }

            return complaint;
        }

        /// <summary>
        /// Get all complaints submitted by the current user.
        /// </summary>
        public async Task<List<object>> GetMyComplaintsAsync(string userId, int pageNumber = 1, int pageSize = Constants.PAGE_SIZE)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var complaints = await Helper.PaginateUsers(
                    _context.Complaints
                        .Include(c => c.Contract)
                        .Where(c => c.ReporterUserId == userId)
                        .OrderByDescending(c => c.CreatedAt),
                    pageNumber,
                    pageSize)
                .ToListAsync();

            var result = new List<object>();
            foreach (var complaint in complaints)
            {
                var providerUser = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .FirstOrDefaultAsync(u => u.PhoneNumber == complaint.Contract.ServiceProviderPhoneNumber);

                var clientUser = await _context.Users.FindAsync(complaint.Contract.ClientUserId);

                bool isClientReporter = complaint.ReporterUserId == complaint.Contract.ClientUserId;
                string reporterType = isClientReporter ? "client" : "provider";

                result.Add(new
                {
                    complaint.Id,
                    complaint.ContractId,
                    complaint.Reason,
                    complaint.Description,
                    complaint.ReportType,
                    complaint.Status,
                    complaint.CreatedAt,
                    complaint.ResolvedAt,
                    complaint.AdminNote,
                    reporterType,
                    contract = new
                    {
                        complaint.Contract.Id,
                        complaint.Contract.Status,
                        complaint.Contract.TotalAmount,
                        complaint.Contract.TotalDays,
                        complaint.Contract.DailySalary,
                        complaint.Contract.PenaltyAmount,
                        complaint.Contract.StartDate,
                        complaint.Contract.TerminationReason,
                        client = new
                        {
                            id = clientUser?.Id,
                            firstName = clientUser?.FirstName,
                            lastName = clientUser?.LastName,
                            phoneNumber = clientUser?.PhoneNumber
                        },
                        provider = new
                        {
                            id = providerUser?.Id,
                            firstName = providerUser?.FirstName,
                            lastName = providerUser?.LastName,
                            phoneNumber = providerUser?.PhoneNumber,
                            specialization = providerUser?.ServiceProvider?.GetSpecialization()
                        }
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// Get a single complaint by ID (user must be the reporter or an admin).
        /// </summary>
        public async Task<Models.Complaint?> GetByIdAsync(int complaintId)
        {
            return await _context.Complaints
                .Include(c => c.Contract)
                .FirstOrDefaultAsync(c => c.Id == complaintId);
        }

        // ── ADMIN ACTIONS ─────────────────────────────────────────────────────

        /// <summary>
        /// List all complaints — optionally filtered by status and search.
        /// </summary>
        public async Task<(List<object> Items, int TotalCount)> GetAllComplaintsAsync(
            string? statusFilter = null,
            string? search = null,
            int pageNumber = 1,
            int pageSize = 20,
            bool includeDays = false)
        {
            var baseQuery = _context.Complaints
                .Include(c => c.Contract)
                    .ThenInclude(c => c.ClientUser)
                .Include(c => c.ReporterUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter))
                baseQuery = baseQuery.Where(c => c.Status == statusFilter);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                baseQuery = baseQuery.Where(c =>
                    c.Reason.ToLower().Contains(searchLower) ||
                    c.Description.ToLower().Contains(searchLower) ||
                    c.ReporterUser.FirstName.ToLower().Contains(searchLower) ||
                    c.ReporterUser.LastName.ToLower().Contains(searchLower) ||
                    c.Contract.ClientUser.FirstName.ToLower().Contains(searchLower) ||
                    c.Contract.ClientUser.LastName.ToLower().Contains(searchLower) ||
                    c.Contract.ServiceProviderPhoneNumber.Contains(searchLower));
            }

            var total = await baseQuery.CountAsync();

            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var itemsQuery = baseQuery
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    ComplaintContractId = c.ContractId,
                    c.Reason,
                    c.Description,
                    c.ReportType,
                    c.Status,
                    c.CreatedAt,
                    c.ResolvedAt,
                    c.AdminNote,
                    c.ReporterUserId,
                    ContractId = c.Contract.Id,
                    ContractStatus = c.Contract.Status,
                    ContractTotalAmount = c.Contract.TotalAmount,
                    ContractTotalDays = c.Contract.TotalDays,
                    ContractDailySalary = c.Contract.DailySalary,
                    ContractPenaltyAmount = c.Contract.PenaltyAmount,
                    ContractStartDate = c.Contract.StartDate,
                    ContractShiftStartTime = c.Contract.ShiftStartTime,
                    ContractShiftEndTime = c.Contract.ShiftEndTime,
                    ContractGovernorate = c.Contract.Governorate,
                    ContractCity = c.Contract.City,
                    ContractDistrict = c.Contract.District,
                    ContractDetailedAddress = c.Contract.DetailedAddress,
                    ContractNotes = c.Contract.Notes,
                    ContractRestrictedTerms = c.Contract.RestrictedTerms,
                    ContractTerminationReason = c.Contract.TerminationReason,
                    ContractCreatedAt = c.Contract.CreatedAt,
                    ContractCancelledAt = c.Contract.CancelledAt,
                    ContractCancelledBy = c.Contract.CancelledBy,
                    ContractUpdatedAt = c.Contract.UpdatedAt,
                    ContractCompletedAt = c.Contract.CompletedAt,
                    ContractTerminatedAt = c.Contract.TerminatedAt,
                    ContractTerminatedBy = c.Contract.TerminatedBy,
                    ClientUserId = c.Contract.ClientUserId,
                    ServiceProviderPhoneNumber = c.Contract.ServiceProviderPhoneNumber
                });

            var items = await itemsQuery.ToListAsync();

            // Load all related users in single queries
            var allUserIds = items.Select(i => i.ClientUserId).Concat(items.Select(i => i.ReporterUserId)).Distinct().ToList();
            var allPhoneNumbers = items.Select(i => i.ServiceProviderPhoneNumber).Distinct().ToList();

            var usersById = await _context.Users
                .Where(u => allUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.PhoneNumber })
                .ToDictionaryAsync(u => u.Id);

            var usersByPhone = await _context.Users
                .Include(u => u.ServiceProvider)
                .Where(u => allPhoneNumbers.Contains(u.PhoneNumber))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.PhoneNumber, u.ServiceProvider })
                .ToDictionaryAsync(u => u.PhoneNumber);

            // Load contract days if includeDays is true
            Dictionary<int, List<object>> contractDaysDict = new Dictionary<int, List<object>>();
            if (includeDays)
            {
                var contractIds = items.Select(i => i.ContractId).Distinct().ToList();
                var contractDays = await _context.ContractDays
                    .Where(cd => contractIds.Contains(cd.ContractId))
                    .OrderBy(cd => cd.DayNumber)
                    .ToListAsync();

                var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

                foreach (var cd in contractDays)
                {
                    if (!contractDaysDict.ContainsKey(cd.ContractId))
                        contractDaysDict[cd.ContractId] = new List<object>();
                    
                    contractDaysDict[cd.ContractId].Add(new
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
                    });
                }
            }

            var result = new List<object>();
            foreach (var item in items)
            {
                var clientUser = usersById.GetValueOrDefault(item.ClientUserId);
                var reporterUser = usersById.GetValueOrDefault(item.ReporterUserId);
                var providerUser = usersByPhone.GetValueOrDefault(item.ServiceProviderPhoneNumber);

                bool isClientReporter = item.ReporterUserId == item.ClientUserId;
                string reporterType = isClientReporter ? "client" : "provider";

                object contractData;
                if (includeDays)
                {
                    contractData = new
                    {
                        id = item.ContractId,
                        status = item.ContractStatus,
                        totalAmount = item.ContractTotalAmount,
                        totalDays = item.ContractTotalDays,
                        dailySalary = item.ContractDailySalary,
                        penaltyAmount = item.ContractPenaltyAmount,
                        startDate = item.ContractStartDate,
                        shiftStartTime = item.ContractShiftStartTime,
                        shiftEndTime = item.ContractShiftEndTime,
                        governorate = item.ContractGovernorate,
                        city = item.ContractCity,
                        district = item.ContractDistrict,
                        detailedAddress = item.ContractDetailedAddress,
                        notes = item.ContractNotes,
                        restrictedTerms = item.ContractRestrictedTerms,
                        terminationReason = item.ContractTerminationReason,
                        createdAt = item.ContractCreatedAt,
                        cancelledAt = item.ContractCancelledAt,
                        cancelledBy = item.ContractCancelledBy,
                        updatedAt = item.ContractUpdatedAt,
                        completedAt = item.ContractCompletedAt,
                        terminatedAt = item.ContractTerminatedAt,
                        terminatedBy = item.ContractTerminatedBy,
                        contractDays = contractDaysDict.GetValueOrDefault(item.ContractId, new List<object>()),
                        client = new
                        {
                            id = clientUser?.Id,
                            firstName = clientUser?.FirstName,
                            lastName = clientUser?.LastName,
                            phoneNumber = clientUser?.PhoneNumber
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
                else
                {
                    contractData = new
                    {
                        id = item.ContractId,
                        status = item.ContractStatus,
                        totalAmount = item.ContractTotalAmount,
                        totalDays = item.ContractTotalDays,
                        dailySalary = item.ContractDailySalary,
                        penaltyAmount = item.ContractPenaltyAmount,
                        startDate = item.ContractStartDate,
                        shiftStartTime = item.ContractShiftStartTime,
                        shiftEndTime = item.ContractShiftEndTime,
                        governorate = item.ContractGovernorate,
                        city = item.ContractCity,
                        district = item.ContractDistrict,
                        detailedAddress = item.ContractDetailedAddress,
                        notes = item.ContractNotes,
                        restrictedTerms = item.ContractRestrictedTerms,
                        terminationReason = item.ContractTerminationReason,
                        createdAt = item.ContractCreatedAt,
                        cancelledAt = item.ContractCancelledAt,
                        cancelledBy = item.ContractCancelledBy,
                        updatedAt = item.ContractUpdatedAt,
                        completedAt = item.ContractCompletedAt,
                        terminatedAt = item.ContractTerminatedAt,
                        terminatedBy = item.ContractTerminatedBy,
                        client = new
                        {
                            id = clientUser?.Id,
                            firstName = clientUser?.FirstName,
                            lastName = clientUser?.LastName,
                            phoneNumber = clientUser?.PhoneNumber
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

                result.Add(new
                {
                    item.Id,
                    item.ComplaintContractId,
                    item.Reason,
                    item.Description,
                    item.ReportType,
                    item.Status,
                    item.CreatedAt,
                    item.ResolvedAt,
                    item.AdminNote,
                    reporterType,
                    contract = contractData,
                    reporter = new
                    {
                        id = reporterUser?.Id,
                        firstName = reporterUser?.FirstName,
                        lastName = reporterUser?.LastName,
                        phoneNumber = reporterUser?.PhoneNumber
                    }
                });
            }

            return (result, total);
        }

        /// <summary>
        /// Admin changes the status of a complaint to under_review, resolved, or rejected.
        /// When resolved or rejected the resolution is recorded with a note.
        /// </summary>
        public async Task<Models.Complaint> ReviewComplaintAsync(
            int complaintId,
            string adminUserId,
            string newStatus,        // "under_review" | "resolved" | "rejected"
            string? adminNote)
        {
            var validStatuses = new[] { "under_review", "resolved", "rejected" };
            if (!validStatuses.Contains(newStatus))
                throw new ArgumentException("الحالة غير صالحة. الحالات المتاحة: under_review, resolved, rejected");

            var complaint = await _context.Complaints
                .Include(c => c.Contract)
                .FirstOrDefaultAsync(c => c.Id == complaintId)
                ?? throw new KeyNotFoundException("الشكوى غير موجودة");

            if (complaint.Status == "resolved" || complaint.Status == "rejected")
                throw new InvalidOperationException("هذه الشكوى تمت معالجتها بالفعل");

            complaint.Status = newStatus;
            complaint.ResolvedByAdminId = adminUserId;
            complaint.AdminNote = adminNote;

            if (newStatus == "resolved" || newStatus == "rejected")
                complaint.ResolvedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify the reporter
            var statusText = newStatus switch
            {
                "under_review" => "قيد المراجعة",
                "resolved" => "تم حلها",
                "rejected" => "مرفوضة",
                _ => newStatus
            };

            await SafeNotifyById(
                complaint.ReporterUserId,
                "تحديث شكواك",
                $"تم تحديث حالة شكواك #{complaintId} إلى: {statusText}");

            return complaint;
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private async Task SafeNotifyById(string userId, string title, string body)
        {
            try { await _notificationService.SendNotificationToUser(userId, title, body, "contract"); }
            catch (Exception ex) { Log.Warning(ex, "Failed to notify user {UserId}", userId); }
        }

        private async Task SafeNotifyByUserId(string userId, string senderId, string title, string body)
        {
            if (userId == senderId) return;
            try { await _notificationService.SendNotificationToUser(userId, title, body, "contract"); }
            catch (Exception ex) { Log.Warning(ex, "Failed to notify user {UserId}", userId); }
        }

        private async Task SafeNotifyByUsername(string targetUsername, string senderUsername, string title, string body)
        {
            if (targetUsername == senderUsername) return;
            try
            {
                var userId = (await _context.Users.FirstOrDefaultAsync(u => u.UserName == targetUsername))?.Id;
                if (string.IsNullOrEmpty(userId)) return;
                await _notificationService.SendNotificationToUser(userId, title, body, "contract");
            }
            catch (Exception ex) { Log.Warning(ex, "Failed to notify {Username}", targetUsername); }
        }
    }
}
