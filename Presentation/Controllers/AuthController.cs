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
using Microsoft.AspNetCore.Identity.Data;
using System.Text.RegularExpressions;
using System.Transactions;
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

        private readonly UserImageService _userImageService;

        private readonly ApplicationDbContext _context;

        public AuthController(UserManager<User> userManager, UserRegisterationService userRegisterationService, IUserService service, IOTPService sms, ApplicationDbContext context, UserImageService userImageService)
        {
            _userRegisterationService = userRegisterationService;
            _userService = service;
            _smsOtpService = sms;
            _context = context;
            _userImageService = userImageService;
            _userManager = userManager;
        }
        [AllowAnonymous]
        [HttpPost("register")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Register([FromForm] RegisterWorkerDto model, [FromForm] IFormFile imageFile)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (imageFile == null)
                {
                    return BadRequest(new
                    {
                        message = "Please upload a profile image.",
                        errorCode = UserErrors.ImageIsNull.ToString()
                    });
                }
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (model.Pay < 100)
                {
                    return BadRequest(new
                    {
                        message = "The Pay/Service Price must be at least 100 EGP",
                        errorCode = UserErrors.InvalidPaymentValue.ToString()
                    });
                }
                UserRegisterationResult UserRegisterationResult = await _userRegisterationService.RegisterUser(model);
                if (UserRegisterationResult.Result != IdentityResult.Success)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new
                    {
                        message = UserRegisterationResult.Result.Errors.First().Description,
                        errorCode = UserRegisterationResult.Result.Errors.First().Code
                    });

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
                        WorkerType = (WorkerTypes)model.WorkerType,
                        Skill = model.Skill,
                        ProviderType = model.ProviderType,
                        ServicePricePerDay = model.Pay ?? 0,
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
                        Salary = model.Pay ?? 0,
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
                        Salary = model.Pay ?? 0,
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
                string? imageUrl = null;

                imageUrl = await _userImageService.UploadUserImageAsync(UserRegisterationResult.User, imageFile);
                if (imageUrl == null)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new
                    {
                        message = "Error uploading profile image",
                        errorCode = UserErrors.GeneralError.ToString()
                    });
                }

                // Transaction is done here
                await transaction.CommitAsync();

                // await Login(new LoginWorkerDto { Email = model.Email, Password = model.Password });
                return Ok(new { message = $"The Service Provider which is {model.ProviderType} is Created Successfully" });





            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = ex.Message });
            }
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginWorkerDto model)
        {
            try
            {



                var input = model.Email.Trim();
                User user = null;
                if (Helper.IsEmail(input))
                {

                    user = await _context.Users
                        .Include(u => u.Subscription)
                        .Include(u => u.ServiceProvider)
                        .FirstOrDefaultAsync(u => u.Email == input);
                }
                //Egyptain Server
                else if (Helper.IsPhone(input))
                {
                    Console.WriteLine("Reached here after phone check");

                    string phoneNumber = $"+20{input.Substring(1)}";
                    Console.WriteLine(phoneNumber);
                    user = await _context.Users
                        .Include(u => u.Subscription)
                        .Include(u => u.ServiceProvider)
                        .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
                }
                else
                {
                    return BadRequest(new { message = "Invalid email or phone format" });
                }
                // Check user existence and password
                Console.WriteLine(user.Id);
                if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                    return NotFound(new { message = "The Email/Phone or password is incorrect" });

                // Check subscription availability
                if (!user.ServiceProvider.IsAvailable)
                {
                    return Unauthorized(new
                    {
                        message = "Your subscription has expired",
                        LastDate = user.Subscription?.EndDate.ToString()
                    });
                }

                // Determine user role
                if (!Enum.TryParse<UsersTypes>(user.ServiceProvider.ProviderType, out UsersTypes userRole))
                {
                    return StatusCode(500, new { message = "Error while fetching the user role" });
                }

                // Generate tokens
                var accessToken = _userService.GenerateJwtToken(user, userRole, TokensTypes.AccessToken);
                var refreshTokenString = _userService.GenerateJwtToken(user, userRole, TokensTypes.RefreshToken);

                var refreshToken = new RefreshToken
                {
                    Token = refreshTokenString,
                    UserId = user.Id,
                    Expires = DateTime.UtcNow.AddDays(14),
                    Created = DateTime.UtcNow,
                    IsRevoked = false
                };

                _context.RefreshTokens.Add(refreshToken);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Login successful",
                    accessToken,
                    refreshToken = refreshTokenString
                });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            var user = await _userManager.GetUserAsync(User); // logged-in user
            Console.WriteLine(model.CurrentPassword);
            if (!await _userManager.CheckPasswordAsync(user, model.CurrentPassword))
                return BadRequest(new { message = "Current password is incorrect" });

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = "Password changed successfully" });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest refreshRequest)
        {
            if (refreshRequest == null || string.IsNullOrEmpty(refreshRequest.RefreshToken))
                return BadRequest("Refresh token is required");

            try
            {
                // Step 1: Find the refresh token in the database
                var storedToken = await _context.RefreshTokens
                    .Include(rt => rt.User) // Include user for generating new access token
                    .ThenInclude(u => u.ServiceProvider)
                    .FirstOrDefaultAsync(t => t.Token == refreshRequest.RefreshToken);

                if (storedToken == null)
                    return Unauthorized("Invalid refresh token");

                if (storedToken.IsRevoked || storedToken.Expires < DateTime.UtcNow)
                    return Unauthorized("Refresh token is expired or revoked");

                var user = storedToken.User;
                if (user == null)
                    return Unauthorized("User not found");

                if (!user.ServiceProvider.IsAvailable)
                {
                    return Unauthorized(new
                    {
                        message = "Your subscription has expired",
                        LastDate = user.Subscription?.EndDate.ToString()
                    });
                }

                // Optional: revoke the old refresh token to enforce single use
                storedToken.IsRevoked = true;
                storedToken.Revoked = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Step 2: Generate a new access token
                var userRoleClaim = user.UserName; // Replace with actual role claim
                var newAccessToken = _userService.GenerateJwtToken(
                    user,
                    UsersTypes.Company, // Example: replace with real enum from user's role
                    TokensTypes.AccessToken
                );

                // Optional: generate a new refresh token
                var newRefreshTokenString = _userService.GenerateJwtToken(
                    user,
                    UsersTypes.Company,
                    TokensTypes.RefreshToken
                );

                var newRefreshToken = new RefreshToken
                {
                    Token = newRefreshTokenString,
                    UserId = user.Id,
                    Expires = DateTime.UtcNow.AddDays(14),
                    Created = DateTime.UtcNow,
                    IsRevoked = false
                };
                _context.RefreshTokens.Add(newRefreshToken);
                await _context.SaveChangesAsync();

                // Step 3: Return new tokens to client
                return Ok(new
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshTokenString,
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
                return StatusCode(500, new
                {
                    message = "Error processing refresh token",
                    errorMessage = ex.Message
                });
            }
        }
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest refreshRequest)
        {
            if (refreshRequest == null || string.IsNullOrEmpty(refreshRequest.RefreshToken))
                return BadRequest("Refresh token is required");

            try
            {
                // Step 1: Find the refresh token in the database
                var storedToken = await _context.RefreshTokens
                    .FirstOrDefaultAsync(t => t.Token == refreshRequest.RefreshToken);

                if (storedToken == null)
                    return NotFound("Refresh token not found");

                // Step 2: Revoke the token
                storedToken.IsRevoked = true;
                storedToken.Revoked = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Step 3: Optionally, you can also clear other active tokens for this user
                // var userTokens = _context.RefreshTokens.Where(t => t.UserId == storedToken.UserId && !t.IsRevoked);
                // foreach(var token in userTokens) { token.IsRevoked = true; token.Revoked = DateTime.UtcNow; }
                // await _context.SaveChangesAsync();

                return Ok(new { message = "Logout successful, refresh token revoked" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error logging out",
                    errorMessage = ex.Message
                });
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
                // Get authenticated user ID
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null) return NotFound();

                try
                {
                    var imageUrl = await _userImageService.UploadUserImageAsync(user, file);
                    return Ok(new { Message = "Profile image uploaded successfully", ImageUrl = imageUrl });
                }
                catch (Exception ex)
                {
                    return BadRequest(new { Message = ex.Message });
                }

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error: " + ex.Message });
            }
        }
    }
}


