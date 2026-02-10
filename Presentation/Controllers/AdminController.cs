using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Services;
using EgyptOnline.Data;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Identity.Data;
using System.Text.RegularExpressions;

namespace EgyptOnline.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IUserService _userService;

        public AdminController(ApplicationDbContext context, UserManager<User> userManager, IUserService userService)
        {
            _context = context;
            _userManager = userManager;
            _userService = userService;
        }

        [HttpGet("users")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetAllUsers(

            [FromQuery] SearchAdminDto dto,
                  [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = Constants.PAGE_SIZE
            )
        {
            try
            {
                var usersQuery = _context.Users
                .Include(u => u.Subscription)
                .Include(u => u.ServiceProvider)
                    .Select(u => new
                    {
                        u.Id,
                        u.UserName,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        u.PhoneNumber,
                        u.Points,
                        u.Governorate,
                        u.City,
                        u.District,
                        SubscriptionStartDate = u.ServiceProvider != null && u.Subscription != null
                                                ? u.Subscription.StartDate
                                                : (DateTime?)null,
                        SubscriptionEndDate = u.ServiceProvider != null && u.Subscription != null
                                              ? u.Subscription.EndDate
                                              : (DateTime?)null,
                        IsAvailable = u.ServiceProvider != null ? u.ServiceProvider.IsAvailable : (bool?)null,
                        ProviderType = u.ServiceProvider != null ? u.ServiceProvider.ProviderType : null,
                        Profession = u.ServiceProvider != null ? u.ServiceProvider!.GetSpecialization() : "Not Found",
                        SubscriptionPoints = u.SubscriptionPoints,
                    });
                Console.WriteLine("Continue");
                // Apply search filters
                if (!string.IsNullOrWhiteSpace(dto.Email))
                {
                    usersQuery = usersQuery.Where(u => u.Email.Contains(dto.Email));
                }

                if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                {
                    usersQuery = usersQuery.Where(u => u.PhoneNumber.Contains(dto.PhoneNumber));
                }

                if (!string.IsNullOrWhiteSpace(dto.FirstName))
                {
                    usersQuery = usersQuery.Where(u => u.FirstName.Contains(dto.FirstName));
                }

                if (!string.IsNullOrWhiteSpace(dto.LastName))
                {
                    usersQuery = usersQuery.Where(u => u.LastName.Contains(dto.LastName));
                }

                if (!string.IsNullOrWhiteSpace(dto.Governorate))
                {
                    usersQuery = usersQuery.Where(u => u.Governorate.Contains(dto.Governorate));
                }

                if (!string.IsNullOrWhiteSpace(dto.City))
                {
                    usersQuery = usersQuery.Where(u => u.City.Contains(dto.City));
                }

                if (!string.IsNullOrWhiteSpace(dto.District))
                {
                    usersQuery = usersQuery.Where(u => u.District.Contains(dto.District));
                }

                // Get total count for pagination
                var totalCount = await usersQuery.CountAsync();

                // Apply pagination
                var pagedUsersQuery = Helper.PaginateUsers(usersQuery, pageNumber, pageSize);

                var users = await pagedUsersQuery.ToListAsync();

                return Ok(new
                {
                    data = users,
                    pageNumber,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }
        [HttpGet("payments/{userId}")]
        [Authorize(Roles = Roles.Admin)]

        public async Task<IActionResult> GetUserPayments(string userId)
        {
            var paymentTransaction = await _context.PaymentTransactions.Where(pt => pt.UserId == userId).ToListAsync();
            if (paymentTransaction == null)
            {
                return NotFound(new { message = "No payment transactions found for the specified user." });
            }
            return Ok(paymentTransaction);
        }

        [HttpPut("users/{userId}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserDto dto)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return NotFound(new { message = "User not found" });

                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Contains(Roles.Admin))
                {
                    return BadRequest(new { message = "انتا بتعمل اييييييييييييييه؟" });
                }

                if (dto.Points < 0)
                {
                    return BadRequest(new { message = "The points should be more than or equal 0" });
                }
                if (dto.SubscriptionPoints < 0)
                {
                    return BadRequest(new { message = "The subscription points should be more than or equal 0" });
                }

                var phoneRegex = new Regex(@"^\+(2010|2011|2012|2015)\d{8}$");

                if (!phoneRegex.IsMatch(dto.PhoneNumber))
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

                if (dto.PhoneNumber != null)
                {
                    if (await _context.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber && u.Id != user.Id))
                    {
                        return BadRequest(new
                        {
                            message = "This phone is already in use",
                            errorCode = UserErrors.PhoneNumberAlreadyExists.ToString()
                        });
                    }
                    user.PhoneNumber = dto.PhoneNumber;
                }

                if (dto.Email != null)
                {
                    if (await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != user.Id))
                    {
                        return BadRequest(new
                        {
                            message = "This email is already in use",
                            errorCode = UserErrors.EmailAlreadyExists.ToString()
                        });
                    }
                    user.Email = dto.Email;
                }

                if (dto.Points.HasValue)
                    user.Points = dto.Points.Value;
                if (dto.SubscriptionPoints.HasValue)
                    user.SubscriptionPoints = dto.SubscriptionPoints.Value;

                if (user.ServiceProvider != null)
                {
                    if (dto.IsAvailable.HasValue)
                        user.ServiceProvider.IsAvailable = dto.IsAvailable.Value;


                }

                if (user.Subscription != null)
                {
                    if (dto.SubscriptionStartDate.HasValue)
                        user.Subscription.StartDate = dto.SubscriptionStartDate.Value;

                    if (dto.SubscriptionEndDate.HasValue)
                        user.Subscription.EndDate = dto.SubscriptionEndDate.Value;
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }
        /*
                [HttpDelete("users/{userId}")]
                [Authorize(Roles = Roles.Admin)]
                public async Task<IActionResult> DeleteUser(string userId)
                {
                    try
                    {
                        var user = await _context.Users
                            .Include(u => u.ServiceProvider)
                            .Include(u => u.Subscription)
                            .Include(u => u.RefreshTokens)
                            .FirstOrDefaultAsync(u => u.Id == userId);

                        if (user == null)
                            return NotFound(new { message = "User not found" });

                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains(Roles.Admin))
                        {
                            return BadRequest(new { message = "انتا بتعمل اييييييييييييييه؟" });
                        }

                        if (user.RefreshTokens != null && user.RefreshTokens.Any())
                        {
                            _context.RefreshTokens.RemoveRange(user.RefreshTokens);
                        }

                        if (user.ServiceProvider != null)
                            _context.ServiceProviders.Remove(user.ServiceProvider);

                        if (user.Subscription != null)
                            _context.Subscriptions.Remove(user.Subscription);

                        _context.Users.Remove(user);
                        await _context.SaveChangesAsync();

                        return Ok(new { message = "User deleted successfully" });
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
                    }
                }
        */
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
                else if (Helper.IsPhone(input))
                {
                    string phoneNumber = $"+20{input.Substring(1)}";
                    user = await _context.Users
                        .Include(u => u.Subscription)
                        .Include(u => u.ServiceProvider)
                        .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
                }
                else
                {
                    return BadRequest(new { message = "Invalid email or phone format" });
                }

                if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                    return Unauthorized(new { message = "Email/Phone or password is incorrect" });

                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains(Roles.Admin))
                {
                    return Forbid();
                }

                var accessToken = await _userService.GenerateJwtToken(user, TokensTypes.AccessToken);

                return Ok(new
                {
                    message = "Login successful",
                    accessToken,
                    subscriptionExpiry = user.Subscription?.EndDate,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
        //Useless function no use really
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest refreshRequest)
        {
            if (refreshRequest == null || string.IsNullOrEmpty(refreshRequest.RefreshToken))
                return BadRequest("Refresh token is required");

            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshRequest.RefreshToken);

            if (storedToken == null)
                return NotFound(new { message = "Refresh token not found" });

            storedToken.IsRevoked = true;
            storedToken.Revoked = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Logout successful, refresh token revoked" });
        }
    }

    public class SearchAdminDto
    {
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Governorate { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}