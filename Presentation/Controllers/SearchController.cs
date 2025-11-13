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
        //Search for the workers based on the things that you want
        private async Task<bool> CheckSubscription()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
            var user = await _context.Users.Include(U => U.Subscription).Include(U => U.ServiceProvider).FirstOrDefaultAsync(U => U.Id == userId);
            return user?.ServiceProvider?.IsAvailable ?? false;
        }
        [HttpPost("workers")]
        public async Task<IActionResult> SearchWorkers([FromBody] FilterSearchDto? filter)
        {
            try
            {
                bool UserSubscribed = await CheckSubscription();
                if (!UserSubscribed)
                {
                    return Unauthorized(new { message = "Your Subscription period Expired" });
                }
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

                    if (filter.LocationCoords != null)
                    {
                        double userLat = filter.LocationCoords.Latitude;
                        double userLon = filter.LocationCoords.Longitude;
                        double factor = 111.32;
                        double cosLat = Math.Cos(userLat * Math.PI / 180.0);
                        double rangeKm = 10;
                        double rangeSq = rangeKm * rangeKm;

                        workers = workers
                            .Where(u =>
                                (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                +
                                (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                <= rangeSq
                            )
                            .Select(u => new
                            {
                                Contractors = u,
                                Distance = Math.Sqrt(
                                    (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                    +
                                    (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                )
                            })
                            .OrderBy(x => x.Distance)  // <-- sort by closest
                            .Select(x => x.Contractors);
                    }
                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        workers = workers.Where(w => w.Skill != null && w.Skill.Contains(filter.Profession));
                    }
                }

                //Don't need to show
                workers = workers.Where(market => market.IsAvailable);

                workers = Helper.PaginateUsers(workers, filter!.PageNumber, Constants.PAGE_SIZE);
                var result = await workers.ToListAsync();
                return Ok(result.Select(w => new
                {
                    name = $"{w.User.FirstName} {w.User.LastName}",
                    skill = w.Skill,
                    location = w.User.Location,
                    pay = w.ServicePricePerDay,
                    owner = (string?)null, // workers don't have owners
                    imageUrl = w.User.ImageUrl,
                    isCompany = false,
                    workerType = 1,
                    mobileNumber = w.User.PhoneNumber,
                    email = w.User.Email,
                    locationOfServiceArea = w.User.Location,
                    typeOfService = w.ProviderType?.ToString(),
                    aboutMe = w.Bio
                }).ToList());

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
                bool UserSubscribed = await CheckSubscription();
                if (!UserSubscribed)
                {
                    return Unauthorized(new { message = "Your Subscription period Expired" });
                }
                var companies = _context.Companies.Include(w => w.User).AsQueryable();
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        companies = companies.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }


                    if (filter.LocationCoords != null)
                    {
                        double userLat = filter.LocationCoords.Latitude;
                        double userLon = filter.LocationCoords.Longitude;
                        double factor = 111.32;
                        double cosLat = Math.Cos(userLat * Math.PI / 180.0);
                        double rangeKm = 10;
                        double rangeSq = rangeKm * rangeKm;

                        companies = companies
                            .Where(u =>
                                (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                +
                                (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                <= rangeSq
                            )
                            .Select(u => new
                            {
                                Contractors = u,
                                Distance = Math.Sqrt(
                                    (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                    +
                                    (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                )
                            })
                            .OrderBy(x => x.Distance)  // <-- sort by closest
                            .Select(x => x.Contractors);
                    }

                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        companies = companies.Where(w => w.Business != null && w.Business.Contains(filter.Profession));
                    }
                }
                companies = companies.Where(market => market.IsAvailable);

                companies = Helper.PaginateUsers(companies, filter!.PageNumber, Constants.PAGE_SIZE);

                var result = await companies.ToListAsync();
                return Ok(result.Select(w => new
                {
                    name = $"{w.User.FirstName} {w.User.LastName}",
                    skill = w.Business,
                    location = w.User.Location,
                    pay = 0,
                    owner = w.Owner, // include owner for companies
                    imageUrl = w.User.ImageUrl,
                    isCompany = true,

                    workerType = 1,
                    mobileNumber = w.User.PhoneNumber,
                    email = w.User.Email,
                    locationOfServiceArea = w.User.Location,
                    typeOfService = w.ProviderType?.ToString(),
                    aboutMe = w.Bio
                }).ToList());

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
                bool UserSubscribed = await CheckSubscription();
                if (!UserSubscribed)
                {
                    return Unauthorized(new { message = "Your Subscription period Expired" });
                }
                var contractors = _context.Contractors.Include(w => w.User).AsQueryable();
                var userId = User?.FindFirst("uid")?.Value;
                var UserFound = await _context.Users.FirstOrDefaultAsync(User => User.Id == userId);
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        contractors = contractors.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }



                    if (filter.LocationCoords != null)
                    {
                        double userLat = filter.LocationCoords.Latitude;
                        double userLon = filter.LocationCoords.Longitude;
                        double factor = 111.32;
                        double cosLat = Math.Cos(userLat * Math.PI / 180.0);
                        double rangeKm = 10;
                        double rangeSq = rangeKm * rangeKm;

                        contractors = contractors
                            .Where(u =>
                                (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                +
                                (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                <= rangeSq
                            )
                            .Select(u => new
                            {
                                Contractors = u,
                                Distance = Math.Sqrt(
                                    (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                    +
                                    (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                )
                            })
                            .OrderBy(x => x.Distance)  // <-- sort by closest
                            .Select(x => x.Contractors);
                    }
                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        contractors = contractors.Where(w => w.Specialization != null && w.Specialization.Contains(filter.Profession));
                    }
                }
                contractors = contractors.Where(market => market.IsAvailable);

                contractors = Helper.PaginateUsers(contractors, filter!.PageNumber, Constants.PAGE_SIZE);

                var result = await contractors.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    name = $"{w.User.FirstName} {w.User.LastName}",
                    skill = w.Specialization,
                    location = w.User.Location,
                    pay = w.Salary,
                    owner = (string?)null, // contractors don't have owners
                    imageUrl = w.User.ImageUrl,
                    isCompany = false,

                    workerType = 1,
                    mobileNumber = w.User.PhoneNumber,
                    email = w.User.Email,
                    locationOfServiceArea = w.User.Location,
                    typeOfService = w.ProviderType?.ToString(),
                    aboutMe = w.Bio
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
                bool UserSubscribed = await CheckSubscription();
                if (!UserSubscribed)
                {
                    return Unauthorized(new { message = "Your Subscription period Expired" });
                }
                var marketplaces = _context.MarketPlaces.Include(w => w.User).AsQueryable();
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        marketplaces = marketplaces.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }

                    if (filter.LocationCoords != null)
                    {
                        double userLat = filter.LocationCoords.Latitude;
                        double userLon = filter.LocationCoords.Longitude;
                        double factor = 111.32;
                        double cosLat = Math.Cos(userLat * Math.PI / 180.0);
                        double rangeKm = 10;
                        double rangeSq = rangeKm * rangeKm;

                        marketplaces = marketplaces
                            .Where(u =>
                                (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                +
                                (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                <= rangeSq
                            )
                            .Select(u => new
                            {
                                Marketplace = u,
                                Distance = Math.Sqrt(
                                    (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                    +
                                    (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                )
                            })
                            .OrderBy(x => x.Distance)  // <-- sort by closest
                            .Select(x => x.Marketplace); // unwrap the marketplace object
                    }


                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        marketplaces = marketplaces.Where(w => w.Business != null && w.Business.Contains(filter.Profession));
                    }
                }
                marketplaces = marketplaces.Where(market => market.IsAvailable);
                marketplaces = Helper.PaginateUsers(marketplaces, filter!.PageNumber, Constants.PAGE_SIZE);

                var result = await marketplaces.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    name = $"{w.User.FirstName} {w.User.LastName}",
                    skill = w.Business,
                    location = w.User.Location,
                    pay = 0,
                    owner = w.Owner, // include owner
                    imageUrl = w.User.ImageUrl,
                    isCompany = true,
                    workerType = 1,
                    mobileNumber = w.User.PhoneNumber,
                    email = w.User.Email,
                    locationOfServiceArea = w.User.Location,
                    typeOfService = w.ProviderType?.ToString(),
                    aboutMe = w.Bio
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
                bool UserSubscribed = await CheckSubscription();
                Console.WriteLine("The user subscription model is working fine");
                if (!UserSubscribed)
                {
                    return Unauthorized(new { message = "Your Subscription period Expired" });
                }
                Console.WriteLine("Fetching good 1");
                var engineers = _context.Engineers
                .Include(w => w.User)

                .AsQueryable();
                Console.WriteLine("Fetching good 2");

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        engineers = engineers.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }


                    if (filter.LocationCoords != null)
                    {
                        double userLat = filter.LocationCoords.Latitude;
                        double userLon = filter.LocationCoords.Longitude;
                        double factor = 111.32;
                        double cosLat = Math.Cos(userLat * Math.PI / 180.0);
                        double rangeKm = 10;
                        double rangeSq = rangeKm * rangeKm;

                        engineers = engineers
                            .Where(u =>
                                (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                +
                                (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                <= rangeSq
                            )
                            .Select(u => new
                            {
                                Contractors = u,
                                Distance = Math.Sqrt(
                                    (u.User.LocationCoords.Latitude - userLat) * factor * ((u.User.LocationCoords.Latitude - userLat) * factor)
                                    +
                                    (u.User.LocationCoords.Longitude - userLon) * factor * cosLat * ((u.User.LocationCoords.Longitude - userLon) * factor * cosLat)
                                )
                            })
                            .OrderBy(x => x.Distance)  // <-- sort by closest
                            .Select(x => x.Contractors);
                    }
                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        engineers = engineers.Where(w => w.Specialization != null && w.Specialization.Contains(filter.Profession));
                    }
                }
                engineers = engineers.Where(market => market.IsAvailable);
                engineers = Helper.PaginateUsers(engineers, filter!.PageNumber, Constants.PAGE_SIZE);

                var result = await engineers.ToListAsync();

                foreach (var engineer in result)
                {
                    Console.WriteLine(engineer.Bio);
                }



                // The problem in fetching the data and return the engineers here

                return Ok(

                result.Select(
                w => new
                {
                    name = $"{w.User.FirstName} {w.User.LastName}",
                    skill = w.Specialization,
                    location = w.User.Location,
                    pay = w.Salary,
                    owner = (string?)null, // engineers don't have owners
                    imageUrl = w.User.ImageUrl,
                    isCompany = false,

                    workerType = 1,
                    mobileNumber = w.User.PhoneNumber,
                    email = w.User.Email,
                    locationOfServiceArea = w.User.Location,
                    typeOfService = w.ProviderType?.ToString(),
                    aboutMe = w.Bio
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