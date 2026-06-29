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
        private readonly OccupationService _occupationService;

        public JobRequestService(ApplicationDbContext context, INotificationService notificationService, OccupationService occupationService)
        {
            _context = context;
            _notificationService = notificationService;
            _occupationService = occupationService;
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
                .Where(u => u.Governorate == governorate &&
                 u.City == city &&
                  u.Id != clientUserId)
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
        /// Retrieve requests created by the current user along with details of interested service providers.
        /// </summary>
        public async Task<List<object>> GetMyRequestsAsync(string clientUserId, int pageNumber = 1, int pageSize = Constants.PAGE_SIZE)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var requests = await Helper.PaginateUsers(
                    _context.JobRequests
                        .Include(r => r.Interests)
                            .ThenInclude(i => i.ServiceProviderUser!)
                                .ThenInclude(u => u.ServiceProvider)
                        .Where(r => r.ClientUserId == clientUserId)
                        .OrderByDescending(r => r.CreatedAt),
                    pageNumber,
                    pageSize)
                .ToListAsync();

            var providerIds = requests
                .SelectMany(r => r.Interests)
                .Select(i => i.ServiceProviderUserId)
                .Distinct()
                .ToList();

            var occupiedProviders = await _occupationService.GetOccupiedUsersBatchAsync(providerIds);

            var result = new List<object>();
            foreach (var r in requests)
            {
                var interestedProviders = r.Interests
                    .Where(i => i.IsInterested && i.ServiceProviderUser != null)
                    .Select(i => MapServiceProvider(i.ServiceProviderUser!, occupiedProviders.Contains(i.ServiceProviderUserId)))
                    .ToList();

                result.Add(new
                {
                    r.Id,
                    r.ProviderType,
                    r.Skill,
                    r.Governorate,
                    r.City,
                    r.WorkerType,
                    r.PayRate,
                    r.CreatedAt,
                    r.Status,
                    r.AcceptedProviderUserId,
                    interestedProviders
                });
            }

            return result;
        }

        /// <summary>
        /// Retrieve other people's requests (Pending only) with 'isInterested' status for the current user.
        /// </summary>
        public async Task<List<object>> GetOtherRequestsAsync(string currentUserId, int pageNumber = 1, int pageSize = Constants.PAGE_SIZE)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var requests = await Helper.PaginateUsers(
                    _context.JobRequests
                        .Include(r => r.ClientUser)
                        .Include(r => r.Interests)
                            .ThenInclude(i => i.ServiceProviderUser!)
                                .ThenInclude(u => u.ServiceProvider)
                        .Where(r => r.ClientUserId != currentUserId && r.Status == "Pending")
                        .OrderByDescending(r => r.CreatedAt),
                    pageNumber,
                    pageSize)
                .ToListAsync();

            var providerIds = requests
                .SelectMany(r => r.Interests)
                .Select(i => i.ServiceProviderUserId)
                .Distinct()
                .ToList();

            var occupiedProviders = await _occupationService.GetOccupiedUsersBatchAsync(providerIds);

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
                    isInterested = interest?.IsInterested ?? false,
                    canInterest = r.Status == "Pending",
                    interestedProviders = r.Interests
                        .Where(i => i.IsInterested && i.ServiceProviderUser != null)
                        .Select(i => MapServiceProvider(i.ServiceProviderUser!, occupiedProviders.Contains(i.ServiceProviderUserId)))
                        .ToList()
                });
            }

            return result;
        }

        /// <summary>
        /// Update interested/not-interested status for a job request.
        /// Notifies the request creator when a provider marks interest.
        /// </summary>
        public async Task<JobRequestInterest> SetInterestAsync(int requestId, string serviceProviderUserId, bool isInterested)
        {
            var request = await _context.JobRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null)
                throw new KeyNotFoundException("طلب العمل غير موجود");

            if (request.Status != "Pending")
                throw new InvalidOperationException("لا يمكن إبداء الاهتمام بطلب عمل غير معلق");

            var interest = await _context.JobRequestInterests
                .FirstOrDefaultAsync(i => i.JobRequestId == requestId && i.ServiceProviderUserId == serviceProviderUserId);

            bool wasNewInterest = interest == null || !interest.IsInterested;

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

            if (isInterested && wasNewInterest)
            {
                try
                {
                    var provider = await _context.Users
                        .Include(u => u.ServiceProvider)
                        .FirstOrDefaultAsync(u => u.Id == serviceProviderUserId);

                    var providerName = provider != null
                        ? $"{provider.FirstName} {provider.LastName}"
                        : "أحدهم";

                    var title = "اهتمام بطلبك";
                    var body = $"{providerName} مهتم بطلبك ({request.Skill} في {request.City}).";
                    await _notificationService.SendNotificationToUser(request.ClientUserId, title, body);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send interest notification to request creator {UserId}", request.ClientUserId);
                }
            }

            return interest;
        }

        private static object MapServiceProvider(User user, bool isOccupied)
        {
            var serviceProvider = user.ServiceProvider;
            var isCompany = serviceProvider is Company;
            var workerType = 1;
            decimal pay = 0;
            string? owner = null;
            string? typeOfService = serviceProvider?.ProviderType;
            string? aboutMe = serviceProvider?.Bio;
            string? marketPlace = serviceProvider?.MarketPlace;
            string skill = serviceProvider?.GetSpecialization() ?? string.Empty;
            string derivedSpec = serviceProvider?.GetDerivedSpecialization() ?? string.Empty;

            if (serviceProvider is Worker worker)
            {
                workerType = Convert.ToInt32(worker.WorkerType);
                pay = worker.ServicePricePerDay;
            }
            else if (serviceProvider is Assistant assistant)
            {
                workerType = 1;
                pay = assistant.ServicePricePerDay;
            }
            else if (serviceProvider is Sculptor sculptor)
            {
                workerType = Convert.ToInt32(sculptor.WorkerType);
                pay = sculptor.ServicePricePerDay;
            }
            else if (serviceProvider is Company company)
            {
                owner = company.Owner;
            }

            return new
            {
                userId = user.Id,
                name = $"{user.FirstName} {user.LastName}",
                skill,
                governorate = user.Governorate,
                city = user.City,
                district = user.District,
                pay = isCompany ? 0 : pay,
                owner = owner,
                imageUrl = user.ImageUrl,
                isCompany = isCompany,
                workerType = workerType,
                mobileNumber = user.PhoneNumber,
                typeOfService = typeOfService,
                aboutMe = aboutMe,
                isOccupied = isOccupied,
                marketPlace = marketPlace,
                derivedSpec = derivedSpec
            };
        }

        /// <summary>
        /// Remove/Delete a job request created by the user.
        /// </summary>
        public async Task DeleteRequestAsync(int requestId, string clientUserId)
        {
            var request = await _context.JobRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.ClientUserId == clientUserId);

            if (request == null)
                throw new KeyNotFoundException("طلب العمل غير موجود أو لا تملك صلاحية حذفه");

            _context.JobRequests.Remove(request);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Cancel a job request (marking it as Cancelled without deleting).
        /// </summary>
        public async Task<Models.JobRequest> CancelRequestAsync(int requestId, string clientUserId)
        {
            var request = await _context.JobRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.ClientUserId == clientUserId);

            if (request == null)
                throw new KeyNotFoundException("طلب العمل غير موجود أو لا تملك صلاحية تعديله");

            if (request.Status != "Pending")
                throw new InvalidOperationException("لا يمكن إلغاء طلب عمل غير معلق");

            request.Status = "Cancelled";
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task<Models.JobRequest> CompleteRequestAsync(int requestId, string clientUserId)
        {
            var request = await _context.JobRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.ClientUserId == clientUserId);

            if (request == null)
                throw new KeyNotFoundException("طلب العمل غير موجود أو لا تملك صلاحية تعديله");

            if (request.Status == "Cancelled")
                throw new InvalidOperationException("لا يمكن اتمام طلب عمل ملغى");

            if (request.Status == "Completed")
                throw new InvalidOperationException("طلب العمل مكتمل بالفعل");

            request.Status = "Completed";
            await _context.SaveChangesAsync();
            return request;
        }
    }
}
