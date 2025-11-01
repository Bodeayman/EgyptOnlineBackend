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
    [Route("api/[controller]")]
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
        //Search for the workers based on the things that you want
        [HttpPost("worker")]
        public async Task<IActionResult> SearchWorkers([FromBody] FilterSearchDto? filter)
        {
            try
            {
                string UserLocation = await _userService.GetUserLocation(User);
                Console.WriteLine("User locations is {0}", UserLocation);
                var workers = _context.Workers.Include(w => w.User).AsQueryable();
                foreach (var w in workers)
                {
                    Console.WriteLine(w.User.UserName);
                }
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        workers = workers.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }

                    if (!string.IsNullOrEmpty(filter.Location))
                    {
                        workers = workers.Where(w => w.User.Location != null && w.User.Location.Contains(filter.Location));
                    }
                    else
                    {
                        workers = workers.Where(w => w.User.Location.Contains(UserLocation));
                    }

                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        workers = workers.Where(w => w.Skill != null && w.Skill.Contains(filter.Profession));
                    }
                }
                workers = Helper.PaginateUsers(workers, filter!.PageNumber, Constants.PAGE_SIZE);
                var result = await workers.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    w.User.FirstName,
                    w.User.LastName,
                    w.User.ImageUrl,
                    w.User.UserName,
                    w.User.Email,
                    w.User.PhoneNumber,
                    w.IsAvailable,
                    w.User.Location,
                    w.Skill,
                    w.Bio,
                    w.ProviderType,
                }
                )

                .ToList()

                );
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

                var companies = _context.Companies.Include(w => w.User).AsQueryable();
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        companies = companies.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }

                    if (!string.IsNullOrEmpty(filter.Location))
                    {
                        companies = companies.Where(w => w.User.Location != null && w.User.Location.Contains(filter.Location));
                    }

                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        companies = companies.Where(w => w.Business != null && w.Business.Contains(filter.Profession));
                    }
                }
                companies = Helper.PaginateUsers(companies, filter!.PageNumber, Constants.PAGE_SIZE);

                var result = await companies.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    w.User.FirstName,
                    w.User.LastName,
                    w.User.ImageUrl,
                    w.User.UserName,
                    w.User.Email,
                    w.User.PhoneNumber,
                    w.IsAvailable,
                    w.User.Location,
                    w.Business,
                    w.Bio,
                    w.ProviderType,
                }
                )

                .ToList()

                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Internal Error Message {ex.Message}");
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }
        [HttpPost("contractors")]
        public async Task<IActionResult> SearchContractors([FromBody] FilterSearchDto? filter)
        {
            try
            {

                var contractors = _context.Contractors.Include(w => w.User).AsQueryable();
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        contractors = contractors.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }

                    if (!string.IsNullOrEmpty(filter.Location))
                    {
                        contractors = contractors.Where(w => w.User.Location != null && w.User.Location.Contains(filter.Location));
                    }

                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        contractors = contractors.Where(w => w.Specialization != null && w.Specialization.Contains(filter.Profession));
                    }
                }
                contractors = Helper.PaginateUsers(contractors, filter!.PageNumber, Constants.PAGE_SIZE);

                var result = await contractors.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    w.User.FirstName,
                    w.User.LastName,
                    w.User.ImageUrl,
                    w.User.UserName,
                    w.User.Email,
                    w.User.PhoneNumber,
                    w.IsAvailable,
                    w.User.Location,
                    w.Specialization,
                    w.Bio,
                    w.ProviderType,
                }
                )

                .ToList()

                );
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

                var marketplaces = _context.MarketPlaces.Include(w => w.User).AsQueryable();
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        marketplaces = marketplaces.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }

                    if (!string.IsNullOrEmpty(filter.Location))
                    {
                        marketplaces = marketplaces.Where(w => w.User.Location != null && w.User.Location.Contains(filter.Location));
                    }

                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        marketplaces = marketplaces.Where(w => w.Business != null && w.Business.Contains(filter.Profession));
                    }
                }
                marketplaces = Helper.PaginateUsers(marketplaces, filter!.PageNumber, Constants.PAGE_SIZE);

                var result = await marketplaces.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    w.User.FirstName,
                    w.User.LastName,
                    w.User.ImageUrl,
                    w.User.UserName,
                    w.User.Email,
                    w.User.PhoneNumber,
                    w.IsAvailable,
                    w.User.Location,
                    w.Business,
                    w.Bio,
                    w.ProviderType,
                }
                )

                .ToList()

                );
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

                var engineers = _context.Engineers.Include(w => w.User).AsQueryable();
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        engineers = engineers.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }

                    if (!string.IsNullOrEmpty(filter.Location))
                    {
                        engineers = engineers.Where(w => w.User.Location != null && w.User.Location.Contains(filter.Location));
                    }

                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        engineers = engineers.Where(w => w.Specialization != null && w.Specialization.Contains(filter.Profession));
                    }
                }
                engineers = Helper.PaginateUsers(engineers, filter!.PageNumber, Constants.PAGE_SIZE);

                var result = await engineers.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    w.User.FirstName,
                    w.User.LastName,
                    w.User.ImageUrl,
                    w.User.UserName,
                    w.User.Email,
                    w.User.PhoneNumber,
                    w.IsAvailable,
                    w.User.Location,
                    w.Specialization,
                    w.Bio,
                    w.ProviderType,
                }
                )

                .ToList()

                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }

    }
}