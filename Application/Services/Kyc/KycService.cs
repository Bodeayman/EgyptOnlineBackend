using EgyptOnline.Data;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Application.Services.Kyc
{
    public class KycService
    {
        private readonly ApplicationDbContext _context;

        public KycService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<KycSubmission> SubmitKycAsync(string userId, string frontImagePath, string backImagePath, string selfieImagePath)
        {
            // Check user exists
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                throw new InvalidOperationException("المستخدم غير موجود");

            // Check if there's already a pending KYC submission
            var existing = await _context.KycSubmissions
                .Where(k => k.UserId == userId && k.Status == "pending")
                .FirstOrDefaultAsync();

            if (existing != null)
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

            if (submission.Status != "pending")
                throw new InvalidOperationException("هذا الطلب تمت مراجعته بالفعل");

            submission.Status = status;
            submission.ReviewedByAdminId = adminUserId;
            submission.ReviewedAt = DateTime.UtcNow;

            if (status == "rejected" && !string.IsNullOrWhiteSpace(rejectionReason))
            {
                submission.RejectionReason = rejectionReason;
            }

            await _context.SaveChangesAsync();

            return submission;
        }

        public async Task<List<KycSubmission>> GetPendingKycSubmissionsAsync(int pageNumber = 1, int pageSize = 20)
        {
            return await _context.KycSubmissions
                .Include(k => k.User)
                .Where(k => k.Status == "pending")
                .OrderBy(k => k.SubmittedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}
