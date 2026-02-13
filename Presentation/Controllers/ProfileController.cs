

using System.Runtime.InteropServices;
using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using EgyptOnline.Domain.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Controllers
{

    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize(Roles = Roles.User)]

    public class ProfileController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly OccupationService _occupationService;

        public ProfileController(UserManager<User> userManager, IUserService userService, ApplicationDbContext context, OccupationService occupationService)
        {
            _userService = userService;
            _userManager = userManager;
            _context = context;
            _occupationService = occupationService;
        }
        // Get the profile of the worker
        // Non-critical: Allow viewing profile even if expired (uses token claim, no DB hit for subscription check)
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

                // Check subscription from token (may be stale, but acceptable for viewing own profile)
                // No DB hit needed - subscription status available in token claim
                // Client can check subscription_expires claim from token if needed

                return Ok(user.ToShowProfileDto());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("subscription-status")]
        public async Task<IActionResult> GetSubscriptionStatus()
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                var user = await _context.Users.Include(u => u.Subscription).Select(u => new
                {
                    u.Subscription,
                    u.Id
                }).FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                    return NotFound();

                return Ok(user.Subscription);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        //Update the location and availability and skills of the worker
        // Critical operation: Requires active subscription (checks DB for fresh data)
        [HttpPut]
        [RequireSubscription]
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

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Subscription check is handled by [RequireSubscription] attribute
                using var transaction = await _context.Database.BeginTransactionAsync();




                /* End of Authentcation the User is added*/


                /* Update User Data */

                user.FirstName = model.FirstName ?? user.FirstName;
                user.LastName = model.LastName ?? user.LastName;
                user.Governorate = model.Governorate ?? user.Governorate;
                user.City = model.City ?? user.City;
                user.District = model.District ?? user.District;

                // The UserName of the user should be consistent even after we change the first name and the last name



                /* Update the Service Provider Data */




                if (user == null || user.ServiceProvider == null)
                {
                    return NotFound(new { message = "The Service Provider related to this user is not found" });
                }


                user.ServiceProvider.Bio = model.Bio ?? user.ServiceProvider.Bio;


                if (user.ServiceProvider.ProviderType == "Worker")
                {
                    var worker = await _context.Workers.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                    worker.ServicePricePerDay = model.Pay;
                    worker.MarketPlace = model.Marketplace;
                    worker.DerivedSpec = model.DerivedSpec;

                }
                else if (user.ServiceProvider.ProviderType == "Contractor")
                {
                    var contractor = await _context.Contractors.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                    contractor.Salary = model.Pay;

                }
                else if (user.ServiceProvider.ProviderType == "Company")
                {
                    var company = await _context.Companies.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                }
                else if (user.ServiceProvider.ProviderType == "Marketplace")
                {
                    var marketPlace = await _context.MarketPlaces.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);

                }
                else if (user.ServiceProvider.ProviderType == "Engineer")
                {
                    var engineer = await _context.Engineers.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                    engineer!.Salary = model.Pay;
                }
                else if (user.ServiceProvider.ProviderType == "Assistant")
                {
                    var assistant = await _context.Assistants.FirstOrDefaultAsync(s => user.ServiceProvider.Id == s.Id);
                    assistant!.ServicePricePerDay = model.Pay;
                    assistant.MarketPlace = model.Marketplace;
                    assistant.DerivedSpec = model.DerivedSpec;

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

        /// <summary>
        /// Mark the authenticated user as occupied until midnight
        /// </summary>
        [HttpPost("set-occupied")]
        [RequireSubscription]
        public async Task<IActionResult> SetOccupied()
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                    return Unauthorized();

                var expirationTime = await _occupationService.SetUserOccupiedAsync(userId);

                return Ok(new 
                { 
                    message = "You have been marked as occupied until midnight",
                    expiresAt = expirationTime,
                    isOccupied = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove occupation status for the authenticated user
        /// </summary>
        [HttpDelete("remove-occupied")]
        [RequireSubscription]
        public async Task<IActionResult> RemoveOccupied()
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                    return Unauthorized();

                await _occupationService.RemoveUserOccupiedAsync(userId);

                return Ok(new 
                { 
                    message = "You have been marked as available",
                    isOccupied = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current occupation status of the authenticated user
        /// </summary>
        [HttpGet("occupation-status")]
        public async Task<IActionResult> GetOccupationStatus()
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                    return Unauthorized();

                var isOccupied = await _occupationService.IsUserOccupiedAsync(userId);

                return Ok(new { isOccupied });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

}
