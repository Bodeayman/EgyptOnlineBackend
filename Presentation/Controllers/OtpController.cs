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
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return BadRequest(new { message = "Phone number is required" });

            // Normalize to same format as registration: +2 + digits (e.g. 01012345678 -> +201012345678)
            string phoneNumber = request.PhoneNumber.Trim();
            if (!phoneNumber.StartsWith("+"))
                phoneNumber = $"+2{phoneNumber}";

            // Find user by phone (primary); optionally match email if provided
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            if (user == null)
                return NotFound(new { message = "User not found" });
            if (!string.IsNullOrWhiteSpace(request.Email) && user.Email != request.Email)
                return NotFound(new { message = "User not found" });

            // OTP key is phone-based (email optional)
            string key = string.IsNullOrWhiteSpace(request.Email) ? phoneNumber : $"{request.Email}:{phoneNumber}";

            await _otpService.SendOtpAsync(key, false);

            return Ok(new { message = "OTP sent successfully" });
        }

        // ------------------ VERIFY OTP ------------------

        // ------------------ CHANGE PASSWORD ------------------
        [AllowAnonymous]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] OtpVerifyDto model)
        {
            if (string.IsNullOrWhiteSpace(model.PhoneNumber))
                return BadRequest(new { message = "Phone number is required" });

            string phoneNumber = model.PhoneNumber.Trim();
            if (!phoneNumber.StartsWith("+"))
                phoneNumber = $"+2{phoneNumber}";

            // OTP key: phone-only when email not used
            string key = string.IsNullOrWhiteSpace(model.Email) ? phoneNumber : $"{model.Email}:{phoneNumber}";

            bool isOtpValid = await _otpService.ValidateOtpAsync(key, model.Otp);
            if (!isOtpValid)
                return BadRequest(new { message = "OTP invalid or expired" });

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            if (user == null)
                return NotFound(new { message = "User not found" });
            if (!string.IsNullOrWhiteSpace(model.Email) && user.Email != model.Email)
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