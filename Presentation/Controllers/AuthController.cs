using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EgyptOnline.Utilities;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Services;
using EgyptOnline.Data;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace EgyptOnline.Controllers
{

    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserManager<User> _userManager;

        private readonly UserRegisterationService _userRegisterationService;
        private readonly IOTPService _smsOtpService;

        private readonly ICDNService _cdnService;

        private readonly ApplicationDbContext _context;

        public AuthController(UserManager<User> userManager, UserRegisterationService userRegisterationService, IUserService service, IOTPService sms, ApplicationDbContext context, ICDNService CDNService)
        {
            _userRegisterationService = userRegisterationService;
            _userService = service;
            _smsOtpService = sms;
            _context = context;
            _cdnService = CDNService;
            _userManager = userManager;
        }
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterWorkerDto model)
        {
            try
            {

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                using var transaction = await _context.Database.BeginTransactionAsync();
                UserRegisterationResult UserRegisterationResult = await _userRegisterationService.RegisterUser(model);
                if (UserRegisterationResult.Result != IdentityResult.Success)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new { message = UserRegisterationResult.Result });

                }

                if (model.ProviderType == null)
                {
                    return BadRequest(new { message = "Please Provide the Type Of Service" });
                }
                Console.WriteLine("Service Provider is being created right now , but not yet");


                if (model.ProviderType.Equals("worker", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (model.Skill == null)
                    {
                        return BadRequest(new { message = "Please Add the Skill" });
                    }

                    var Worker = new Worker
                    {
                        User = UserRegisterationResult.User,
                        UserId = UserRegisterationResult.User!.Id,
                        Bio = model.Bio,
                        WorkerType = model.WorkerType,
                        Skill = model.Skill,
                        ProviderType = model.ProviderType,
                        ServicePricePerDay = model.Pay,
                        IsAvailable = true,
                    };

                    _context.Workers.Add(Worker);
                }
                else if (model.ProviderType.Equals("contractor", StringComparison.CurrentCultureIgnoreCase))
                {

                    if (model.Specialization == null)
                    {
                        return BadRequest(new { message = "Please Add the Specialization" });
                    }
                    var Contractor = new Contractor
                    {
                        User = UserRegisterationResult.User,
                        UserId = UserRegisterationResult.User!.Id,
                        Bio = model.Bio,
                        Specialization = model.Specialization,
                        ProviderType = model.ProviderType,
                        IsAvailable = true,
                    };
                    _context.Contractors.Add(Contractor);
                }
                else if (model.ProviderType.Equals("company", StringComparison.CurrentCultureIgnoreCase))
                {

                    if (model.Business == null)
                    {
                        return BadRequest(new { message = "Please Add the Business" });
                    }
                    var Company = new Company
                    {
                        User = UserRegisterationResult.User,
                        UserId = UserRegisterationResult.User!.Id,
                        Bio = model.Bio,
                        ProviderType = model.ProviderType,
                        Owner = model.Owner,
                        Business = model.Business,
                        IsAvailable = true,
                    };
                    _context.Companies.Add(Company);
                }
                else if (model.ProviderType.Equals("marketplace", StringComparison.CurrentCultureIgnoreCase))
                {

                    if (model.Business == null)
                    {
                        return BadRequest(new { message = "Please Add the Business" });
                    }
                    var MarketPlace = new MarketPlace
                    {
                        User = UserRegisterationResult.User,
                        UserId = UserRegisterationResult.User!.Id,
                        Bio = model.Bio,
                        ProviderType = model.ProviderType,
                        Business = model.Business,
                        IsAvailable = true,
                        Owner = model.Owner,
                    };
                    _context.MarketPlaces.Add(MarketPlace);
                }
                else if (model.ProviderType.Equals("engineer", StringComparison.CurrentCultureIgnoreCase))
                {

                    if (model.Specialization == null)
                    {
                        return BadRequest(new { message = "Please Add the Specialization" });
                    }
                    var Engineer = new Engineer
                    {
                        User = UserRegisterationResult.User,
                        UserId = UserRegisterationResult.User!.Id,
                        Bio = model.Bio,
                        ProviderType = model.ProviderType,
                        Salary = model.Pay,
                        Specialization = model.Specialization,
                        IsAvailable = true,
                    };
                    _context.Engineers.Add(Engineer);
                }
                else
                {
                    return BadRequest(new { message = "Please Provide the Type Of Service" });
                }
                await _context.SaveChangesAsync();


                // Transaction is done here
                await transaction.CommitAsync();
                return Ok(new { message = $"The Service Provider which is {model.ProviderType} is Created Successfully" });





            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginWorkerDto model)
        {
            try
            {


                var user = await _context.Users
                .Include(u => u.Subscription)
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Email == model.Email);
                if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                {
                    return NotFound(new { message = "The Email or the password is not found" });
                }
                // Including it in every function that related to that controller
                if (!user.ServiceProvider.IsAvailable)
                {
                    return Unauthorized(new
                    {
                        message = "Your subscription has expired",
                        LastDate = user.Subscription.EndDate.ToString()
                    });
                }
                var AllUserDetails = await _context.Users.Include(u => u.ServiceProvider)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == user.Id);
                Console.WriteLine(AllUserDetails.UserType.ToString());
                UsersTypes UserRole;

                if (AllUserDetails.UserType == "User")
                {
                    UserRole = UsersTypes.User;
                }
                else
                {
                    Console.WriteLine(AllUserDetails.ServiceProvider.ProviderType);
                    if (AllUserDetails.ServiceProvider.ProviderType == "Worker")
                    {
                        UserRole = UsersTypes.Worker;
                    }
                    else if (AllUserDetails.ServiceProvider.ProviderType == "Company")
                    {
                        UserRole = UsersTypes.Company;
                    }
                    else if (AllUserDetails.ServiceProvider.ProviderType == "Contractor")
                    {
                        UserRole = UsersTypes.Contractor;
                    }
                    else if (AllUserDetails.ServiceProvider.ProviderType == "Marketplace")
                    {
                        UserRole = UsersTypes.Marketplace;
                    }
                    else if (AllUserDetails.ServiceProvider.ProviderType == "Engineer")
                    {
                        UserRole = UsersTypes.Engineer;
                    }
                    else
                    {
                        return StatusCode(500, new { message = "Error while Fetching the User" });
                    }
                }
                Console.WriteLine(UserRole.ToString());
                var accessToken = _userService.GenerateJwtToken(user, UserRole, TokensTypes.AccessToken);
                var refreshToken = _userService.GenerateJwtToken(user, UserRole, TokensTypes.RefreshToken);

                // var refreshToken = _userService.GenerateRefreshToken(user);

                return Ok(new
                {
                    message = "Login successful",
                    accessToken = accessToken,
                    refreshToken = refreshToken
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] string refreshToken)
        {
            try
            {


                ClaimsPrincipal principal = _userService.ValidateRefreshToken(refreshToken);

                foreach (var c in principal.Claims)
                {
                    Console.WriteLine($"{c.Type} = {c.Value}");
                }
                var tokenType = principal.FindFirst("token_type")?.Value;
                if (tokenType != null && tokenType != TokensTypes.RefreshToken.ToString())
                    return Unauthorized("Invalid token type");

                // Extract claims safely
                var userId = principal.FindFirst("uid")?.Value;
                var typeClaim = principal.FindFirst(ClaimTypes.Role)?.Value;
                Console.WriteLine(userId);
                Console.WriteLine("Type");

                Console.WriteLine(typeClaim);
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(typeClaim))
                    return Unauthorized("Invalid token claims");

                // Find user asynchronously
                var user = await _userManager.FindByIdAsync(userId);

                if (user == null)
                    return Unauthorized("User not found");



                if (!user.ServiceProvider.IsAvailable)
                {
                    return Unauthorized(new
                    {
                        message = "Your subscription has expired",
                        LastDate = user.Subscription.EndDate.ToString()
                    });
                }
                // Parse role using enum directly (cleaner)
                if (!Enum.TryParse<UsersTypes>(typeClaim, out UsersTypes userRole))
                    return StatusCode(500, new { message = "Invalid user role" });

                // Generate new access token
                var newAccessToken = _userService.GenerateJwtToken(user, userRole, TokensTypes.AccessToken);

                return Ok(new
                {
                    AccessToken = newAccessToken,
                    ExpiresIn = TimeSpan.FromMinutes(30).TotalSeconds
                });
            }
            catch (SecurityTokenExpiredException)
            {
                return Unauthorized("Refresh token expired");
            }
            catch (SecurityTokenException)
            {
                return Unauthorized("Invalid refresh token");
            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, new { message = "Error processing refresh token" });
            }
        }
        [HttpPost("request-otp")]
        public async Task<IActionResult> RequestOtp([FromBody] string phoneNumber)
        {
            try
            {

                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

                if (user == null) return NotFound("User not found");
                await _smsOtpService.SendOtpAsync(phoneNumber, false);
                // SendOtpAsync()


                return Ok("OTP sent");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error: " + ex.Message });
            }
        }
        [Authorize]
        [HttpPost("upload-profile-image")]
        public async Task<IActionResult> UploadProfileImage(IFormFile file)
        {
            try
            {


                // I need to get the User itself here , but it's not found , i don't know the serverity
                // Get user ID from claims
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { Message = "User not authenticated" });
                }

                // Validate file
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { Message = "No file uploaded" });
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest(new { Message = "Invalid file type. Allowed: jpg, jpeg, png, gif, webp" });
                }

                // Validate file size (5MB max)
                if (file.Length > 5 * 1024 * 1024)
                {
                    return BadRequest(new { Message = "File too large. Maximum size: 5MB" });
                }

                // Find user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Generate unique filename
                var uniqueFileName = $"{userId}_{Guid.NewGuid()}{extension}";

                // Convert file to bytes
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var fileBytes = ms.ToArray();

                // Delete old image if exists
                if (!string.IsNullOrEmpty(user.ImageUrl))
                {
                    try
                    {
                        await _cdnService.DeleteImageAsync(user.ImageUrl);
                    }
                    catch (Exception ex)
                    {
                    }
                }

                // Upload new image
                var imageUrl = await _cdnService.UploadImageAsync(fileBytes, uniqueFileName, "profiles");

                // Update user record
                user.ImageUrl = imageUrl;
                await _context.SaveChangesAsync();


                return Ok(new
                {
                    Message = "Profile image uploaded successfully",
                    Url = imageUrl,
                    FileName = uniqueFileName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error: " + ex.Message });

            }
        }



    }
}

