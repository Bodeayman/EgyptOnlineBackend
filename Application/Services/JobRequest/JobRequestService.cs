using EgyptOnline.Data;
using EgyptOnline.Dtos.JobRequest;
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
        public async Task<JobRequestSummaryDto> CreateRequestAsync(
            string clientUserId,
            string providerType,
            string skill,
            string governorate,
            string city,
            WorkerTypes? workerType,
            decimal payRate,
            int? days = null)
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
                Days = days,
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

            return new JobRequestSummaryDto
            {
                Id = request.Id,
                ClientUserId = request.ClientUserId,
                ProviderType = request.ProviderType,
                Skill = request.Skill,
                Governorate = request.Governorate,
                City = request.City,
                WorkerType = request.WorkerType.HasValue ? (int?)request.WorkerType.Value : null,
                PayRate = request.PayRate,
                Days = request.Days,
                CreatedAt = request.CreatedAt,
                Status = request.Status,
                AcceptedProviderUserId = request.AcceptedProviderUserId
            };
        }

        /// <summary>
        /// Retrieve requests created by the current user with count of interested providers.
        /// Full provider details available via separate endpoint.
        /// Ordered by pending first, then completed.
        /// </summary>
        public async Task<List<object>> GetMyRequestsAsync(string clientUserId, int pageNumber = 1, int pageSize = Constants.PAGE_SIZE)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var requests = await Helper.PaginateUsers(
                    _context.JobRequests
                        .Include(r => r.Interests)
                        .Where(r => r.ClientUserId == clientUserId)
                        .OrderBy(r => r.Status == "Pending" ? 0 : 1)
                        .ThenByDescending(r => r.CreatedAt),
                    pageNumber,
                    pageSize)
                .ToListAsync();

            var result = new List<object>();
            foreach (var r in requests)
            {
                var interestedCount = r.Interests.Count(i => i.IsInterested);

                result.Add(new
                {
                    r.Id,
                    r.ProviderType,
                    r.Skill,
                    r.Governorate,
                    r.City,
                    WorkerType = r.WorkerType.HasValue ? (int?)r.WorkerType.Value : null,
                    r.PayRate,
                    r.Days,
                    r.CreatedAt,
                    r.Status,
                    r.AcceptedProviderUserId,
                    interestedCount
                });
            }

            return result;
        }

        /// <summary>
        /// Retrieve paginated interested service providers for a job request created by the current user.
        /// </summary>
        public async Task<object> GetInterestedProvidersAsync(int requestId, string clientUserId, int pageNumber = 1, int pageSize = Constants.PAGE_SIZE)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var request = await _context.JobRequests
                .Include(r => r.Interests)
                    .ThenInclude(i => i.ServiceProviderUser!)
                        .ThenInclude(u => u.ServiceProvider)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.ClientUserId == clientUserId);

            if (request == null)
                throw new KeyNotFoundException("طلب العمل غير موجود أو لا تملك صلاحية الوصول إليه");

            var interestedInterests = request.Interests
                .Where(i => i.IsInterested && i.ServiceProviderUser != null)
                .ToList();

            var totalInterested = interestedInterests.Count;
            var providerIds = interestedInterests
                .Select(i => i.ServiceProviderUserId)
                .Distinct()
                .ToList();

            var occupiedProviders = await _occupationService.GetOccupiedUsersBatchAsync(providerIds);

            var pageItems = interestedInterests
                .Skip(pageSize * (pageNumber - 1))
                .Take(pageSize)
                .Select(i => MapServiceProvider(i.ServiceProviderUser!, occupiedProviders.Contains(i.ServiceProviderUserId)))
                .ToList();

            return new
            {
                requestId = request.Id,
                totalInterested,
                pageNumber,
                pageSize,
                items = pageItems
            };
        }

        /// <summary>
        /// Retrieve other people's requests (Pending only) with 'isInterested' status for the current user.
        /// Shows count of interested providers, not the full list.
        /// Filtered by governorate (case-insensitive).
        /// </summary>
        public async Task<List<object>> GetOtherRequestsAsync(string currentUserId, string? governorate = null, int pageNumber = 1, int pageSize = Constants.PAGE_SIZE)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var query = _context.JobRequests
                .Include(r => r.ClientUser)
                .Include(r => r.Interests)
                .Where(r => r.ClientUserId != currentUserId && r.Status == "Pending");

            if (!string.IsNullOrEmpty(governorate))
            {
                query = query.Where(r => r.Governorate != null && r.Governorate.ToLower() == governorate.ToLower());
            }

            var requests = await Helper.PaginateUsers(
                    query.OrderByDescending(r => r.CreatedAt),
                    pageNumber,
                    pageSize)
                .ToListAsync();

            var result = new List<object>();
            foreach (var r in requests)
            {
                var interest = r.Interests.FirstOrDefault(i => i.ServiceProviderUserId == currentUserId);
                var interestedCount = r.Interests.Count(i => i.IsInterested);

                result.Add(new
                {
                    r.Id,
                    r.ClientUserId,
                    clientName = r.ClientUser != null ? $"{r.ClientUser.FirstName} {r.ClientUser.LastName}" : "Unknown",
                    r.ProviderType,
                    r.Skill,
                    r.Governorate,
                    r.City,
                    WorkerType = r.WorkerType.HasValue ? (int?)r.WorkerType.Value : null,
                    r.PayRate,
                    r.Days,
                    r.CreatedAt,
                    isInterested = interest?.IsInterested ?? false,
                    canInterest = r.Status == "Pending",
                    interestedCount
                });
            }

            return result;
        }

        /// <summary>
        /// Update interested/not-interested status for a job request.
        /// Notifies the request creator when a provider marks interest.
        /// </summary>
        public async Task<JobRequestInterestResultDto> SetInterestAsync(int requestId, string serviceProviderUserId, bool isInterested)
        {
            var request = await _context.JobRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null)
                throw new KeyNotFoundException("طلب العمل غير موجود");

            if (request.Status != "Pending")
                throw new InvalidOperationException("لا يمكن إبداء الاهتمام بطلب عمل غير معلق");

            if (request.ClientUserId == serviceProviderUserId)
                throw new UnauthorizedAccessException("لا يمكنك إبداء الاهتمام بطلبك الخاص");

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

            return new JobRequestInterestResultDto
            {
                Id = interest.Id,
                JobRequestId = interest.JobRequestId,
                ServiceProviderUserId = interest.ServiceProviderUserId,
                IsInterested = interest.IsInterested,
                UpdatedAt = interest.UpdatedAt
            };
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
        public async Task<JobRequestSummaryDto> CancelRequestAsync(int requestId, string clientUserId)
        {
            var request = await _context.JobRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.ClientUserId == clientUserId);

            if (request == null)
                throw new KeyNotFoundException("طلب العمل غير موجود أو لا تملك صلاحية تعديله");

            if (request.Status != "Pending")
                throw new InvalidOperationException("لا يمكن إلغاء طلب عمل غير معلق");

            request.Status = "Cancelled";
            await _context.SaveChangesAsync();
            return new JobRequestSummaryDto
            {
                Id = request.Id,
                ClientUserId = request.ClientUserId,
                ProviderType = request.ProviderType,
                Skill = request.Skill,
                Governorate = request.Governorate,
                City = request.City,
                WorkerType = request.WorkerType.HasValue ? (int?)request.WorkerType.Value : null,
                PayRate = request.PayRate,
                Days = request.Days,
                CreatedAt = request.CreatedAt,
                Status = request.Status,
                AcceptedProviderUserId = request.AcceptedProviderUserId
            };
        }

        public async Task<JobRequestSummaryDto> CompleteRequestAsync(int requestId, string clientUserId)
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
            return new JobRequestSummaryDto
            {
                Id = request.Id,
                ClientUserId = request.ClientUserId,
                ProviderType = request.ProviderType,
                Skill = request.Skill,
                Governorate = request.Governorate,
                City = request.City,
                WorkerType = request.WorkerType.HasValue ? (int?)request.WorkerType.Value : null,
                PayRate = request.PayRate,
                Days = request.Days,
                CreatedAt = request.CreatedAt,
                Status = request.Status,
                AcceptedProviderUserId = request.AcceptedProviderUserId
            };
        }
    }
}
