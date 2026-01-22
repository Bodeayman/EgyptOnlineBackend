using System.Linq.Expressions;
using System.Reflection;
using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
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


        private static FilterSearchDto TrimAllSearchInputs(FilterSearchDto? filterSearchDto)
        {
            if (filterSearchDto == null)

            {
                return filterSearchDto;

            }

            // Get all public instance properties
            var properties = typeof(FilterSearchDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                // Only handle string properties
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

        private async Task<bool> CheckSubscription()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
            var user = await _context.Users
                .Include(u => u.Subscription)
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user?.ServiceProvider?.IsAvailable ?? false;
        }

        private static object MapResult(dynamic x, bool isCompany, int workerType, decimal pay)
        {

            string skill = x.GetSpecialization();

            return new
            {
                userId = x.User.Id,
                userName = x.User.UserName,
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
                email = x.User.Email,
                typeOfService = x.ProviderType?.ToString(),
                aboutMe = x.Bio,
                Points = x.User.Points

            };
        }

        [HttpPost("workers")]
        public async Task<IActionResult> SearchWorkers([FromBody] FilterSearchDto? filter)
        {
            try
            {

                if (!await CheckSubscription())
                {
                    return Unauthorized(new
                    {
                        message = "Your Subscription period Expired",
                        errorCode = UserErrors.SubscriptionInvalid.ToString()
                    });
                }
                var workers = _context.Workers.Include(w => w.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                if (filter != null)
                {
                    if (filter.WorkerType != null)
                        workers = workers.Where(w => w.WorkerType == filter.WorkerType);

                    if (filter.BasedOnPoints == true)
                    {
                        workers = workers.Where(w => w.IsAvailable).OrderByDescending(w => w.User.Points).ThenBy(w => Guid.NewGuid()).Take(Constants.SEARCH_PAGE_SIZE);
                        var list = await workers.ToListAsync();
                        return Ok(list.Select(w => MapResult(w, false, Convert.ToInt32(w.WorkerType), w.ServicePricePerDay)).ToList());
                    }

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

                }
                workers = workers.Where(w => w.IsAvailable);

                workers = Helper.PaginateUsers(workers, filter!.PageNumber, Constants.PAGE_SIZE);
                workers = workers.OrderBy(w => Guid.NewGuid());
                var finalList = await workers.ToListAsync();
                return Ok(finalList.Select(w => MapResult(w, false, Convert.ToInt32(w.WorkerType), w.ServicePricePerDay)).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }

        [HttpPost("companies")]
        public async Task<IActionResult> SearchCompanies([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new
                    {
                        message = "Your Subscription period Expired",
                        errorCode = UserErrors.SubscriptionInvalid.ToString()
                    });

                var companies = _context.Companies.Include(c => c.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                if (filter != null && filter.BasedOnPoints == true)
                {
                    companies = companies.OrderByDescending(c => c.User.Points).ThenBy(w => Guid.NewGuid()).Take(Constants.SEARCH_PAGE_SIZE);
                    var list = await companies.ToListAsync();
                    return Ok(list.Select(c => MapResult(c, true, 1, 0)).ToList());
                }

                if (filter != null)
                {
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

                }

                companies = companies.Where(c => c.IsAvailable);
                companies = Helper.PaginateUsers(companies, filter!.PageNumber, Constants.PAGE_SIZE);
                companies = companies.OrderBy(c => Guid.NewGuid());
                var finalList = await companies.ToListAsync();
                return Ok(finalList.Select(c => MapResult(c, true, 1, 0)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return StatusCode(500, new { message = $"Internal Error: {ex.Message}" });
            }
        }

        [HttpPost("contractors")]
        public async Task<IActionResult> SearchContractors([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new
                    {
                        message = "Your Subscription period Expired",
                        errorCode = UserErrors.SubscriptionInvalid.ToString()
                    });

                var contractors = _context.Contractors.Include(c => c.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                if (filter != null && filter.BasedOnPoints == true)
                {
                    contractors = contractors.OrderByDescending(c => c.User.Points).ThenBy(w => Guid.NewGuid()).Take(Constants.SEARCH_PAGE_SIZE);
                    var list = await contractors.ToListAsync();
                    return Ok(list.Select(c => MapResult(c, false, 1, c.Salary)).ToList());
                }

                if (filter != null)
                {
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

                }

                contractors = contractors.Where(c => c.IsAvailable);
                contractors = Helper.PaginateUsers(contractors, filter!.PageNumber, Constants.PAGE_SIZE);
                contractors = contractors.OrderBy(c => Guid.NewGuid());
                var finalList = await contractors.ToListAsync();
                return Ok(finalList.Select(c => MapResult(c, false, 1, c.Salary)).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }

        [HttpPost("marketplaces")]
        public async Task<IActionResult> SearchMarketPlaces([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new
                    {
                        message = "Your Subscription period Expired",
                        errorCode = UserErrors.SubscriptionInvalid.ToString(),

                    });

                var marketplaces = _context.MarketPlaces.Include(m => m.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                if (filter != null && filter.BasedOnPoints == true)
                {
                    marketplaces = marketplaces.OrderByDescending(m => m.User.Points).ThenBy(w => Guid.NewGuid()).Take(Constants.SEARCH_PAGE_SIZE);
                    var list = await marketplaces.ToListAsync();
                    return Ok(list.Select(m => MapResult(m, true, 1, 0)).ToList());
                }

                if (filter != null)
                {
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

                }

                marketplaces = marketplaces.Where(m => m.IsAvailable);
                marketplaces = Helper.PaginateUsers(marketplaces, filter!.PageNumber, Constants.PAGE_SIZE);
                marketplaces = marketplaces.OrderBy(m => Guid.NewGuid());
                var finalList = await marketplaces.ToListAsync();
                return Ok(finalList.Select(m => MapResult(m, true, 1, 0)).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }

        [HttpPost("engineers")]
        public async Task<IActionResult> SearchEngineers([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new
                    {
                        message = "Your Subscription period Expired",
                        errorCode = UserErrors.SubscriptionInvalid.ToString()
                    });

                var engineers = _context.Engineers.Include(e => e.User).AsQueryable();
                filter = TrimAllSearchInputs(filter);

                if (filter != null && filter.BasedOnPoints == true)
                {
                    engineers = engineers.OrderByDescending(e => e.User.Points).ThenBy(w => Guid.NewGuid()).Take(Constants.SEARCH_PAGE_SIZE);
                    var list = await engineers.ToListAsync();
                    return Ok(list.Select(e => MapResult(e, false, 1, e.Salary)).ToList());
                }

                if (filter != null)
                {
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

                }

                engineers = engineers.Where(e => e.IsAvailable);
                engineers = Helper.PaginateUsers(engineers, filter!.PageNumber, Constants.PAGE_SIZE);
                engineers = engineers.OrderBy(e => Guid.NewGuid());

                var finalList = await engineers.ToListAsync();
                return Ok(finalList.Select(e => MapResult(e, false, 1, e.Salary)).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
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
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }
    }
}
