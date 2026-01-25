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
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore.SqlServer.Migrations.Internal;
using System.Collections.Concurrent;
using EgyptOnline.Strategies;
using Microsoft.AspNetCore.Mvc.Versioning;
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

        private readonly ProviderRegistrationStrategyFactory _strategyFactory;



        public AuthController(UserManager<User> userManager, UserRegisterationService userRegisterationService, IUserService service, IOTPService sms, ApplicationDbContext context, UserImageService userImageService)
        {
            _userRegisterationService = userRegisterationService;
            _userService = service;
            _smsOtpService = sms;
            _context = context;
            _userImageService = userImageService;
            _userManager = userManager;
            _strategyFactory = new ProviderRegistrationStrategyFactory();
        }
        [AllowAnonymous]
        [HttpPost("register")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Register([FromForm] RegisterWorkerDto model, [FromForm] IFormFile imageFile)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var phoneRegex = new Regex(@"^(010|011|012|015)\d{8}$");

                if (!phoneRegex.IsMatch(model.PhoneNumber))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Validation failed",
                        errorCode = "InvalidInput",
                        errors = new
                        {
                            PhoneNumber = "Phone number must start with 010, 011, 012, or 015 and be 11 digits long"
                        }
                    });
                }
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
                    // Collect all validation errors
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        );

                    return BadRequest(new
                    {
                        success = false,
                        message = "Validation failed",
                        errorCode = "InvalidInput"
                    });
                }

                if (model.Pay < 100 &&
                 !model.ProviderType!.Equals("marketplace", StringComparison.CurrentCultureIgnoreCase) &&
                 !model.ProviderType.Equals("company", StringComparison.CurrentCultureIgnoreCase)
                 )
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

                // Get the appropriate strategy for this provider type
                var strategy = _strategyFactory.GetStrategy(model.ProviderType);
                if (strategy == null)
                {
                    return BadRequest(new { message = "Please Provide the Type Of Service" });
                }

                // Validate provider-specific requirements
                var validationError = strategy.Validate(model);
                if (validationError != null)
                {
                    return BadRequest(new { message = validationError });
                }

                // Create the appropriate provider using the strategy
                var provider = strategy.CreateProvider(model, UserRegisterationResult.User);
                _context.Add(provider);


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
                return Ok(new
                {
                    message = $"The Service Provider which is {model.ProviderType} is Created Successfully",
                    expiryDate = UserRegisterationResult.User.Subscription!.EndDate
                });





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
                User user;
                if (Helper.IsEmail(input))
                {

                    user = await _context.Users
                        .Include(u => u.Subscription)
                        .Include(u => u.ServiceProvider)
                        .FirstOrDefaultAsync(u => u.Email == input);
                    if (user == null)
                    {
                        return BadRequest(new { message = "This User is not found", errorCode = UserErrors.UserIsNotFound.ToString() });
                    }
                }
                //Egyptain Server
                else if (Helper.IsPhone(input))
                {

                    string phoneNumber = $"+20{input.Substring(1)}";
                    user = await _context.Users
                        .Include(u => u.Subscription)
                        .Include(u => u.ServiceProvider)
                        .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
                    if (user == null)
                    {
                        return BadRequest(new { message = "This User is not found", errorCode = UserErrors.UserIsNotFound.ToString() });

                    }
                }

                else
                {
                    return BadRequest(new { message = "Invalid email or phone format" });
                }
                // Check user existence and password
                Console.WriteLine(user.Id);
                if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                    return NotFound(new { message = "The Email/Phone or password is incorrect", errorCode = UserErrors.EmailOrPasswordInCorrect.ToString() });
                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Contains(Roles.Admin))
                {
                    return BadRequest(new
                    {
                        message = "You are an admin, can't access the app",
                        errorCode = UserErrors.GeneralError.ToString()
                    });
                }


                // Determine user role
                if (!Enum.TryParse<UsersTypes>(user.ServiceProvider.ProviderType, out UsersTypes userRole))
                {
                    return StatusCode(500, new { message = "Error while fetching the user role" });
                }

                // Generate tokens
                var accessToken = await _userService.GenerateJwtToken(user, userRole, TokensTypes.AccessToken);
                var refreshTokenString = await _userService.GenerateJwtToken(user, userRole, TokensTypes.RefreshToken);

                var refreshToken = new RefreshToken
                {
                    Token = refreshTokenString,
                    UserId = user.Id,
                    Expires = DateTime.UtcNow.AddDays(TokenPeriod.REFRESH_TOKEN_DAYS),
                    Created = DateTime.UtcNow,
                    IsRevoked = false
                };
                _context.RefreshTokens.Add(refreshToken);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Login successful",
                    accessToken,
                    isExpired = !(user!.ServiceProvider.IsAvailable),
                    refreshToken = refreshTokenString,
                    subscriptionExpiry = user.Subscription!.EndDate,
                    refreshTokenExpiry = DateTime.UtcNow.AddDays(TokenPeriod.REFRESH_TOKEN_DAYS)
                });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // This password is for logged in user
        [HttpPost("change-password")]
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            var user = await _userManager.GetUserAsync(User); // logged-in user
            if (user == null)
            {
                return BadRequest(new { message = "User is not found" });
            }
            if (!await _userManager.CheckPasswordAsync(user, model.CurrentPassword))
                return BadRequest(new { message = "Current password is incorrect" });

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = "Password changed successfully" });
        }
        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest refreshRequest)
        {
            if (refreshRequest == null || string.IsNullOrEmpty(refreshRequest.RefreshToken))
                return BadRequest("Refresh token is required");

            try
            {
                // 1. Validate the token itself
                var principal = _userService.ValidateRefreshToken(refreshRequest.RefreshToken);
                if (principal == null)
                    return Unauthorized(new { message = "Invalid refresh token", errorCode = "InvalidToken" });

                var tokenType = principal.Claims.FirstOrDefault(c => c.Type == "token_type")?.Value;
                if (tokenType != TokensTypes.RefreshToken.ToString())
                    return Unauthorized(new { message = "Token is not a refresh token", errorCode = "InvalidToken" });

                // 2. Find the stored token
                var storedToken = await _context.RefreshTokens
                    .Include(rt => rt.User)
                        .ThenInclude(u => u.ServiceProvider)
                    .Include(rt => rt.User.Subscription)
                    .FirstOrDefaultAsync(t => t.Token == refreshRequest.RefreshToken);

                if (storedToken == null || storedToken.IsRevoked || storedToken.Expires < DateTime.UtcNow)
                    return Unauthorized(new
                    {
                        message = "Refresh token is expired or revoked",
                        errorCode = UserErrors.RefreshTokenInvalid.ToString()
                    });

                var user = storedToken.User;
                if (user == null)
                    return Unauthorized("User not found");

                // Allow refresh even if subscription expired - user can still access app but with limited functionality
                // The new token will reflect current subscription status
                // Critical operations will check DB via RequireSubscription attribute

                // 3. Revoke **all previous valid tokens** to prevent replay/race attacks
                var oldTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == user.Id && !rt.IsRevoked && rt.Expires > DateTime.UtcNow)
                    .ToListAsync();

                foreach (var oldToken in oldTokens)
                {
                    oldToken.IsRevoked = true;
                    oldToken.Revoked = DateTime.UtcNow;
                }

                // 4. Generate new tokens
                var newAccessToken = await _userService.GenerateJwtToken(user, Helper.GetUserType(user), TokensTypes.AccessToken);
                var newRefreshTokenString = await _userService.GenerateJwtToken(user, Helper.GetUserType(user), TokensTypes.RefreshToken);

                var newRefreshToken = new RefreshToken
                {
                    Token = newRefreshTokenString,
                    UserId = user.Id,
                    Expires = DateTime.UtcNow.AddDays(TokenPeriod.REFRESH_TOKEN_DAYS),
                    Created = DateTime.UtcNow,
                    IsRevoked = false
                };

                _context.RefreshTokens.Add(newRefreshToken);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshTokenString,
                    refreshTokenExpiry = newRefreshToken.Expires,
                    subscriptionExpiry = user.Subscription?.EndDate,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in refresh: {ex.Message}");
                return StatusCode(500, new { message = "Error processing refresh token", errorMessage = ex.Message });
            }
        }

        [AllowAnonymous]

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
                    return NotFound(new { message = "Refresh token not found" });

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


        [Authorize(Roles = Roles.User)]
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


