using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace EgyptOnline.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class OTPController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IOTPService _otpService;

        public OTPController(UserManager<User> userManager, IOTPService otpService)
        {
            _userManager = userManager;
            _otpService = otpService;
        }

        // ------------------ REQUEST OTP ------------------
        [AllowAnonymous]
        [HttpPost("request-otp")]
        public async Task<IActionResult> RequestOtp([FromBody] OtpRequestDto request)
        {
            string phoneNumber = $"+2{request.PhoneNumber}";

            // Ensure both email & phone match
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.PhoneNumber == phoneNumber);

            if (user == null)
                return NotFound(new { message = "User not found" });

            // Create a key combining email + phone
            string key = $"{request.Email}:{phoneNumber}";

            // Send OTP
            await _otpService.SendOtpAsync(key, false);

            return Ok(new { message = "OTP sent successfully" });
        }

        // ------------------ VERIFY OTP ------------------

        // ------------------ CHANGE PASSWORD ------------------
        [AllowAnonymous]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] OtpVerifyDto model)
        {
            string phoneNumber = $"+2{model.PhoneNumber}";
            string key = $"{model.Email}:{phoneNumber}";
            Console.WriteLine(key);
            // Validate OTP
            bool isOtpValid = await _otpService.ValidateOtpAsync(key, model.Otp);
            if (!isOtpValid)
                return BadRequest(new { message = "OTP invalid or expired" });


            // Check user
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email && u.PhoneNumber == phoneNumber);

            if (user == null)
                return NotFound(new { message = "User not found" });

            // Reset password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);


            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = "Password changed successfully" });
        }
    }

    // ------------------ DTOs ------------------

}
