

using System.Runtime.InteropServices;
using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
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
                {
                    return Unauthorized("User ID not found in token");
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found");
                }
                Console.WriteLine(userId);
                var ServiceProvider = await _context.ServiceProviders.FirstOrDefaultAsync(w => w.UserId == userId);
                Console.WriteLine(ServiceProvider != null);

                if (ServiceProvider!.ProviderType == "Worker")
                {

                    return Ok(await _context.ServiceProviders
              .FirstOrDefaultAsync(C => C.UserId == userId));
                }
                else if (ServiceProvider!.ProviderType == "Company")
                {

                    return Ok(await _context.Set<Company>()
                   .Include(C => new
                   {
                       C.Bio,
                       C.Location,
                       C.Business,
                       C.IsAvailable,
                       C.User.PhoneNumber,
                       C.User.UserName,
                       C.User.Email,
                       Subscription = new
                       {
                           EndDate = C.User.Subscription.EndDate
                       }
                   })
                   .FirstOrDefaultAsync(C => C.UserId == userId));
                }
                else if (ServiceProvider!.ProviderType == "Worker")
                {
                    return Ok(await _context.Set<Contractor>()
              .Include(C => new
              {
                  C.Bio,
                  C.Location,
                  C.Specialization,
                  C.IsAvailable,
                  C.User.PhoneNumber,
                  C.User.UserName,
                  C.User.Email,
                  Subscription = new
                  {
                      EndDate = C.User.Subscription.EndDate
                  }
              })
              .FirstOrDefaultAsync(C => C.UserId == userId));
                }
                else
                {
                    return StatusCode(500, new { message = "Something wrong with showing the profile" });
                }

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
                user.UserName = model.FullName ?? user.UserName;
                user.Email = model.Email ?? user.Email;
                var result = await _userManager.UpdateAsync(user);
                await _context.SaveChangesAsync();


                var serviceProvider = await _context.ServiceProviders
                .FirstOrDefaultAsync(sp => sp.UserId == userId);

                if (serviceProvider == null)
                {
                    return BadRequest("This User is not a serviceProvider");

                }

                serviceProvider.Location = model.Location ?? serviceProvider.Location;
                serviceProvider.IsAvailable = model.IsAvailable ?? serviceProvider.IsAvailable;
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


*/