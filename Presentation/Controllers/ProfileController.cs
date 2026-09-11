

using System.Runtime.InteropServices;
using EgyptOnline.Application.Services.Search;
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
        private readonly SearchService _searchService;

        public ProfileController(UserManager<User> userManager, IUserService userService, ApplicationDbContext context, OccupationService occupationService, SearchService searchService)
        {
            _userService = userService;
            _userManager = userManager;
            _context = context;
            _occupationService = occupationService;
            _searchService = searchService;
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
                    return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });

                var user = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return NotFound(new { message = "المستخدم غير موجود", errorCode = "USER_NOT_FOUND" });

                return Ok(user.ToShowProfileDto());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        // Get the public profile of any user by id.
        // Only explicitly public fields are exposed; the response is projected
        // into SearchV2ResultDto and never serializes the raw User entity.
        // Route uses a GUID constraint so literal action names like
        // "subscription-status" never collide and invalid GUIDs are rejected.
        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> GetUserProfile(Guid userId)
        {
            try
            {
                var profile = await _searchService.GetUserPublicProfileAsync(userId.ToString());
                if (profile == null)
                    return NotFound(new { message = "المستخدم غير موجود", errorCode = "USER_NOT_FOUND" });

                return Ok(profile);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
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
                    return NotFound(new { message = "المستخدم غير موجود", errorCode = "USER_NOT_FOUND" });

                return Ok(user.Subscription);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }
        //Update the location and availability and skills of the worker
        // SUBSCRIPTION VALIDATION DISABLED — re-enable by uncommenting [RequireSubscription]
        [HttpPut]
        // [RequireSubscription]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileDto model)
        {
            try
            {
                /* Authentication Stage Check*/
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                    return BadRequest(new { message = "فشل التحقق من صحة البيانات", errorCode = "INVALID_INPUT", errors });
                }
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                {
                    return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });
                }
                var user = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return NotFound(new { message = "المستخدم غير موجود", errorCode = "USER_NOT_FOUND" });
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
                    return NotFound(new { message = "لم يتم العثور على مقدم الخدمة المرتبط بهذا المستخدم", errorCode = "PROVIDER_NOT_FOUND" });
                }

                // Update Bio safely
                if (!string.IsNullOrWhiteSpace(model.Bio))
                {
                    user.ServiceProvider.Bio = model.Bio;
                }

                switch (user.ServiceProvider.ProviderType)
                {
                    case "Worker":
                        var worker = await _context.Workers.FirstOrDefaultAsync(s => s.Id == user.ServiceProvider.Id);
                        if (worker == null) return BadRequest(new { message = "لم يتم العثور على بيانات العامل", errorCode = "PROVIDER_NOT_FOUND" });

                        if (model.Pay >= 0)
                            worker.ServicePricePerDay = model.Pay;

                        if (!string.IsNullOrWhiteSpace(model.Marketplace))
                            worker.MarketPlace = model.Marketplace;

                        worker.DerivedSpec = string.IsNullOrWhiteSpace(model.DerivedSpec) ? worker.DerivedSpec : model.DerivedSpec;
                        break;

                    case "Contractor":
                        var contractor = await _context.Contractors.FirstOrDefaultAsync(s => s.Id == user.ServiceProvider.Id);
                        if (contractor == null) return BadRequest(new { message = "لم يتم العثور على بيانات المقاول", errorCode = "PROVIDER_NOT_FOUND" });

                        break;

                    case "Company":
                        var company = await _context.Companies.FirstOrDefaultAsync(s => s.Id == user.ServiceProvider.Id);
                        if (company == null) return BadRequest(new { message = "لم يتم العثور على بيانات الشركة", errorCode = "PROVIDER_NOT_FOUND" });
                        break;

                    case "Marketplace":
                        var marketPlace = await _context.MarketPlaces.FirstOrDefaultAsync(s => s.Id == user.ServiceProvider.Id);
                        if (marketPlace == null) return BadRequest(new { message = "لم يتم العثور على بيانات المعرض", errorCode = "PROVIDER_NOT_FOUND" });
                        break;

                    case "Engineer":
                        var engineer = await _context.Engineers.FirstOrDefaultAsync(s => s.Id == user.ServiceProvider.Id);
                        if (engineer == null) return BadRequest(new { message = "لم يتم العثور على بيانات المهندس", errorCode = "PROVIDER_NOT_FOUND" });


                        // Ensure DerivedSpec is NOT NULL
                        engineer.DerivedSpec = string.IsNullOrWhiteSpace(model.DerivedSpec) ? engineer.DerivedSpec : model.DerivedSpec;
                        break;

                    case "Assistant":
                        var assistant = await _context.Assistants.FirstOrDefaultAsync(s => s.Id == user.ServiceProvider.Id);
                        if (assistant == null) return BadRequest(new { message = "لم يتم العثور على بيانات المساعد", errorCode = "PROVIDER_NOT_FOUND" });

                        if (model.Pay >= 0)
                            assistant.ServicePricePerDay = model.Pay;

                        if (!string.IsNullOrWhiteSpace(model.Marketplace))
                            assistant.MarketPlace = model.Marketplace;

                        assistant.DerivedSpec = string.IsNullOrWhiteSpace(model.DerivedSpec) ? assistant.DerivedSpec : model.DerivedSpec;
                        break;

                    case "Sculptor":
                        var sculptor = await _context.Sculptors.FirstOrDefaultAsync(s => s.Id == user.ServiceProvider.Id);
                        if (sculptor == null) return BadRequest(new { message = "لم يتم العثور على بيانات النحات", errorCode = "PROVIDER_NOT_FOUND" });

                        if (model.Pay >= 0)
                            sculptor.ServicePricePerDay = model.Pay;

                        if (!string.IsNullOrWhiteSpace(model.Marketplace))
                            sculptor.MarketPlace = model.Marketplace;
                        break;

                    default:
                        return BadRequest(new { message = "نوع مقدم الخدمة غير صحيح", errorCode = "INVALID_PROVIDER_TYPE" });
                }

                // Save changes safely


                await transaction.CommitAsync();
                await _context.SaveChangesAsync();

                return Ok(new { message = "تم تحديث ملفك الشخصي بنجاح" });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        /// <summary>
        /// Mark the authenticated user as occupied until midnight
        /// </summary>
        [HttpPost("set-occupied")]
        // [RequireSubscription]
        public async Task<IActionResult> SetOccupied()
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                    return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });

                var expirationTime = await _occupationService.SetUserOccupiedAsync(userId);

                return Ok(new
                {
                    message = "تم تحديد حالتك كمشغول حتى منتصف الليل",
                    expiresAt = expirationTime,
                    isOccupied = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        /// <summary>
        /// Remove occupation status for the authenticated user
        /// </summary>
        [HttpDelete("remove-occupied")]
        // [RequireSubscription]
        public async Task<IActionResult> RemoveOccupied()
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                    return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });

                await _occupationService.RemoveUserOccupiedAsync(userId);

                return Ok(new
                {
                    message = "تم تحديد حالتك كمتاح",
                    isOccupied = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
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
                    return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });

                var isOccupied = await _occupationService.IsUserOccupiedAsync(userId);

                return Ok(new { isOccupied });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }
    }

}
