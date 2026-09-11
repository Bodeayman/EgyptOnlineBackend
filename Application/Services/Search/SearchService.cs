using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Application.Services.Search
{
    public class SearchService
    {
        private readonly ApplicationDbContext _context;
        private readonly OccupationService? _occupationService;

        public SearchService(ApplicationDbContext context, OccupationService? occupationService)
        {
            _context = context;
            _occupationService = occupationService;
        }

        public async Task<List<SearchV2ResultDto>> SearchWorkersV2Async(FilterSearchDto? filter)
        {
            var workers = _context.Workers.Include(w => w.User).AsQueryable();
            filter = TrimAllSearchInputs(filter);
            workers = ApplyWorkerFilters(workers, filter);
            workers = workers.OrderByDescending(w => w.User.Points);
            workers = Helper.PaginateUsers(workers, filter!.PageNumber, Constants.PAGE_SIZE);

            var userIds = (await workers.ToListAsync()).Select(w => w.User.Id).ToList();
            var occupiedUsers = _occupationService != null ? await _occupationService.GetOccupiedUsersBatchAsync(userIds) : new HashSet<string>();

            var ratings = await GetRatingsForUsers(userIds);
            var postPreviews = await GetPostImagePreviewsForUsers(userIds);

            var finalList = await workers.ToListAsync();
            return finalList.Select(w => MapToV2Result(w, false,
                Convert.ToInt32(w.WorkerType), w.ServicePricePerDay, occupiedUsers.Contains(w.User.Id),
                ratings.GetValueOrDefault(w.User.Id, (0, 0)),
                postPreviews.GetValueOrDefault(w.User.Id, new()))).ToList();
        }

        public async Task<List<SearchV2ResultDto>> SearchCompaniesV2Async(FilterSearchDto? filter)
        {
            var companies = _context.Companies.Include(c => c.User).AsQueryable();
            filter = TrimAllSearchInputs(filter);
            companies = ApplyCompanyFilters(companies, filter);
            companies = companies.OrderByDescending(c => c.User.Points);
            companies = Helper.PaginateUsers(companies, filter!.PageNumber, Constants.PAGE_SIZE);

            var userIds = (await companies.ToListAsync()).Select(c => c.User.Id).ToList();
            var occupiedUsers = _occupationService != null ? await _occupationService.GetOccupiedUsersBatchAsync(userIds) : new HashSet<string>();

            var ratings = await GetRatingsForUsers(userIds);
            var postPreviews = await GetPostImagePreviewsForUsers(userIds);

            var finalList = await companies.ToListAsync();
            return finalList.Select(c => MapToV2Result(c, true, 0, 0, occupiedUsers.Contains(c.User.Id),
                ratings.GetValueOrDefault(c.User.Id, (0, 0)),
                postPreviews.GetValueOrDefault(c.User.Id, new()))).ToList();
        }

        public async Task<List<SearchV2ResultDto>> SearchContractorsV2Async(FilterSearchDto? filter)
        {
            var contractors = _context.Contractors.Include(c => c.User).AsQueryable();
            filter = TrimAllSearchInputs(filter);
            contractors = ApplyContractorFilters(contractors, filter);
            contractors = contractors.OrderByDescending(c => c.User.Points);
            contractors = Helper.PaginateUsers(contractors, filter!.PageNumber, Constants.PAGE_SIZE);

            var userIds = (await contractors.ToListAsync()).Select(c => c.User.Id).ToList();
            var occupiedUsers = _occupationService != null ? await _occupationService.GetOccupiedUsersBatchAsync(userIds) : new HashSet<string>();

            var ratings = await GetRatingsForUsers(userIds);
            var postPreviews = await GetPostImagePreviewsForUsers(userIds);

            var finalList = await contractors.ToListAsync();
            return finalList.Select(c => MapToV2Result(c, false, 0, 0, occupiedUsers.Contains(c.User.Id),
                ratings.GetValueOrDefault(c.User.Id, (0, 0)),
                postPreviews.GetValueOrDefault(c.User.Id, new()))).ToList();
        }

        public async Task<List<SearchV2ResultDto>> SearchMarketPlacesV2Async(FilterSearchDto? filter)
        {
            var marketplaces = _context.MarketPlaces.Include(m => m.User).AsQueryable();
            filter = TrimAllSearchInputs(filter);
            marketplaces = ApplyMarketPlaceFilters(marketplaces, filter);
            marketplaces = marketplaces.OrderByDescending(m => m.User.Points);
            marketplaces = Helper.PaginateUsers(marketplaces, filter!.PageNumber, Constants.PAGE_SIZE);

            var userIds = (await marketplaces.ToListAsync()).Select(m => m.User.Id).ToList();
            var occupiedUsers = _occupationService != null ? await _occupationService.GetOccupiedUsersBatchAsync(userIds) : new HashSet<string>();

            var ratings = await GetRatingsForUsers(userIds);
            var postPreviews = await GetPostImagePreviewsForUsers(userIds);

            var finalList = await marketplaces.ToListAsync();
            return finalList.Select(m => MapToV2Result(m, false, 0, 0, occupiedUsers.Contains(m.User.Id),
                ratings.GetValueOrDefault(m.User.Id, (0, 0)),
                postPreviews.GetValueOrDefault(m.User.Id, new()))).ToList();
        }

        public async Task<List<SearchV2ResultDto>> SearchEngineersV2Async(FilterSearchDto? filter)
        {
            var engineers = _context.Engineers.Include(e => e.User).AsQueryable();
            filter = TrimAllSearchInputs(filter);
            engineers = ApplyEngineerFilters(engineers, filter);
            engineers = engineers.OrderByDescending(e => e.User.Points);
            engineers = Helper.PaginateUsers(engineers, filter!.PageNumber, Constants.PAGE_SIZE);

            var userIds = (await engineers.ToListAsync()).Select(e => e.User.Id).ToList();
            var occupiedUsers = _occupationService != null ? await _occupationService.GetOccupiedUsersBatchAsync(userIds) : new HashSet<string>();

            var ratings = await GetRatingsForUsers(userIds);
            var postPreviews = await GetPostImagePreviewsForUsers(userIds);

            var finalList = await engineers.ToListAsync();
            return finalList.Select(e => MapToV2Result(e, false, 0, 0, occupiedUsers.Contains(e.User.Id),
                ratings.GetValueOrDefault(e.User.Id, (0, 0)),
                postPreviews.GetValueOrDefault(e.User.Id, new()))).ToList();
        }

        public async Task<List<SearchV2ResultDto>> SearchAssistantsV2Async(FilterSearchDto? filter)
        {
            var assistants = _context.Assistants.Include(a => a.User).AsQueryable();
            filter = TrimAllSearchInputs(filter);
            assistants = ApplyAssistantFilters(assistants, filter);
            assistants = assistants.OrderByDescending(a => a.User.Points);
            assistants = Helper.PaginateUsers(assistants, filter!.PageNumber, Constants.PAGE_SIZE);

            var userIds = (await assistants.ToListAsync()).Select(a => a.User.Id).ToList();
            var occupiedUsers = _occupationService != null ? await _occupationService.GetOccupiedUsersBatchAsync(userIds) : new HashSet<string>();

            var ratings = await GetRatingsForUsers(userIds);
            var postPreviews = await GetPostImagePreviewsForUsers(userIds);

            var finalList = await assistants.ToListAsync();
            return finalList.Select(a => MapToV2Result(a, false, 0, 0, occupiedUsers.Contains(a.User.Id),
                ratings.GetValueOrDefault(a.User.Id, (0, 0)),
                postPreviews.GetValueOrDefault(a.User.Id, new()))).ToList();
        }

        public async Task<List<SearchV2ResultDto>> SearchSculptorsV2Async(FilterSearchDto? filter)
        {
            var sculptors = _context.Sculptors.Include(s => s.User).AsQueryable();
            filter = TrimAllSearchInputs(filter);
            sculptors = ApplySculptorsFilters(sculptors, filter);
            sculptors = sculptors.OrderByDescending(s => s.User.Points);
            sculptors = Helper.PaginateUsers(sculptors, filter!.PageNumber, Constants.PAGE_SIZE);

            var userIds = (await sculptors.ToListAsync()).Select(s => s.User.Id).ToList();
            var occupiedUsers = _occupationService != null ? await _occupationService.GetOccupiedUsersBatchAsync(userIds) : new HashSet<string>();

            var ratings = await GetRatingsForUsers(userIds);
            var postPreviews = await GetPostImagePreviewsForUsers(userIds);

            var finalList = await sculptors.ToListAsync();
            return finalList.Select(s => MapToV2Result(s, false, 0, 0, occupiedUsers.Contains(s.User.Id),
                ratings.GetValueOrDefault(s.User.Id, (0, 0)),
                postPreviews.GetValueOrDefault(s.User.Id, new()))).ToList();
        }

        /// <summary>
        /// Returns the public profile for a single user by id.
        /// Only explicitly public information is exposed. The user supplies the
        /// id but the lookup is fully parameterized through EF Core and the
        /// response is projected into SearchV2ResultDto (a user entity is never
        /// serialized). Returns null when the user does not exist.
        /// </summary>
        public async Task<SearchV2ResultDto?> GetUserPublicProfileAsync(string userId)
        {
            var user = await _context.Users
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return null;
            }

            var isOccupied = _occupationService != null &&
                             await _occupationService.IsUserOccupiedAsync(userId);

            // Ratings received by this user (deleted ratings no longer exist in the table)
            var ratings = await GetReceivedRatingsSummaryAsync(userId);
            var postPreviews = (await GetPostImagePreviewsForUsers([userId]))
                .GetValueOrDefault(userId, new List<string>());

            if (user.ServiceProvider != null)
            {
                var mapped = await MapProviderProfileAsync(
                    user.ServiceProvider.ProviderType, userId, isOccupied, ratings, postPreviews);
                if (mapped != null)
                {
                    return mapped;
                }
            }

            // No provider or unrecognized provider type — expose only the public base fields
            return BuildBaseProfile(user, isOccupied, ratings, postPreviews);
        }

        /// <summary>
        /// Average and count of ratings the given user RECEIVED (public reputation).
        /// A user with no ratings returns (0, 0).
        /// </summary>
        private async Task<(double average, int count)> GetReceivedRatingsSummaryAsync(string userId)
        {
            var summary = await _context.Ratings
                .Where(r => r.TargetUserId == userId)
                .GroupBy(r => r.TargetUserId)
                .Select(g => new
                {
                    Average = g.Average(r => r.RatingValue),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            return summary == null ? (0, 0) : (Math.Round(summary.Average, 1), summary.Count);
        }

        private static SearchV2ResultDto BuildBaseProfile(User user, bool isOccupied, (double average, int count) ratings, List<string> postPreviews)
        {
            return new SearchV2ResultDto
            {
                userId = user.Id,
                name = $"{user.FirstName} {user.LastName}",
                skill = string.Empty,
                governorate = user.Governorate,
                city = user.City,
                district = user.District,
                pay = 0,
                owner = null,
                imageUrl = user.ImageUrl,
                isCompany = false,
                workerType = 0,
                mobileNumber = user.PhoneNumber ?? string.Empty,
                typeOfService = user.ServiceProvider?.ProviderType,
                aboutMe = user.ServiceProvider?.Bio,
                isOccupied = isOccupied,
                marketPlace = null,
                derivedSpec = null,
                averageRating = ratings.average,
                totalRatingCount = ratings.count,
                postImagePreviews = postPreviews
            };
        }

        private async Task<SearchV2ResultDto?> MapProviderProfileAsync(string providerType, string userId, bool isOccupied, (double average, int count) ratings, List<string> postPreviews)
        {
            switch (providerType)
            {
                case "Worker":
                    var worker = await _context.Workers.Include(w => w.User).FirstOrDefaultAsync(w => w.UserId == userId);
                    return worker != null ? MapToV2Result(worker, false, Convert.ToInt32(worker.WorkerType), worker.ServicePricePerDay, isOccupied, ratings, postPreviews) : null;
                case "Company":
                    var company = await _context.Companies.Include(c => c.User).FirstOrDefaultAsync(c => c.UserId == userId);
                    return company != null ? MapToV2Result(company, true, 0, 0, isOccupied, ratings, postPreviews) : null;
                case "Contractor":
                    var contractor = await _context.Contractors.Include(c => c.User).FirstOrDefaultAsync(c => c.UserId == userId);
                    return contractor != null ? MapToV2Result(contractor, false, 0, 0, isOccupied, ratings, postPreviews) : null;
                case "Marketplace":
                    var marketPlace = await _context.MarketPlaces.Include(m => m.User).FirstOrDefaultAsync(m => m.UserId == userId);
                    return marketPlace != null ? MapToV2Result(marketPlace, false, 0, 0, isOccupied, ratings, postPreviews) : null;
                case "Engineer":
                    var engineer = await _context.Engineers.Include(e => e.User).FirstOrDefaultAsync(e => e.UserId == userId);
                    return engineer != null ? MapToV2Result(engineer, false, 0, 0, isOccupied, ratings, postPreviews) : null;
                case "Assistant":
                    var assistant = await _context.Assistants.Include(a => a.User).FirstOrDefaultAsync(a => a.UserId == userId);
                    return assistant != null ? MapToV2Result(assistant, false, 0, 0, isOccupied, ratings, postPreviews) : null;
                case "Sculptor":
                    var sculptor = await _context.Sculptors.Include(s => s.User).FirstOrDefaultAsync(s => s.UserId == userId);
                    return sculptor != null ? MapToV2Result(sculptor, false, Convert.ToInt32(sculptor.WorkerType), sculptor.ServicePricePerDay, isOccupied, ratings, postPreviews) : null;
                default:
                    return null;
            }
        }

        private async Task<Dictionary<string, (double average, int count)>> GetRatingsForUsers(List<string> userIds)
        {
            var ratings = await _context.Ratings
                .Where(r => userIds.Contains(r.UserId))
                .GroupBy(r => r.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Average = g.Average(r => r.RatingValue),
                    Count = g.Count()
                })
                .ToListAsync();

            return ratings.ToDictionary(r => r.UserId, r => (Math.Round(r.Average, 1), r.Count));
        }

        private async Task<Dictionary<string, List<string>>> GetPostImagePreviewsForUsers(List<string> userIds)
        {
            var previews = await _context.Posts
                .Where(p => userIds.Contains(p.UserId) && p.Photos.Any())
                .Select(p => new
                {
                    p.UserId,
                    FirstPhotoUrl = p.Photos.OrderBy(ph => ph.Order).Select(ph => ph.PhotoUrl).FirstOrDefault()
                })
                .ToListAsync();

            var grouped = previews
                .GroupBy(p => p.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    PhotoUrls = g.Take(4).Select(p => p.FirstPhotoUrl).Where(url => url != null).ToList()
                })
                .ToList();

            return grouped.ToDictionary(g => g.UserId, g => g.PhotoUrls);
        }

        private static FilterSearchDto? TrimAllSearchInputs(FilterSearchDto? filterSearchDto)
        {
            if (filterSearchDto == null) return filterSearchDto;

            var properties = typeof(FilterSearchDto).GetProperties();
            foreach (var prop in properties)
            {
                if (prop.PropertyType == typeof(string))
                {
                    var value = (string?)prop.GetValue(filterSearchDto);
                    if (!string.IsNullOrEmpty(value))
                    {
                        prop.SetValue(filterSearchDto, value.Trim());
                    }
                }
            }
            return filterSearchDto;
        }

        private static IQueryable<Worker> ApplyWorkerFilters(IQueryable<Worker> workers, FilterSearchDto? filter)
        {
            if (filter == null) return workers;

            if (filter.WorkerType != null)
                workers = workers.Where(w => w.WorkerType == filter.WorkerType);

            if (!string.IsNullOrEmpty(filter.FirstName))
                workers = workers.Where(w =>
                    w.User.FirstName != null &&
                    w.User.FirstName.ToLower().Contains(filter.FirstName.ToLower()));

            if (!string.IsNullOrEmpty(filter.LastName))
                workers = workers.Where(w =>
                    w.User.LastName != null &&
                    w.User.LastName.ToLower().Contains(filter.LastName.ToLower()));

            if (!string.IsNullOrEmpty(filter.Governorate))
                workers = workers.Where(w =>
                    w.User.Governorate != null &&
                    w.User.Governorate.ToLower().Contains(filter.Governorate.ToLower()));

            if (!string.IsNullOrEmpty(filter.City))
                workers = workers.Where(w =>
                    w.User.City != null &&
                    w.User.City.ToLower().Contains(filter.City.ToLower()));

            if (!string.IsNullOrEmpty(filter.District))
                workers = workers.Where(w =>
                    w.User.District != null &&
                    w.User.District.ToLower().Contains(filter.District.ToLower()));

            if (!string.IsNullOrEmpty(filter.Profession))
                workers = workers.Where(w =>
                    w.Skill != null &&
                    w.Skill.ToLower().Contains(filter.Profession.ToLower()));

            if (!string.IsNullOrEmpty(filter.Marketplace))
                workers = workers.Where(a =>
                    a.MarketPlace != null &&
                    a.MarketPlace.ToLower().Contains(filter.Marketplace.ToLower()));

            if (!string.IsNullOrEmpty(filter.DerviedSpec))
                workers = workers.Where(a =>
                    a.DerivedSpec != null &&
                    a.DerivedSpec.ToLower().Contains(filter.DerviedSpec.ToLower()));

            return workers;
        }

        private static IQueryable<Company> ApplyCompanyFilters(IQueryable<Company> companies, FilterSearchDto? filter)
        {
            if (filter == null) return companies;

            if (!string.IsNullOrEmpty(filter.FirstName))
                companies = companies.Where(c =>
                    c.User.FirstName != null &&
                    c.User.FirstName.ToLower().Contains(filter.FirstName.ToLower()));

            if (!string.IsNullOrEmpty(filter.LastName))
                companies = companies.Where(c =>
                    c.User.LastName != null &&
                    c.User.LastName.ToLower().Contains(filter.LastName.ToLower()));

            if (!string.IsNullOrEmpty(filter.Governorate))
                companies = companies.Where(c =>
                    c.User.Governorate != null &&
                    c.User.Governorate.ToLower().Contains(filter.Governorate.ToLower()));

            if (!string.IsNullOrEmpty(filter.City))
                companies = companies.Where(c =>
                    c.User.City != null &&
                    c.User.City.ToLower().Contains(filter.City.ToLower()));

            if (!string.IsNullOrEmpty(filter.District))
                companies = companies.Where(c =>
                    c.User.District != null &&
                    c.User.District.ToLower().Contains(filter.District.ToLower()));

            if (!string.IsNullOrEmpty(filter.Profession))
                companies = companies.Where(c =>
                    c.Business != null &&
                    c.Business.ToLower().Contains(filter.Profession.ToLower()));

            return companies;
        }

        private static IQueryable<Contractor> ApplyContractorFilters(IQueryable<Contractor> contractors, FilterSearchDto? filter)
        {
            if (filter == null) return contractors;

            if (!string.IsNullOrEmpty(filter.FirstName))
                contractors = contractors.Where(c =>
                    c.User.FirstName != null &&
                    c.User.FirstName.ToLower().Contains(filter.FirstName.ToLower()));

            if (!string.IsNullOrEmpty(filter.LastName))
                contractors = contractors.Where(c =>
                    c.User.LastName != null &&
                    c.User.LastName.ToLower().Contains(filter.LastName.ToLower()));

            if (!string.IsNullOrEmpty(filter.Governorate))
                contractors = contractors.Where(c =>
                    c.User.Governorate != null &&
                    c.User.Governorate.ToLower().Contains(filter.Governorate.ToLower()));

            if (!string.IsNullOrEmpty(filter.City))
                contractors = contractors.Where(c =>
                    c.User.City != null &&
                    c.User.City.ToLower().Contains(filter.City.ToLower()));

            if (!string.IsNullOrEmpty(filter.District))
                contractors = contractors.Where(c =>
                    c.User.District != null &&
                    c.User.District.ToLower().Contains(filter.District.ToLower()));

            if (!string.IsNullOrEmpty(filter.Profession))
                contractors = contractors.Where(c =>
                    c.Specialization != null &&
                    c.Specialization.ToLower().Contains(filter.Profession.ToLower()));

            return contractors;
        }

        private static IQueryable<MarketPlace> ApplyMarketPlaceFilters(IQueryable<MarketPlace> marketplaces, FilterSearchDto? filter)
        {
            if (filter == null) return marketplaces;

            if (!string.IsNullOrEmpty(filter.FirstName))
                marketplaces = marketplaces.Where(m =>
                    m.User.FirstName != null &&
                    m.User.FirstName.ToLower().Contains(filter.FirstName.ToLower()));

            if (!string.IsNullOrEmpty(filter.LastName))
                marketplaces = marketplaces.Where(m =>
                    m.User.LastName != null &&
                    m.User.LastName.ToLower().Contains(filter.LastName.ToLower()));

            if (!string.IsNullOrEmpty(filter.Governorate))
                marketplaces = marketplaces.Where(m =>
                    m.User.Governorate != null &&
                    m.User.Governorate.ToLower().Contains(filter.Governorate.ToLower()));

            if (!string.IsNullOrEmpty(filter.City))
                marketplaces = marketplaces.Where(m =>
                    m.User.City != null &&
                    m.User.City.ToLower().Contains(filter.City.ToLower()));

            if (!string.IsNullOrEmpty(filter.District))
                marketplaces = marketplaces.Where(m =>
                    m.User.District != null &&
                    m.User.District.ToLower().Contains(filter.District.ToLower()));

            if (!string.IsNullOrEmpty(filter.Profession))
                marketplaces = marketplaces.Where(m =>
                    m.MarketPlace != null &&
                    m.MarketPlace.ToLower().Contains(filter.Profession.ToLower()));

            return marketplaces;
        }

        private static IQueryable<Engineer> ApplyEngineerFilters(IQueryable<Engineer> engineers, FilterSearchDto? filter)
        {
            if (filter == null) return engineers;

            if (!string.IsNullOrEmpty(filter.FirstName))
                engineers = engineers.Where(e =>
                    e.User.FirstName != null &&
                    e.User.FirstName.ToLower().Contains(filter.FirstName.ToLower()));

            if (!string.IsNullOrEmpty(filter.LastName))
                engineers = engineers.Where(e =>
                    e.User.LastName != null &&
                    e.User.LastName.ToLower().Contains(filter.LastName.ToLower()));

            if (!string.IsNullOrEmpty(filter.Governorate))
                engineers = engineers.Where(e =>
                    e.User.Governorate != null &&
                    e.User.Governorate.ToLower().Contains(filter.Governorate.ToLower()));

            if (!string.IsNullOrEmpty(filter.City))
                engineers = engineers.Where(e =>
                    e.User.City != null &&
                    e.User.City.ToLower().Contains(filter.City.ToLower()));

            if (!string.IsNullOrEmpty(filter.District))
                engineers = engineers.Where(e =>
                    e.User.District != null &&
                    e.User.District.ToLower().Contains(filter.District.ToLower()));

            if (!string.IsNullOrEmpty(filter.Profession))
                engineers = engineers.Where(e =>
                    e.Specialization != null &&
                    e.Specialization.ToLower().Contains(filter.Profession.ToLower()));

            return engineers;
        }

        private static IQueryable<Assistant> ApplyAssistantFilters(IQueryable<Assistant> assistants, FilterSearchDto? filter)
        {
            if (filter == null) return assistants;

            if (!string.IsNullOrEmpty(filter.FirstName))
                assistants = assistants.Where(a =>
                    a.User.FirstName != null &&
                    a.User.FirstName.ToLower().Contains(filter.FirstName.ToLower()));

            if (!string.IsNullOrEmpty(filter.LastName))
                assistants = assistants.Where(a =>
                    a.User.LastName != null &&
                    a.User.LastName.ToLower().Contains(filter.LastName.ToLower()));

            if (!string.IsNullOrEmpty(filter.Governorate))
                assistants = assistants.Where(a =>
                    a.User.Governorate != null &&
                    a.User.Governorate.ToLower().Contains(filter.Governorate.ToLower()));

            if (!string.IsNullOrEmpty(filter.City))
                assistants = assistants.Where(a =>
                    a.User.City != null &&
                    a.User.City.ToLower().Contains(filter.City.ToLower()));

            if (!string.IsNullOrEmpty(filter.District))
                assistants = assistants.Where(a =>
                    a.User.District != null &&
                    a.User.District.ToLower().Contains(filter.District.ToLower()));

            if (!string.IsNullOrEmpty(filter.Profession))
                assistants = assistants.Where(a =>
                    a.Skill != null &&
                    a.Skill.ToLower().Contains(filter.Profession.ToLower()));

            return assistants;
        }

        private static IQueryable<Sculptor> ApplySculptorsFilters(IQueryable<Sculptor> sculptors, FilterSearchDto? filter)
        {
            if (filter == null) return sculptors;

            if (!string.IsNullOrEmpty(filter.FirstName))
                sculptors = sculptors.Where(s =>
                    s.User.FirstName != null &&
                    s.User.FirstName.ToLower().Contains(filter.FirstName.ToLower()));

            if (!string.IsNullOrEmpty(filter.LastName))
                sculptors = sculptors.Where(s =>
                    s.User.LastName != null &&
                    s.User.LastName.ToLower().Contains(filter.LastName.ToLower()));

            if (!string.IsNullOrEmpty(filter.Governorate))
                sculptors = sculptors.Where(s =>
                    s.User.Governorate != null &&
                    s.User.Governorate.ToLower().Contains(filter.Governorate.ToLower()));

            if (!string.IsNullOrEmpty(filter.City))
                sculptors = sculptors.Where(s =>
                    s.User.City != null &&
                    s.User.City.ToLower().Contains(filter.City.ToLower()));

            if (!string.IsNullOrEmpty(filter.District))
                sculptors = sculptors.Where(s =>
                    s.User.District != null &&
                    s.User.District.ToLower().Contains(filter.District.ToLower()));

            return sculptors;
        }

        private static SearchV2ResultDto MapToV2Result(Worker worker, bool isCompany, int workerType, decimal pay, bool isOccupied, (double average, int count) ratings, List<string> postPreviews)
        {
            return new SearchV2ResultDto
            {
                userId = worker.User.Id,
                name = $"{worker.User.FirstName} {worker.User.LastName}",
                skill = worker.Skill ?? string.Empty,
                governorate = worker.User.Governorate,
                city = worker.User.City,
                district = worker.User.District,
                pay = isCompany ? 0 : pay,
                owner = isCompany ? string.Empty : (string?)null,
                imageUrl = worker.User.ImageUrl,
                isCompany = isCompany,
                workerType = workerType,
                mobileNumber = worker.User.PhoneNumber,
                typeOfService = worker.ProviderType?.ToString(),
                aboutMe = worker.Bio,
                isOccupied = isOccupied,
                marketPlace = worker.MarketPlace,
                derivedSpec = worker.DerivedSpec,
                averageRating = ratings.average,
                totalRatingCount = ratings.count,
                postImagePreviews = postPreviews
            };
        }

        private static SearchV2ResultDto MapToV2Result(Company company, bool isCompany, int workerType, decimal pay, bool isOccupied, (double average, int count) ratings, List<string> postPreviews)
        {
            return new SearchV2ResultDto
            {
                userId = company.User.Id,
                name = $"{company.User.FirstName} {company.User.LastName}",
                skill = company.Business ?? string.Empty,
                governorate = company.User.Governorate,
                city = company.User.City,
                district = company.User.District,
                pay = 0,
                owner = company.Owner,
                imageUrl = company.User.ImageUrl,
                isCompany = true,
                workerType = 0,
                mobileNumber = company.User.PhoneNumber,
                typeOfService = company.ProviderType?.ToString(),
                aboutMe = company.Bio,
                isOccupied = isOccupied,
                marketPlace = null,
                derivedSpec = null,
                averageRating = ratings.average,
                totalRatingCount = ratings.count,
                postImagePreviews = postPreviews
            };
        }

        private static SearchV2ResultDto MapToV2Result(Contractor contractor, bool isCompany, int workerType, decimal pay, bool isOccupied, (double average, int count) ratings, List<string> postPreviews)
        {
            return new SearchV2ResultDto
            {
                userId = contractor.User.Id,
                name = $"{contractor.User.FirstName} {contractor.User.LastName}",
                skill = contractor.Specialization ?? string.Empty,
                governorate = contractor.User.Governorate,
                city = contractor.User.City,
                district = contractor.User.District,
                pay = 0,
                owner = null,
                imageUrl = contractor.User.ImageUrl,
                isCompany = false,
                workerType = 0,
                mobileNumber = contractor.User.PhoneNumber,
                typeOfService = contractor.ProviderType?.ToString(),
                aboutMe = contractor.Bio,
                isOccupied = isOccupied,
                marketPlace = null,
                derivedSpec = null,
                averageRating = ratings.average,
                totalRatingCount = ratings.count,
                postImagePreviews = postPreviews
            };
        }

        private static SearchV2ResultDto MapToV2Result(MarketPlace marketplace, bool isCompany, int workerType, decimal pay, bool isOccupied, (double average, int count) ratings, List<string> postPreviews)
        {
            return new SearchV2ResultDto
            {
                userId = marketplace.User.Id,
                name = $"{marketplace.User.FirstName} {marketplace.User.LastName}",
                skill = marketplace.MarketPlace ?? string.Empty,
                governorate = marketplace.User.Governorate,
                city = marketplace.User.City,
                district = marketplace.User.District,
                pay = 0,
                owner = null,
                imageUrl = marketplace.User.ImageUrl,
                isCompany = false,
                workerType = 0,
                mobileNumber = marketplace.User.PhoneNumber,
                typeOfService = marketplace.ProviderType?.ToString(),
                aboutMe = marketplace.Bio,
                isOccupied = isOccupied,
                marketPlace = marketplace.MarketPlace,
                derivedSpec = null,
                averageRating = ratings.average,
                totalRatingCount = ratings.count,
                postImagePreviews = postPreviews
            };
        }

        private static SearchV2ResultDto MapToV2Result(Engineer engineer, bool isCompany, int workerType, decimal pay, bool isOccupied, (double average, int count) ratings, List<string> postPreviews)
        {
            return new SearchV2ResultDto
            {
                userId = engineer.User.Id,
                name = $"{engineer.User.FirstName} {engineer.User.LastName}",
                skill = engineer.Specialization ?? string.Empty,
                governorate = engineer.User.Governorate,
                city = engineer.User.City,
                district = engineer.User.District,
                pay = 0,
                owner = null,
                imageUrl = engineer.User.ImageUrl,
                isCompany = false,
                workerType = 0,
                mobileNumber = engineer.User.PhoneNumber,
                typeOfService = engineer.ProviderType?.ToString(),
                aboutMe = engineer.Bio,
                isOccupied = isOccupied,
                marketPlace = null,
                derivedSpec = null,
                averageRating = ratings.average,
                totalRatingCount = ratings.count,
                postImagePreviews = postPreviews
            };
        }

        private static SearchV2ResultDto MapToV2Result(Assistant assistant, bool isCompany, int workerType, decimal pay, bool isOccupied, (double average, int count) ratings, List<string> postPreviews)
        {
            return new SearchV2ResultDto
            {
                userId = assistant.User.Id,
                name = $"{assistant.User.FirstName} {assistant.User.LastName}",
                skill = assistant.Skill ?? string.Empty,
                governorate = assistant.User.Governorate,
                city = assistant.User.City,
                district = assistant.User.District,
                pay = 0,
                owner = null,
                imageUrl = assistant.User.ImageUrl,
                isCompany = false,
                workerType = 0,
                mobileNumber = assistant.User.PhoneNumber,
                typeOfService = assistant.ProviderType?.ToString(),
                aboutMe = assistant.Bio,
                isOccupied = isOccupied,
                marketPlace = null,
                derivedSpec = null,
                averageRating = ratings.average,
                totalRatingCount = ratings.count,
                postImagePreviews = postPreviews
            };
        }

        private static SearchV2ResultDto MapToV2Result(Sculptor sculptor, bool isCompany, int workerType, decimal pay, bool isOccupied, (double average, int count) ratings, List<string> postPreviews)
        {
            return new SearchV2ResultDto
            {
                userId = sculptor.User.Id,
                name = $"{sculptor.User.FirstName} {sculptor.User.LastName}",
                skill = sculptor.GetSpecialization(),
                governorate = sculptor.User.Governorate,
                city = sculptor.User.City,
                district = sculptor.User.District,
                pay = 0,
                owner = null,
                imageUrl = sculptor.User.ImageUrl,
                isCompany = false,
                workerType = 0,
                mobileNumber = sculptor.User.PhoneNumber,
                typeOfService = sculptor.ProviderType?.ToString(),
                aboutMe = sculptor.Bio,
                isOccupied = isOccupied,
                marketPlace = null,
                derivedSpec = null,
                averageRating = ratings.average,
                totalRatingCount = ratings.count,
                postImagePreviews = postPreviews
            };
        }
    }
}
