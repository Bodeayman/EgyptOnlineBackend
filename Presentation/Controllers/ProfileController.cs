

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

    [Route("api/[controller]")]
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
                Console.WriteLine("worksRightNOWWWWWWW");
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                    return Unauthorized();

                var user = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == userId);

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
                string newUserName = Helper.GenerateUserName(model.FirstName, model.LastName);
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { messag = "User not found" });
                }
                //So we then have a user
                //Update
                user.UserName = newUserName ?? user.UserName;
                user.Location = model.Location ?? user.Location;

                var result = await _userManager.UpdateAsync(user);


                var serviceProvider = await _context.ServiceProviders
                .FirstOrDefaultAsync(sp => sp.UserId == userId);

                if (serviceProvider == null || user.UserType == "User")
                {
                    return Ok(new { message = "User Data Updated" });
                }
                if (serviceProvider == null && user.UserType != "User")
                {
                    return NotFound(new { message = "The Service Provider is not found here" });
                }


                serviceProvider.Bio = model.Bio ?? serviceProvider.Bio;


                if (serviceProvider.ProviderType == "Worker")
                {
                    var worker = await _context.Workers.FirstOrDefaultAsync(s => serviceProvider.Id == s.Id);
                    serviceProvider.ServicePricePerDay = model.ServicePricePerDay;

                    worker!.Skill = model.Skill;
                }
                else if (serviceProvider.ProviderType == "Contractor")
                {
                    var contractor = await _context.Contractors.FirstOrDefaultAsync(s => serviceProvider.Id == s.Id);

                    contractor!.Specialization = model.Specialization ?? contractor.Specialization;
                }
                else if (serviceProvider.ProviderType == "Company")
                {
                    var company = await _context.Companies.FirstOrDefaultAsync(s => serviceProvider.Id == s.Id);
                    company!.Owner = model.Owner ?? company.Owner;
                    company!.Business = model.Business ?? company.Business;
                }
                else if (serviceProvider.ProviderType == "Marketplace")
                {
                    var marketPlace = await _context.MarketPlaces.FirstOrDefaultAsync(s => serviceProvider.Id == s.Id);
                    marketPlace!.Business = model.Business ?? marketPlace.Business;
                }
                else if (serviceProvider.ProviderType == "Engineer")
                {
                    var engineer = await _context.Engineers.FirstOrDefaultAsync(s => serviceProvider.Id == s.Id);
                    engineer!.Specialization = model.Specialization ?? engineer.Specialization;
                }
                else
                {
                    return BadRequest(new { message = "Put a correct ServiceProvider Name" });
                }

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
