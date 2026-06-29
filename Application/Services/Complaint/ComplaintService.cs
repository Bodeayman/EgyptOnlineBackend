using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Services;
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
            string description)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            // Verify the reporter is a party to this contract
            var reporterUsername = await _context.Users
                .Where(u => u.Id == reporterUserId)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("المستخدم غير موجود");

            bool isParty = contract.ContractorUsername == reporterUsername
                        || contract.EngineerUsername   == reporterUsername
                        || contract.WorkerUsername     == reporterUsername;

            if (!isParty)
                throw new UnauthorizedAccessException("أنت لست طرفاً في هذا العقد");

            if (contract.Status == "completed" || contract.Status == "cancelled")
                throw new InvalidOperationException("لا يمكن تقديم شكوى على عقد منتهٍ أو ملغى");

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
                ContractId     = contractId,
                Reason         = reason,
                Description    = description,
                Status         = "open"
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();

            // Notify the other parties
            await SafeNotifyByUsername(contract.ContractorUsername, reporterUsername, "شكوى جديدة", $"تم تقديم شكوى على العقد #{contractId}");
            await SafeNotifyByUsername(contract.EngineerUsername,   reporterUsername, "شكوى جديدة", $"تم تقديم شكوى على العقد #{contractId}");
            await SafeNotifyByUsername(contract.WorkerUsername,     reporterUsername, "شكوى جديدة", $"تم تقديم شكوى على العقد #{contractId}");

            return complaint;
        }

        /// <summary>
        /// Get all complaints submitted by the current user.
        /// </summary>
        public async Task<List<Models.Complaint>> GetMyComplaintsAsync(string userId, int pageNumber = 1, int pageSize = 20)
        {
            return await _context.Complaints
                .Include(c => c.Contract)
                .Where(c => c.ReporterUserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        /// <summary>
        /// Get a single complaint by ID (user must be the reporter or an admin).
        /// </summary>
        public async Task<Models.Complaint?> GetByIdAsync(int complaintId)
        {
            return await _context.Complaints
                .Include(c => c.Contract)
                .Include(c => c.Reporter)
                .FirstOrDefaultAsync(c => c.Id == complaintId);
        }

        // ── ADMIN ACTIONS ─────────────────────────────────────────────────────

        /// <summary>
        /// List all complaints — optionally filtered by status.
        /// </summary>
        public async Task<(List<Models.Complaint> Items, int TotalCount)> GetAllComplaintsAsync(
            string? statusFilter = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var query = _context.Complaints
                .Include(c => c.Reporter)
                .Include(c => c.Contract)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter))
                query = query.Where(c => c.Status == statusFilter);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
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
                .Include(c => c.Reporter)
                .FirstOrDefaultAsync(c => c.Id == complaintId)
                ?? throw new KeyNotFoundException("الشكوى غير موجودة");

            if (complaint.Status == "resolved" || complaint.Status == "rejected")
                throw new InvalidOperationException("هذه الشكوى تمت معالجتها بالفعل");

            complaint.Status           = newStatus;
            complaint.ResolvedByAdminId = adminUserId;
            complaint.AdminNote        = adminNote;

            if (newStatus == "resolved" || newStatus == "rejected")
                complaint.ResolvedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify the reporter
            var statusText = newStatus switch
            {
                "under_review" => "قيد المراجعة",
                "resolved"     => "تم حلها",
                "rejected"     => "مرفوضة",
                _              => newStatus
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

        private async Task SafeNotifyByUsername(string targetUsername, string senderUsername, string title, string body)
        {
            if (targetUsername == senderUsername) return; // don't notify the reporter themselves
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
