using System.Linq.Expressions;
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
    [Authorize]
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
            string skill = "Unknown";

            if (x is Worker)
                skill = x.Skill;
            else if (x is Company)
                skill = x.Business;
            else if (x is Contractor)
                skill = x.Specialization;
            else if (x is Engineer)
                skill = x.Specialization;
            else if (x is MarketPlace)
                skill = x.Business;

            return new
            {
                name = $"{x.User.FirstName} {x.User.LastName}",
                skill = skill,
                governorate = x.User.Governorate,
                city = x.User.City,
                district = x.User.District,
                pay = pay,
                owner = isCompany ? x.Owner : (string?)null,
                imageUrl = x.User.ImageUrl,
                isCompany = isCompany,
                workerType = workerType,
                mobileNumber = x.User.PhoneNumber,
                email = x.User.Email,
                typeOfService = x.ProviderType?.ToString(),
                aboutMe = x.Bio
            };
        }

        [HttpPost("workers")]
        public async Task<IActionResult> SearchWorkers([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var workers = _context.Workers.Include(w => w.User).AsQueryable();

                if (filter != null)
                {
                    if (filter.BasedOnPoints == true)
                    {
                        workers = workers.OrderByDescending(w => w.User.Points).Take(10);
                        var list = await workers.ToListAsync();
                        return Ok(list.Select(w => MapResult(w, false, Convert.ToInt32(filter.WorkerType), w.ServicePricePerDay)).ToList());
                    }

                    if (filter.WorkerType != null)
                        workers = workers.Where(w => w.WorkerType == filter.WorkerType);

                    if (!string.IsNullOrEmpty(filter.FirstName))
                        workers = workers.Where(w => w.User.FirstName != null && w.User.FirstName.Contains(filter.FirstName));

                    if (!string.IsNullOrEmpty(filter.LastName))
                        workers = workers.Where(w => w.User.LastName != null && w.User.LastName.Contains(filter.LastName));

                    if (!string.IsNullOrEmpty(filter.Governorate))
                        workers = workers.Where(w => w.User.Governorate != null && w.User.Governorate.Contains(filter.Governorate));

                    if (!string.IsNullOrEmpty(filter.City))
                        workers = workers.Where(w => w.User.City != null && w.User.City.Contains(filter.City));

                    if (!string.IsNullOrEmpty(filter.District))
                        workers = workers.Where(w => w.User.District != null && w.User.District.Contains(filter.District));

                    if (!string.IsNullOrEmpty(filter.Profession))
                        workers = workers.Where(w => w.Skill != null && w.Skill.Contains(filter.Profession));
                }

                workers = workers.Where(w => w.IsAvailable);
                workers = Helper.PaginateUsers(workers, filter!.PageNumber, Constants.PAGE_SIZE);

                var finalList = await workers.ToListAsync();
                return Ok(finalList.Select(w => MapResult(w, false, Convert.ToInt32(filter.WorkerType), w.ServicePricePerDay)).ToList());
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
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var companies = _context.Companies.Include(c => c.User).AsQueryable();

                if (filter != null && filter.BasedOnPoints == true)
                {
                    companies = companies.OrderByDescending(c => c.User.Points).Take(10);
                    var list = await companies.ToListAsync();
                    return Ok(list.Select(c => MapResult(c, true, 1, 0)).ToList());
                }

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FirstName))
                        companies = companies.Where(c => c.User.UserName != null && c.User.UserName.Contains(filter.FirstName));

                    if (!string.IsNullOrEmpty(filter.LastName))
                        companies = companies.Where(c => c.User.LastName != null && c.User.LastName.Contains(filter.LastName));

                    if (!string.IsNullOrEmpty(filter.Governorate))
                        companies = companies.Where(c => c.User.Governorate != null && c.User.Governorate.Contains(filter.Governorate));

                    if (!string.IsNullOrEmpty(filter.City))
                        companies = companies.Where(c => c.User.City != null && c.User.City.Contains(filter.City));

                    if (!string.IsNullOrEmpty(filter.District))
                        companies = companies.Where(c => c.User.District != null && c.User.District.Contains(filter.District));

                    if (!string.IsNullOrEmpty(filter.Profession))
                        companies = companies.Where(c => c.Business != null && c.Business.Contains(filter.Profession));
                }

                companies = companies.Where(c => c.IsAvailable);
                companies = Helper.PaginateUsers(companies, filter!.PageNumber, Constants.PAGE_SIZE);

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
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var contractors = _context.Contractors.Include(c => c.User).AsQueryable();

                if (filter != null && filter.BasedOnPoints == true)
                {
                    contractors = contractors.OrderByDescending(c => c.User.Points).Take(10);
                    var list = await contractors.ToListAsync();
                    return Ok(list.Select(c => MapResult(c, false, 1, c.Salary)).ToList());
                }

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FirstName))
                        contractors = contractors.Where(c => c.User.FirstName != null && c.User.FirstName.Contains(filter.FirstName));
                    if (!string.IsNullOrEmpty(filter.LastName))
                        contractors = contractors.Where(c => c.User.LastName != null && c.User.LastName.Contains(filter.LastName));
                    if (!string.IsNullOrEmpty(filter.Governorate))
                        contractors = contractors.Where(c => c.User.Governorate != null && c.User.Governorate.Contains(filter.Governorate));
                    if (!string.IsNullOrEmpty(filter.City))
                        contractors = contractors.Where(c => c.User.City != null && c.User.City.Contains(filter.City));
                    if (!string.IsNullOrEmpty(filter.District))
                        contractors = contractors.Where(c => c.User.District != null && c.User.District.Contains(filter.District));
                    if (!string.IsNullOrEmpty(filter.Profession))
                        contractors = contractors.Where(c => c.Specialization != null && c.Specialization.Contains(filter.Profession));
                }

                contractors = contractors.Where(c => c.IsAvailable);
                contractors = Helper.PaginateUsers(contractors, filter!.PageNumber, Constants.PAGE_SIZE);

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
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var marketplaces = _context.MarketPlaces.Include(m => m.User).AsQueryable();

                if (filter != null && filter.BasedOnPoints == true)
                {
                    marketplaces = marketplaces.OrderByDescending(m => m.User.Points).Take(10);
                    var list = await marketplaces.ToListAsync();
                    return Ok(list.Select(m => MapResult(m, true, 1, 0)).ToList());
                }

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FirstName))
                        marketplaces = marketplaces.Where(m => m.User.FirstName != null && m.User.FirstName.Contains(filter.FirstName));
                    if (!string.IsNullOrEmpty(filter.LastName))
                        marketplaces = marketplaces.Where(m => m.User.LastName != null && m.User.LastName.Contains(filter.LastName));
                    if (!string.IsNullOrEmpty(filter.Governorate))
                        marketplaces = marketplaces.Where(m => m.User.Governorate != null && m.User.Governorate.Contains(filter.Governorate));
                    if (!string.IsNullOrEmpty(filter.City))
                        marketplaces = marketplaces.Where(m => m.User.City != null && m.User.City.Contains(filter.City));
                    if (!string.IsNullOrEmpty(filter.District))
                        marketplaces = marketplaces.Where(m => m.User.District != null && m.User.District.Contains(filter.District));
                    if (!string.IsNullOrEmpty(filter.Profession))
                        marketplaces = marketplaces.Where(m => m.Business != null && m.Business.Contains(filter.Profession));
                }

                marketplaces = marketplaces.Where(m => m.IsAvailable);
                marketplaces = Helper.PaginateUsers(marketplaces, filter!.PageNumber, Constants.PAGE_SIZE);

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
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var engineers = _context.Engineers.Include(e => e.User).AsQueryable();

                if (filter != null && filter.BasedOnPoints == true)
                {
                    engineers = engineers.OrderByDescending(e => e.User.Points).Take(10);
                    var list = await engineers.ToListAsync();
                    return Ok(list.Select(e => MapResult(e, false, 1, e.Salary)).ToList());
                }

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FirstName))
                        engineers = engineers.Where(e => e.User.FirstName != null && e.User.FirstName.Contains(filter.FirstName));
                    if (!string.IsNullOrEmpty(filter.LastName))
                        engineers = engineers.Where(e => e.User.LastName != null && e.User.LastName.Contains(filter.LastName));
                    if (!string.IsNullOrEmpty(filter.Governorate))
                        engineers = engineers.Where(e => e.User.Governorate != null && e.User.Governorate.Contains(filter.Governorate));
                    if (!string.IsNullOrEmpty(filter.City))
                        engineers = engineers.Where(e => e.User.City != null && e.User.City.Contains(filter.City));
                    if (!string.IsNullOrEmpty(filter.District))
                        engineers = engineers.Where(e => e.User.District != null && e.User.District.Contains(filter.District));
                    if (!string.IsNullOrEmpty(filter.Profession))
                        engineers = engineers.Where(e => e.Specialization != null && e.Specialization.Contains(filter.Profession));
                }

                engineers = engineers.Where(e => e.IsAvailable);
                engineers = Helper.PaginateUsers(engineers, filter!.PageNumber, Constants.PAGE_SIZE);

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
