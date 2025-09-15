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
namespace EgyptOnline.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserManager<User> _userManager;
        private readonly IOTPService _smsOtpService;

        private readonly ApplicationDbContext _context;

        public AuthController(UserManager<User> userManager, IUserService service, IOTPService sms, ApplicationDbContext context)
        {
            _userService = service;
            _userManager = userManager;
            _smsOtpService = sms;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterWorkerDto model)
        {
            try
            {

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = new
                User
                { UserName = model.FullName, Email = model.Email, PhoneNumber = model.PhoneNumber };
                if (await _userManager.FindByEmailAsync(model.Email) != null)
                {
                    return BadRequest(new { message = "The User is Registered Before" });
                }
                var result = await _userManager.CreateAsync(user, model.Password);

                if (model.UserType == "User")
                {
                    if (result.Succeeded)
                    {
                        return Ok(new { message = "You registered successfully!" });
                    }
                    else
                    {
                        foreach (var res in result.Errors)
                        {
                            Console.WriteLine(res.Description);
                        }
                        return StatusCode(500, new { message = "Something happened with saving the User" });

                    }
                }
                if (model.UserType == "SP")
                {
                    Console.WriteLine(model.ProviderType);
                    Console.WriteLine(model.ProviderType.Equals("worker", StringComparison.CurrentCultureIgnoreCase));
                    if (model.ProviderType.Equals("worker", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (model.Skill == null)
                        {
                            return BadRequest(new { message = "Please Add the Skill" });
                        }
                        var Worker = new Worker
                        {
                            User = user,
                            UserId = user.Id,
                            Bio = model.Bio,
                            Location = model.Location,
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
                            User = user,
                            UserId = user.Id,
                            Bio = model.Bio,
                            Location = model.Location,
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
                            User = user,
                            UserId = user.Id,
                            Bio = model.Bio,
                            Location = model.Location,
                            ProviderType = model.ProviderType,

                            Business = model.Business,
                            IsAvailable = true,
                        };
                        _context.Companies.Add(Company);
                    }
                    else
                    {
                        return BadRequest(new { message = "Please Provide the Type Of Service" });
                    }
                }

                await _context.SaveChangesAsync();


                return Ok(new { message = $"The Service Provider which is {model.ProviderType} is Created Successfully" });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginWorkerDto model)
        {
            try
            {

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                {
                    return Unauthorized("Invalid login attempt");
                }

                var accessToken = _userService.GenerateJwtToken(user);
                // var refreshToken = _userService.GenerateRefreshToken(user);

                return Ok(new
                {
                    message = "Login successful",
                    accessToken = accessToken,
                    // refreshToken = refreshToken
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }
        [HttpPost("refresh")]
        public IActionResult Refresh([FromBody] string refreshToken)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(refreshToken);

            if (jwtToken.ValidTo < DateTime.UtcNow)
                return Unauthorized("Refresh token expired");

            var userId = jwtToken.Claims.First(c => c.Type == "uid").Value;
            var user = _userManager.FindByIdAsync(userId).Result;

            var newAccessToken = _userService.GenerateJwtToken(user);
            var newRefreshToken = _userService.GenerateRefreshToken(user);

            return Ok(new
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            });
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