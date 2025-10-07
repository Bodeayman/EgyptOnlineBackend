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
    [Route("api/[controller]")]
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

                UserRegisterationResult UserRegisterationResult = await _userRegisterationService.RegisterUser(model);
                if (UserRegisterationResult.Result != IdentityResult.Success)
                {
                    return StatusCode(500, new { message = UserRegisterationResult.Result });

                }
                if (model.UserType == "User")
                {
                    /* This save operation will work here when the registerd one is a user , 
                    so no need to check if the service provider is null or not */
                    await _context.SaveChangesAsync();
                    return StatusCode(201, new { message = "You registered successfully!" });

                }
                else if (model.UserType == "SP")
                {
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
                    return Ok(new { message = $"The Service Provider which is {model.ProviderType} is Created Successfully" });


                }
                else
                {
                    return BadRequest(new { message = "Please Provide a valid UserType" });
                }




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

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                {
                    return NotFound("This User hasn't been here before");
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

            Console.WriteLine(phoneNumber);
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            if (user == null) return NotFound("User not found");
            await _smsOtpService.SendOtpAsync(phoneNumber, false);
            // SendOtpAsync()


            return Ok("OTP sent");
        }
        [Authorize]
        [HttpPost("upload-profile-image")]
        public async Task<IActionResult> UploadProfileImage(IFormFile file)
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;

                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded.");

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var fileBytes = ms.ToArray();

                var url = await _cdnService.UploadImageAsync(fileBytes, file.FileName, "/user-uploads");
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                user.ImageUrl = url;

                await _context.SaveChangesAsync();
                return Ok(new { Url = url });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error {ex.Message}");
            }


        }




    }
}

/*
{
  "fullName": "Ayman",
  "email": "ayman@gmail.com",
  "phoneNumber": "01143512531",
  "password": "Bode@999Bode",
  "userType": "SP",
  "location": "string",
  "bio": "string",
  "skill": "string",
  "specialization": "string",
  "business": "string",
  "providerType": "string"
}
*/