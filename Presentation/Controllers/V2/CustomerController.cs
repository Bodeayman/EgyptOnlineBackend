using System.ComponentModel.DataAnnotations;
using EgyptOnline.Application.Services.Contract;
using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Presentation.Controllers.V2
{
    /// <summary>
    /// RESTful V2 API Controller for Customer users
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/customers")]
    [ApiVersion("2.0")]
    [Authorize(Roles = Roles.Customer)]
    public class CustomerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ContractService _contractService;
        private readonly WalletService _walletService;

        public CustomerController(
            ApplicationDbContext context,
            ContractService contractService,
            WalletService walletService)
        {
            _context = context;
            _contractService = contractService;
            _walletService = walletService;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        /// <summary>
        /// Get the authenticated customer's profile.
        /// GET /api/v2/customers/me
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found" });

            return Ok(new
            {
                id = user.Id,
                firstName = user.FirstName,
                lastName = user.LastName,
                phoneNumber = user.PhoneNumber,
                email = user.Email,
                governorate = user.Governorate,
                city = user.City,
                district = user.District,
                role = Roles.Customer
            });
        }

        /// <summary>
        /// Update the authenticated customer's profile.
        /// PATCH /api/v2/customers/me
        /// </summary>
        [HttpPatch("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateCustomerProfileDto dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found" });

            if (!string.IsNullOrWhiteSpace(dto.FirstName)) user.FirstName = dto.FirstName;
            if (dto.LastName != null) user.LastName = dto.LastName;
            if (!string.IsNullOrWhiteSpace(dto.Governorate)) user.Governorate = dto.Governorate;
            if (!string.IsNullOrWhiteSpace(dto.City)) user.City = dto.City;
            if (dto.District != null) user.District = dto.District;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Profile updated successfully",
                data = new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    phoneNumber = user.PhoneNumber,
                    email = user.Email,
                    governorate = user.Governorate,
                    city = user.City,
                    district = user.District
                }
            });
        }

        /// <summary>
        /// Get all contracts created by the authenticated customer.
        /// GET /api/v2/customers/me/contracts
        /// </summary>
        [HttpGet("me/contracts")]
        public async Task<IActionResult> GetMyContracts()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var contracts = await _contractService.GetContractsByUserIdAsync(userId);
            return Ok(new { data = contracts });
        }

        /// <summary>
        /// Get the authenticated customer's digital wallet balance.
        /// GET /api/v2/customers/me/wallet
        /// </summary>
        [HttpGet("me/wallet")]
        public async Task<IActionResult> GetMyWallet()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var balance = await _walletService.GetBalanceAsync(userId);
            return Ok(new
            {
                data = new
                {
                    userId,
                    freeBalance = balance.FreeBalance,
                    frozenBalance = balance.FrozenBalance,
                    totalBalance = balance.FreeBalance + balance.FrozenBalance
                }
            });
        }
    }

    public class UpdateCustomerProfileDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Governorate { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}
