using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EgyptOnline.Application.Services.Kyc
{
    public class KycService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public KycService(ApplicationDbContext context, INotificationService notificationService, IEmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _notificationService = notificationService;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task<KycSubmission> SubmitKycAsync(string userId, string frontImagePath, string backImagePath, string selfieImagePath)
        {
            // Check user exists
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                throw new InvalidOperationException("المستخدم غير موجود");

            // Check if there's already a pending or approved KYC submission
            // Prevents resetting verified identity or flooding the review queue
            var existing = await _context.KycSubmissions
                .Where(k => k.UserId == userId && (k.Status == "pending" || k.Status == "approved"))
                .FirstOrDefaultAsync();

            if (existing != null && existing.Status == "approved")
                throw new InvalidOperationException("تم التحقق من هويتك بالفعل ولا يمكن إعادة تقديم طلب التحقق");

            if (existing != null && existing.Status == "pending")
                throw new InvalidOperationException("يوجد طلب تحقق قيد المراجعة بالفعل");


            var submission = new KycSubmission
            {
                UserId = userId,
                Status = "pending",
                FrontImagePath = frontImagePath,
                BackImagePath = backImagePath,
                SelfieImagePath = selfieImagePath,
                SubmittedAt = DateTime.UtcNow
            };

            _context.KycSubmissions.Add(submission);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            try
            {
                await _notificationService.SendNotificationToUser(
                    userId,
                    "تم استلام طلب التحقق الشخصي",
                    "تم استلام صور التحقق الخاصة بك وسيتم مراجعتها قريبًا.",
                    "kyc");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send KYC submission notification to user {UserId}", userId);
            }

            // Send email to admin
            try
            {
                var adminEmail = _configuration["Admin:Email"];
                if (!string.IsNullOrEmpty(adminEmail))
                {
                    var subject = "طلب تحقق شخصي جديد - معاك";
                    var body = $"تم استلام طلب تحقق شخصي جديد:\n\n" +
                              $"اسم المستخدم: {user?.FirstName} {user?.LastName}\n" +
                              $"رقم الهاتف: {user?.PhoneNumber}\n" +
                              $"تاريخ التقديم: {submission.SubmittedAt:yyyy-MM-dd HH:mm:ss}\n" +
                              $"معرف المستخدم: {userId}\n" +
                              $"معرف الطلب: {submission.Id}\n\n" +
                              $"يرجى مراجعة الطلب في لوحة التحكم.";

                    await _emailService.SendEmailAsync(adminEmail, subject, body);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send KYC submission email to admin");
            }

            return submission;
        }

        public async Task<KycSubmission?> GetLatestKycAsync(string userId)
        {
            return await _context.KycSubmissions
                .Where(k => k.UserId == userId)
                .OrderByDescending(k => k.SubmittedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<KycSubmission> ReviewKycAsync(int kycId, string adminUserId, string status, string? rejectionReason)
        {
            var submission = await _context.KycSubmissions.FirstOrDefaultAsync(k => k.Id == kycId)
                ?? throw new KeyNotFoundException("طلب التحقق غير موجود");

            if (submission.Status != "pending" && submission.Status != "edit_required")
                throw new InvalidOperationException("هذا الطلب تمت مراجعته بالفعل");

            submission.Status = status;
            submission.ReviewedByAdminId = adminUserId;
            submission.ReviewedAt = DateTime.UtcNow;

            if ((status == "rejected" || status == "edit_required") && !string.IsNullOrWhiteSpace(rejectionReason))
            {
                submission.RejectionReason = rejectionReason;
            }

            await _context.SaveChangesAsync();

            try
            {
                var title = "تحديث طلب التحقق الشخصي";
                var body = status switch
                {
                    "approved" => "تهانينا! تم الموافقة على التحقق من هويتك وتفعيل حسابك بالكامل.",
                    "rejected" => $"تم رفض طلب التحقق الشخصي. السبب: {rejectionReason ?? "غير محدد"}",
                    "edit_required" => $"الصور المرفوعة غير واضحة أو غير كاملة: {rejectionReason ?? "يرجى إعادة تصوير البطاقة الشخصية بوضوح وإعادة الرفع."}",
                    _ => "تم تحديث حالة التحقق الشخصي الخاصة بك"
                };

                await _notificationService.SendNotificationToUser(submission.UserId, title, body, "kyc");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send KYC review notification to user {UserId}", submission.UserId);
            }

            return submission;
        }

        public async Task<List<object>> GetPendingKycSubmissionsAsync(string? search = null, int pageNumber = 1, int pageSize = 20)
        {
            var query = _context.KycSubmissions
                .Include(k => k.User)
                .Where(k => k.Status == "pending");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(k =>
                    k.User.FirstName.ToLower().Contains(searchLower) ||
                    k.User.LastName.ToLower().Contains(searchLower) ||
                    k.User.PhoneNumber.Contains(searchLower) ||
                    k.User.UserName.ToLower().Contains(searchLower));
            }

            var submissions = await query
                .OrderByDescending(k => k.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new List<object>();
            foreach (var submission in submissions)
            {
                result.Add(new
                {
                    submission.Id,
                    submission.UserId,
                    userName = submission.User?.UserName,
                    firstName = submission.User?.FirstName,
                    lastName = submission.User?.LastName,
                    phoneNumber = submission.User?.PhoneNumber,
                    submission.Status,
                    submission.SubmittedAt,
                    submission.FrontImagePath,
                    submission.BackImagePath,
                    submission.SelfieImagePath,
                    submission.RejectionReason,
                    submission.ReviewedAt
                });
            }

            return result;
        }
    }
}
