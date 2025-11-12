

using System.Runtime.InteropServices;
using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Controllers
{

    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]

    public class ProfileController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserManager<User> _userManager;

        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<User> userManager, IUserService userService, ApplicationDbContext context)
        {
            _userService = userService;
            _userManager = userManager;
            _context = context;
        }
        // Get the profile of the worker
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                    return Unauthorized();

                var user = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (!user.ServiceProvider.IsAvailable)
                {
                    return Unauthorized(new
                    {
                        message = "Your subscription has expired",
                        LastDate = user.Subscription.EndDate.ToString()
                    });
                }

                if (user == null)
                    return NotFound();

                return Ok(user.ToShowProfileDto());


            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        //Update the location and availability and skills of the worker
        [HttpPut]

        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileDto model)
        {
            try
            {
                /* Authentication Stage Check*/
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }
                var user = await _context.Users
    .Include(u => u.ServiceProvider)
    .Include(u => u.Subscription)
    .FirstOrDefaultAsync(u => u.Id == userId);

                if (!user.ServiceProvider.IsAvailable)
                {
                    return Unauthorized(new
                    {
                        message = "Your subscription has expired",
                        LastDate = user.Subscription.EndDate.ToString()
                    });
                }
                if (user == null)
                {
                    return NotFound(new { messag = "User not found" });
                }
                using var transaction = await _context.Database.BeginTransactionAsync();




                /* End of Authentcation the User is added*/


                /* Update User Data */

                user.FirstName = model.FirstName ?? user.FirstName;
                user.LastName = model.LastName ?? user.LastName;




                /* Update the Service Provider Data */




                if (user == null && user.UserType != "SP")
                {
                    return NotFound(new { message = "The Service Provider is not found here" });
                }


                user.ServiceProvider.Bio = model.Bio ?? user.ServiceProvider.Bio;


                if (user.ServiceProvider.ProviderType == "Worker")
                {
                    var worker = await _context.Workers.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                    worker.ServicePricePerDay = model.Pay;

                    worker!.Skill = model.Skill;
                }
                else if (user.ServiceProvider.ProviderType == "Contractor")
                {
                    var contractor = await _context.Contractors.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                    contractor.Salary = model.Pay;

                    contractor!.Specialization = model.Specialization ?? contractor.Specialization;
                }
                else if (user.ServiceProvider.ProviderType == "Company")
                {
                    var company = await _context.Companies.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                    company!.Owner = model.Owner ?? company.Owner;
                    company!.Business = model.Business ?? company.Business;
                }
                else if (user.ServiceProvider.ProviderType == "Marketplace")
                {
                    var marketPlace = await _context.MarketPlaces.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                    marketPlace!.Business = model.Business ?? marketPlace.Business;
                }
                else if (user.ServiceProvider.ProviderType == "Engineer")
                {
                    var engineer = await _context.Engineers.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                    engineer!.Salary = model.Pay;
                    engineer!.Specialization = model.Specialization ?? engineer.Specialization;
                }
                else
                {
                    return BadRequest(new { message = "Put a correct ServiceProvider Name" });
                }

                await transaction.CommitAsync();
                await _context.SaveChangesAsync();

                return Ok(new { message = "Your Profile has been updated Successfully" });

            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

}
