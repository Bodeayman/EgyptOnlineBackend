

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
                    return Unauthorized("User ID not found in token");
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found");
                }
                //So we then have a user
                //Update
                user.UserName = newUserName ?? user.UserName;
                var result = await _userManager.UpdateAsync(user);
                await _context.SaveChangesAsync();


                var serviceProvider = await _context.ServiceProviders
                .FirstOrDefaultAsync(sp => sp.UserId == userId);

                if (serviceProvider == null)
                {
                    return BadRequest("This User is not a serviceProvider");

                }

                serviceProvider.Location = model.Location ?? serviceProvider.Location;

                serviceProvider.Bio = model.Bio ?? serviceProvider.Bio;

                if (serviceProvider.ProviderType == "Worker")
                {
                    var worker = await _context.Workers.FirstOrDefaultAsync(s => serviceProvider.Id == s.Id);

                    worker!.Skill = model.Skill;
                }
                if (serviceProvider.ProviderType == "Contractor")
                {
                    var contractor = await _context.Contractors.FirstOrDefaultAsync(s => serviceProvider.Id == s.Id);

                    contractor!.Specialization = model.Specialization ?? contractor.Specialization;
                }
                if (serviceProvider.ProviderType == "Company")
                {
                    var company = await _context.Companies.FirstOrDefaultAsync(s => serviceProvider.Id == s.Id);

                    company!.Business = model.Business ?? company.Business;
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
/*
👉 So in your situation:

No active subscription AND not a service provider → show ads.

Otherwise → no ads.
{
  "fullName": "aymoon",
  

  "skill": "Cooking",
  "specialization": "string",
  "business": "string",
  "providerType": "Worker"
}

*/