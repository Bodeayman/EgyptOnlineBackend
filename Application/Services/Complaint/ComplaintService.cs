using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EgyptOnline.Application.Services.Complaint
{
    public class ComplaintService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public ComplaintService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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
        /// List all complaints — optionally filtered by status.
        /// </summary>
        public async Task<(List<object> Items, int TotalCount)> GetAllComplaintsAsync(
            string? statusFilter = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var baseQuery = _context.Complaints
                .Include(c => c.Contract)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter))
                baseQuery = baseQuery.Where(c => c.Status == statusFilter);

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
                    ContractTerminationReason = c.Contract.TerminationReason,
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

            var result = new List<object>();
            foreach (var item in items)
            {
                var clientUser = usersById.GetValueOrDefault(item.ClientUserId);
                var reporterUser = usersById.GetValueOrDefault(item.ReporterUserId);
                var providerUser = usersByPhone.GetValueOrDefault(item.ServiceProviderPhoneNumber);

                bool isClientReporter = item.ReporterUserId == item.ClientUserId;
                string reporterType = isClientReporter ? "client" : "provider";

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
                    contract = new
                    {
                        id = item.ContractId,
                        status = item.ContractStatus,
                        totalAmount = item.ContractTotalAmount,
                        totalDays = item.ContractTotalDays,
                        dailySalary = item.ContractDailySalary,
                        penaltyAmount = item.ContractPenaltyAmount,
                        startDate = item.ContractStartDate,
                        terminationReason = item.ContractTerminationReason,
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
                    },
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
            try { await _notificationService.SendNotificationToUser(userId, title, body); }
            catch (Exception ex) { Log.Warning(ex, "Failed to notify user {UserId}", userId); }
        }

        private async Task SafeNotifyByUserId(string userId, string senderId, string title, string body)
        {
            if (userId == senderId) return;
            try { await _notificationService.SendNotificationToUser(userId, title, body); }
            catch (Exception ex) { Log.Warning(ex, "Failed to notify user {UserId}", userId); }
        }

        private async Task SafeNotifyByUsername(string targetUsername, string senderUsername, string title, string body)
        {
            if (targetUsername == senderUsername) return;
            try
            {
                var userId = (await _context.Users.FirstOrDefaultAsync(u => u.UserName == targetUsername))?.Id;
                if (string.IsNullOrEmpty(userId)) return;
                await _notificationService.SendNotificationToUser(userId, title, body);
            }
            catch (Exception ex) { Log.Warning(ex, "Failed to notify {Username}", targetUsername); }
        }
    }
}
