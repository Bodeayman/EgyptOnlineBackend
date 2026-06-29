using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EgyptOnline.Application.Services.JobRequest
{
    public class JobRequestService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public JobRequestService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Create a new job request and notify all users in the same governorate.
        /// </summary>
        public async Task<Models.JobRequest> CreateRequestAsync(
            string clientUserId,
            string providerType,
            string skill,
            string governorate,
            string city,
            WorkerTypes? workerType,
            decimal payRate)
        {
            var request = new Models.JobRequest
            {
                ClientUserId = clientUserId,
                ProviderType = providerType,
                Skill = skill,
                Governorate = governorate,
                City = city,
                WorkerType = workerType,
                PayRate = payRate,
                CreatedAt = DateTime.UtcNow
            };

            _context.JobRequests.Add(request);
            await _context.SaveChangesAsync();

            // Notify all users registered in the same governorate (except the request creator)
            var usersInGov = await _context.Users
                .Where(u => u.Governorate == governorate && u.Id != clientUserId)
                .Select(u => u.Id)
                .ToListAsync();

            var title = "طلب عمل جديد في محافظتك";
            var body = $"مطلوب {providerType} (مهارة: {skill}) في {city} بمعدل أجر {payRate} جنيه.";

            foreach (var userId in usersInGov)
            {
                try
                {
                    await _notificationService.SendNotificationToUser(userId, title, body);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send job request notification to user {UserId}", userId);
                }
            }

            return request;
        }

        /// <summary>
        /// Retrieve requests created by the current user.
        /// </summary>
        public async Task<List<Models.JobRequest>> GetMyRequestsAsync(string clientUserId)
        {
            return await _context.JobRequests
                .Where(r => r.ClientUserId == clientUserId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieve other people's requests with 'isInterested' status for the current user.
        /// </summary>
        public async Task<List<object>> GetOtherRequestsAsync(string currentUserId)
        {
            var requests = await _context.JobRequests
                .Include(r => r.ClientUser)
                .Include(r => r.Interests)
                .Where(r => r.ClientUserId != currentUserId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var result = new List<object>();
            foreach (var r in requests)
            {
                var interest = r.Interests.FirstOrDefault(i => i.ServiceProviderUserId == currentUserId);
                result.Add(new
                {
                    r.Id,
                    r.ClientUserId,
                    clientName = r.ClientUser != null ? $"{r.ClientUser.FirstName} {r.ClientUser.LastName}" : "Unknown",
                    r.ProviderType,
                    r.Skill,
                    r.Governorate,
                    r.City,
                    r.WorkerType,
                    r.PayRate,
                    r.CreatedAt,
                    isInterested = interest?.IsInterested ?? false
                });
            }

            return result;
        }

        /// <summary>
        /// Update interested/not-interested status for a job request.
        /// </summary>
        public async Task<JobRequestInterest> SetInterestAsync(int requestId, string serviceProviderUserId, bool isInterested)
        {
            var exists = await _context.JobRequests.AnyAsync(r => r.Id == requestId);
            if (!exists)
                throw new KeyNotFoundException("طلب العمل غير موجود");

            var interest = await _context.JobRequestInterests
                .FirstOrDefaultAsync(i => i.JobRequestId == requestId && i.ServiceProviderUserId == serviceProviderUserId);

            if (interest == null)
            {
                interest = new JobRequestInterest
                {
                    JobRequestId = requestId,
                    ServiceProviderUserId = serviceProviderUserId,
                    IsInterested = isInterested,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.JobRequestInterests.Add(interest);
            }
            else
            {
                interest.IsInterested = isInterested;
                interest.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return interest;
        }
    }
}
