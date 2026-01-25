using System.Linq.Expressions;
using System.Reflection;
using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using EgyptOnline.Domain.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize(Roles = Roles.User)]
    public class SearchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SearchController> _logger;
        private readonly IUserService _userService;

        public SearchController(ApplicationDbContext context, ILogger<SearchController> logger, IUserService userService)
        {
            _logger = logger;
            _context = context;
            _userService = userService;
        }

        #region Helper Methods

        private static FilterSearchDto TrimAllSearchInputs(FilterSearchDto? filterSearchDto)
        {
            if (filterSearchDto == null)
            {
                return filterSearchDto;
            }

            var properties = typeof(FilterSearchDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
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

        private static object MapResult(dynamic x, bool isCompany, int workerType, decimal pay)
        {
            string skill = x.GetSpecialization();

            return new
            {
                userId = x.User.Id,
                name = $"{x.User.FirstName} {x.User.LastName}",
                skill = skill,
                governorate = x.User.Governorate,
                city = x.User.City,
                district = x.User.District,
                pay = isCompany ? 0 : pay,
                owner = isCompany ? x.Owner : (string?)null,
                imageUrl = x.User.ImageUrl,
                isCompany = isCompany,
                workerType = workerType,
                mobileNumber = x.User.PhoneNumber,
                typeOfService = x.ProviderType?.ToString(),
                aboutMe = x.Bio,
            };
        }

        private IQueryable<Worker> ApplyWorkerFilters(IQueryable<Worker> workers, FilterSearchDto? filter)
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

            return workers;
        }

        private IQueryable<Company> ApplyCompanyFilters(IQueryable<Company> companies, FilterSearchDto? filter)
        {
            if (filter == null) return companies;

            if (!string.IsNullOrEmpty(filter.FirstName))
                companies = companies.Where(c =>
                    c.User.UserName != null &&
                    c.User.UserName.ToLower().Contains(filter.FirstName.ToLower()));

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

        private IQueryable<Contractor> ApplyContractorFilters(IQueryable<Contractor> contractors, FilterSearchDto? filter)
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

        private IQueryable<MarketPlace> ApplyMarketPlaceFilters(IQueryable<MarketPlace> marketplaces, FilterSearchDto? filter)
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
                    m.Business != null &&
                    m.Business.ToLower().Contains(filter.Profession.ToLower()));

            return marketplaces;
        }

        private IQueryable<Engineer> ApplyEngineerFilters(IQueryable<Engineer> engineers, FilterSearchDto? filter)
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

        private IQueryable<Assistant> ApplyAssistantFilters(IQueryable<Assistant> assistants, FilterSearchDto? filter)
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

        #endregion

        #region Search Endpoints

        [HttpPost("workers")]
        [RequireSubscription]
        public async Task<IActionResult> SearchWorkers([FromBody] FilterSearchDto? filter)
        {
            try
            {
                var workers = _context.Workers.Include(w => w.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                // Apply filters
                workers = ApplyWorkerFilters(workers, filter);
                workers = workers.Where(w => w.IsAvailable);

                // BasedOnPoints: Return top performers only (no pagination)
                if (filter != null && filter.BasedOnPoints == true)
                {
                    workers = workers
                        .OrderByDescending(w => w.User.Points)
                        .Take(Constants.SEARCH_PAGE_SIZE);

                    var topList = await workers.ToListAsync();
                    return Ok(topList.Select(w => MapResult(w, false,
                        Convert.ToInt32(w.WorkerType), w.ServicePricePerDay)).ToList());
                }

                // Regular search: Point-based ranking with pagination
                workers = workers.OrderByDescending(w => w.User.Points);
                workers = Helper.PaginateUsers(workers, filter!.PageNumber, Constants.PAGE_SIZE);

                var finalList = await workers.ToListAsync();
                return Ok(finalList.Select(w => MapResult(w, false,
                    Convert.ToInt32(w.WorkerType), w.ServicePricePerDay)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchWorkers failed: {Message}", ex.Message);
                return StatusCode(500, new { message = $"Internal Error: {ex.Message}" });
            }
        }

        [HttpPost("companies")]
        [RequireSubscription]
        public async Task<IActionResult> SearchCompanies([FromBody] FilterSearchDto? filter)
        {
            try
            {
                var companies = _context.Companies.Include(c => c.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                // Apply filters
                companies = ApplyCompanyFilters(companies, filter);
                companies = companies.Where(c => c.IsAvailable);

                // BasedOnPoints: Return top performers only (no pagination)
                if (filter != null && filter.BasedOnPoints == true)
                {
                    companies = companies
                        .OrderByDescending(c => c.User.Points)
                        .Take(Constants.SEARCH_PAGE_SIZE);

                    var topList = await companies.ToListAsync();
                    return Ok(topList.Select(c => MapResult(c, true, 1, 0)).ToList());
                }

                // Regular search: Point-based ranking with pagination
                companies = companies.OrderByDescending(c => c.User.Points);
                companies = Helper.PaginateUsers(companies, filter!.PageNumber, Constants.PAGE_SIZE);

                var finalList = await companies.ToListAsync();
                return Ok(finalList.Select(c => MapResult(c, true, 1, 0)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchCompanies failed: {Message}", ex.Message);
                return StatusCode(500, new { message = $"Internal Error: {ex.Message}" });
            }
        }

        [HttpPost("contractors")]
        [RequireSubscription]
        public async Task<IActionResult> SearchContractors([FromBody] FilterSearchDto? filter)
        {
            try
            {
                var contractors = _context.Contractors.Include(c => c.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                // Apply filters
                contractors = ApplyContractorFilters(contractors, filter);
                contractors = contractors.Where(c => c.IsAvailable);

                // BasedOnPoints: Return top performers only (no pagination)
                if (filter != null && filter.BasedOnPoints == true)
                {
                    contractors = contractors
                        .OrderByDescending(c => c.User.Points)
                        .Take(Constants.SEARCH_PAGE_SIZE);

                    var topList = await contractors.ToListAsync();
                    return Ok(topList.Select(c => MapResult(c, false, 1, c.Salary)).ToList());
                }

                // Regular search: Point-based ranking with pagination
                contractors = contractors.OrderByDescending(c => c.User.Points);
                contractors = Helper.PaginateUsers(contractors, filter!.PageNumber, Constants.PAGE_SIZE);

                var finalList = await contractors.ToListAsync();
                return Ok(finalList.Select(c => MapResult(c, false, 1, c.Salary)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchContractors failed: {Message}", ex.Message);
                return StatusCode(500, new { message = $"Internal Error: {ex.Message}" });
            }
        }

        [HttpPost("marketplaces")]
        [RequireSubscription]
        public async Task<IActionResult> SearchMarketPlaces([FromBody] FilterSearchDto? filter)
        {
            try
            {
                var marketplaces = _context.MarketPlaces.Include(m => m.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                // Apply filters
                marketplaces = ApplyMarketPlaceFilters(marketplaces, filter);
                marketplaces = marketplaces.Where(m => m.IsAvailable);

                // BasedOnPoints: Return top performers only (no pagination)
                if (filter != null && filter.BasedOnPoints == true)
                {
                    marketplaces = marketplaces
                        .OrderByDescending(m => m.User.Points)
                        .Take(Constants.SEARCH_PAGE_SIZE);

                    var topList = await marketplaces.ToListAsync();
                    return Ok(topList.Select(m => MapResult(m, true, 1, 0)).ToList());
                }

                // Regular search: Point-based ranking with pagination
                marketplaces = marketplaces.OrderByDescending(m => m.User.Points);
                marketplaces = Helper.PaginateUsers(marketplaces, filter!.PageNumber, Constants.PAGE_SIZE);

                var finalList = await marketplaces.ToListAsync();
                return Ok(finalList.Select(m => MapResult(m, true, 1, 0)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchMarketPlaces failed: {Message}", ex.Message);
                return StatusCode(500, new { message = $"Internal Error: {ex.Message}" });
            }
        }

        [HttpPost("engineers")]
        [RequireSubscription]
        public async Task<IActionResult> SearchEngineers([FromBody] FilterSearchDto? filter)
        {
            try
            {
                var engineers = _context.Engineers.Include(e => e.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                // Apply filters
                engineers = ApplyEngineerFilters(engineers, filter);
                engineers = engineers.Where(e => e.IsAvailable);

                // BasedOnPoints: Return top performers only (no pagination)
                if (filter != null && filter.BasedOnPoints == true)
                {
                    engineers = engineers
                        .OrderByDescending(e => e.User.Points)
                        .Take(Constants.SEARCH_PAGE_SIZE);

                    var topList = await engineers.ToListAsync();
                    return Ok(topList.Select(e => MapResult(e, false, 1, e.Salary)).ToList());
                }

                // Regular search: Point-based ranking with pagination
                engineers = engineers.OrderByDescending(e => e.User.Points);
                engineers = Helper.PaginateUsers(engineers, filter!.PageNumber, Constants.PAGE_SIZE);

                var finalList = await engineers.ToListAsync();
                return Ok(finalList.Select(e => MapResult(e, false, 1, e.Salary)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchEngineers failed: {Message}", ex.Message);
                return StatusCode(500, new { message = $"Internal Error: {ex.Message}" });
            }
        }

        [HttpPost("assistants")]
        [RequireSubscription]
        public async Task<IActionResult> SearchAssistants([FromBody] FilterSearchDto? filter)
        {
            try
            {
                var assistants = _context.Assistants.Include(a => a.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                // Apply filters
                assistants = ApplyAssistantFilters(assistants, filter);
                assistants = assistants.Where(a => a.IsAvailable);

                // BasedOnPoints: Return top performers only (no pagination)
                if (filter != null && filter.BasedOnPoints == true)
                {
                    assistants = assistants
                        .OrderByDescending(a => a.User.Points)
                        .Take(Constants.SEARCH_PAGE_SIZE);

                    var topList = await assistants.ToListAsync();
                    return Ok(topList.Select(a => MapResult(a, false, 1, a.ServicePricePerDay)).ToList());
                }

                // Regular search: Point-based ranking with pagination
                assistants = assistants.OrderByDescending(a => a.User.Points);
                assistants = Helper.PaginateUsers(assistants, filter!.PageNumber, Constants.PAGE_SIZE);

                var finalList = await assistants.ToListAsync();
                return Ok(finalList.Select(a => MapResult(a, false, 1, a.ServicePricePerDay)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchAssistants failed: {Message}", ex.Message);
                return StatusCode(500, new { message = $"Internal Error: {ex.Message}" });
            }
        }

        [HttpPost("providers")]
        public async Task<IActionResult> ReturnFirstProviders()
        {
            try
            {
                var users = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .Where(u => u.ServiceProvider != null)
                    .OrderByDescending(u => u.Points)
                    .Take(10)
                    .ToListAsync();

                return Ok(users.Select(u => new
                {
                    u.ImageUrl,
                    FullName = $"{u.FirstName} {u.LastName}",
                    Specialization = u.ServiceProvider.ProviderType,
                    u.Points
                }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReturnFirstProviders failed: {Message}", ex.Message);
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        #endregion
    }
}