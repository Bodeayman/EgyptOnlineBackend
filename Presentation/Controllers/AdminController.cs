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

namespace EgyptOnline.Controllers
{

    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class AdminController : ControllerBase
    {



        private readonly ApplicationDbContext _context;



        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("users")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetAllUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = Constants.PAGE_SIZE)
        {
            try
            {
                var usersQuery = _context.Users
                    .Select(u => new
                    {
                        u.Id, // keep Id for editing/updating later
                        u.UserName,
                        u.Email,
                        u.PhoneNumber,
                        u.Points,
                        SubscriptionStartDate = u.ServiceProvider != null && u.Subscription != null
                                                ? u.Subscription.StartDate
                                                : (DateOnly?)null,
                        SubscriptionEndDate = u.ServiceProvider != null && u.Subscription != null
                                              ? u.Subscription.EndDate
                                              : (DateOnly?)null,
                        IsAvailable = u.ServiceProvider != null ? u.ServiceProvider.IsAvailable : (bool?)null,
                        ProviderType = u.ServiceProvider != null ? u.ServiceProvider.ProviderType : null
                    });

                // Apply pagination
                var pagedUsersQuery = Helper.PaginateUsers(usersQuery, pageNumber, pageSize);

                var users = await pagedUsersQuery.ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }
        // DTO for updates


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

                // Update basic user fields
                if (dto.PhoneNumber != null)
                    user.PhoneNumber = dto.PhoneNumber;

                if (dto.Points.HasValue)
                    user.Points = dto.Points.Value;

                // Update ServiceProvider fields
                if (user.ServiceProvider != null)
                {
                    if (dto.IsAvailable.HasValue)
                        user.ServiceProvider.IsAvailable = dto.IsAvailable.Value;

                    if (dto.ProviderType != null)
                        user.ServiceProvider.ProviderType = dto.ProviderType;
                }

                // Update Subscription fields
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
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }


        }
        [HttpDelete("users/{userId}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return NotFound(new { message = "User not found" });

                // Optionally, remove related entities first if cascade delete is not configured
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

    }

}
