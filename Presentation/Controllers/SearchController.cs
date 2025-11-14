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

        // ---------------------------
        // Shared mapper
        // ---------------------------
        private object MapResult(dynamic x, bool isCompany, int workerType, decimal pay)
        {
            return new
            {
                name = $"{x.User.FirstName} {x.User.LastName}",
                skill = x.Skill ?? x.Business ?? x.Specialization,
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

        // ---------------------------
        // WORKERS
        // ---------------------------
        [HttpPost("workers")]
        public async Task<IActionResult> SearchWorkers([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var workers = _context.Workers
                    .Include(w => w.User)
                    .AsQueryable();

                if (filter != null)
                {
                    if (filter.WorkerType != null)
                    {
                        workers = workers.Where(w => w.WorkerType == filter.WorkerType);
                    }
                    if (!string.IsNullOrEmpty(filter.FirstName))
                        workers = workers.Where(w => w.User.FirstName != null &&
                                                     w.User.FirstName.Contains(filter.FirstName));

                    if (!string.IsNullOrEmpty(filter.LastName))
                        workers = workers.Where(w => w.User.LastName != null &&
                                                     w.User.LastName.Contains(filter.LastName));

                    if (!string.IsNullOrEmpty(filter.Governorate))
                        workers = workers.Where(w => w.User.Governorate != null &&
                                                     w.User.Governorate.Contains(filter.Governorate));

                    if (!string.IsNullOrEmpty(filter.City))
                        workers = workers.Where(w => w.User.City != null &&
                                                     w.User.City.Contains(filter.City));

                    if (!string.IsNullOrEmpty(filter.District))
                        workers = workers.Where(w => w.User.District != null &&
                                                     w.User.District.Contains(filter.District));

                    if (!string.IsNullOrEmpty(filter.Profession))
                        workers = workers.Where(w => w.Skill != null &&
                                                     w.Skill.Contains(filter.Profession));
                }

                workers = workers.Where(w => w.IsAvailable);
                workers = Helper.PaginateUsers(workers, filter!.PageNumber, Constants.PAGE_SIZE);


                /* Calling the Database Request here*/
                var list = await workers.ToListAsync();

                return Ok(
                    list.Select(w => MapResult(w, false, Convert.ToInt32(filter.WorkerType), w.ServicePricePerDay)).ToList()
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }

        // ---------------------------
        // COMPANIES
        // ---------------------------
        [HttpPost("companies")]
        public async Task<IActionResult> SearchCompanies([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var companies = _context.Companies
                    .Include(w => w.User)
                    .AsQueryable();

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FirstName))
                        companies = companies.Where(w => w.User.UserName != null &&
                                                         w.User.UserName.Contains(filter.FirstName));
                    if (!string.IsNullOrEmpty(filter.LastName))
                        companies = companies.Where(w => w.User.LastName != null &&
                                                         w.User.LastName.Contains(filter.LastName));

                    if (!string.IsNullOrEmpty(filter.Governorate))
                        companies = companies.Where(w => w.User.Governorate != null &&
                                                         w.User.Governorate.Contains(filter.Governorate));

                    if (!string.IsNullOrEmpty(filter.City))
                        companies = companies.Where(w => w.User.City != null &&
                                                         w.User.City.Contains(filter.City));

                    if (!string.IsNullOrEmpty(filter.District))
                        companies = companies.Where(w => w.User.District != null &&
                                                         w.User.District.Contains(filter.District));

                    if (!string.IsNullOrEmpty(filter.Profession))
                        companies = companies.Where(w => w.Business != null &&
                                                         w.Business.Contains(filter.Profession));
                }

                companies = companies.Where(w => w.IsAvailable);
                companies = Helper.PaginateUsers(companies, filter!.PageNumber, Constants.PAGE_SIZE);

                var list = await companies.ToListAsync();

                return Ok(
                    list.Select(w => MapResult(w, true, 1, 0)).ToList()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return StatusCode(500, new { message = $"Internal Error: {ex.Message}" });
            }
        }

        // ---------------------------
        // CONTRACTORS
        // ---------------------------
        [HttpPost("contractors")]
        public async Task<IActionResult> SearchContractors([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var contractors = _context.Contractors
                    .Include(w => w.User)
                    .AsQueryable();

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.Governorate))
                        contractors = contractors.Where(w => w.User.Governorate != null &&
                                                     w.User.Governorate.Contains(filter.Governorate));

                    if (!string.IsNullOrEmpty(filter.City))
                        contractors = contractors.Where(w => w.User.City != null &&
                                                     w.User.City.Contains(filter.City));

                    if (!string.IsNullOrEmpty(filter.District))
                        contractors = contractors.Where(w => w.User.District != null &&
                                                     w.User.District.Contains(filter.District));

                    if (!string.IsNullOrEmpty(filter.FirstName))
                        contractors = contractors.Where(w => w.User.FirstName != null &&
                                                             w.User.FirstName.Contains(filter.FirstName));

                    if (!string.IsNullOrEmpty(filter.LastName))
                        contractors = contractors.Where(w => w.User.LastName != null &&
                                                             w.User.LastName.Contains(filter.LastName));

                    if (!string.IsNullOrEmpty(filter.Profession))
                        contractors = contractors.Where(w => w.Specialization != null &&
                                                             w.Specialization.Contains(filter.Profession));
                }

                contractors = contractors.Where(w => w.IsAvailable);
                contractors = Helper.PaginateUsers(contractors, filter!.PageNumber, Constants.PAGE_SIZE);

                var list = await contractors.ToListAsync();

                return Ok(
                    list.Select(w => MapResult(w, false, 1, w.Salary)).ToList()
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }

        // ---------------------------
        // MARKETPLACES
        // ---------------------------
        [HttpPost("marketplaces")]
        public async Task<IActionResult> SearchMarketPlaces([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var marketplaces = _context.MarketPlaces
                    .Include(w => w.User)
                    .AsQueryable();

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.Governorate))
                        marketplaces = marketplaces.Where(w => w.User.Governorate != null &&
                                                     w.User.Governorate.Contains(filter.Governorate));

                    if (!string.IsNullOrEmpty(filter.City))
                        marketplaces = marketplaces.Where(w => w.User.City != null &&
                                                     w.User.City.Contains(filter.City));

                    if (!string.IsNullOrEmpty(filter.District))
                        marketplaces = marketplaces.Where(w => w.User.District != null &&
                                                     w.User.District.Contains(filter.District));

                    if (!string.IsNullOrEmpty(filter.FirstName))
                        marketplaces = marketplaces.Where(w => w.User.FirstName != null &&
                                                             w.User.FirstName.Contains(filter.FirstName));

                    if (!string.IsNullOrEmpty(filter.LastName))
                        marketplaces = marketplaces.Where(w => w.User.LastName != null &&
                                                             w.User.LastName.Contains(filter.LastName));

                    if (!string.IsNullOrEmpty(filter.Profession))
                        marketplaces = marketplaces.Where(w => w.Business != null &&
                                                             w.Business.Contains(filter.Profession));
                }

                marketplaces = marketplaces.Where(w => w.IsAvailable);
                marketplaces = Helper.PaginateUsers(marketplaces, filter!.PageNumber, Constants.PAGE_SIZE);

                var list = await marketplaces.ToListAsync();

                return Ok(
                    list.Select(w => MapResult(w, true, 1, 0)).ToList()
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }

        // ---------------------------
        // ENGINEERS
        // ---------------------------
        [HttpPost("engineers")]
        public async Task<IActionResult> SearchEngineers([FromBody] FilterSearchDto? filter)
        {
            try
            {
                if (!await CheckSubscription())
                    return Unauthorized(new { message = "Your Subscription period Expired" });

                var engineers = _context.Engineers
                    .Include(w => w.User)
                    .AsQueryable();

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.Governorate))
                        engineers = engineers.Where(w => w.User.Governorate != null &&
                                                     w.User.Governorate.Contains(filter.Governorate));

                    if (!string.IsNullOrEmpty(filter.City))
                        engineers = engineers.Where(w => w.User.City != null &&
                                                     w.User.City.Contains(filter.City));

                    if (!string.IsNullOrEmpty(filter.District))
                        engineers = engineers.Where(w => w.User.District != null &&
                                                     w.User.District.Contains(filter.District));

                    if (!string.IsNullOrEmpty(filter.FirstName))
                        engineers = engineers.Where(w => w.User.FirstName != null &&
                                                             w.User.FirstName.Contains(filter.FirstName));

                    if (!string.IsNullOrEmpty(filter.LastName))
                        engineers = engineers.Where(w => w.User.LastName != null &&
                                                             w.User.LastName.Contains(filter.LastName));

                    if (!string.IsNullOrEmpty(filter.Profession))
                        engineers = engineers.Where(w => w.Specialization != null &&
                                                             w.Specialization.Contains(filter.Profession));
                }


                engineers = engineers.Where(e => e.IsAvailable);
                engineers = Helper.PaginateUsers(engineers, filter!.PageNumber, Constants.PAGE_SIZE);

                var list = await engineers.ToListAsync();

                return Ok(
                    list.Select(w => MapResult(w, false, 1, w.Salary)).ToList()
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }

        // ---------------------------
        // PROVIDERS BY POINTS
        // ---------------------------
        [HttpPost("providers")]
        public async Task<IActionResult> GetAllProvidersByPoint()
        {
            try
            {
                var users = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .Where(u => u.ServiceProvider != null)
                    .OrderByDescending(u => u.Points)
                    .Select(u => new
                    {
                        u.ImageUrl,
                        FullName = $"{u.FirstName} {u.LastName}",
                        Specialization = u.ServiceProvider.ProviderType,
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }
    }
}
