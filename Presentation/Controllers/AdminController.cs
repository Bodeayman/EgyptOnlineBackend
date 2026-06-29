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
using EgyptOnline.Application.Services.Kyc;
using EgyptOnline.Application.Services.Complaint;
using EgyptOnline.Application.Services.Wallet;
using System.ComponentModel.DataAnnotations;
using Serilog;

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
        private readonly KycService _kycService;
        private readonly ComplaintService _complaintService;
        private readonly WalletService _walletService;
        private readonly INotificationService _notificationService;

        public AdminController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IUserService userService,
            KycService kycService,
            ComplaintService complaintService,
            WalletService walletService,
            INotificationService notificationService)
        {
            _context          = context;
            _userManager      = userManager;
            _userService      = userService;
            _kycService       = kycService;
            _complaintService = complaintService;
            _walletService     = walletService;
            _notificationService = notificationService;
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

                if (dto.PhoneNumber != null)
                {
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

                if (dto.SubscriptionStartDate.HasValue || dto.SubscriptionEndDate.HasValue)
                {
                    if (user.Subscription == null)
                    {
                        // Explicitly create and add to context if it's a first-time subscription
                        var newSub = new Subscription
                        {
                            UserId = user.Id,
                            User = user,
                            StartDate = dto.SubscriptionStartDate ?? DateTime.UtcNow,
                            EndDate = dto.SubscriptionEndDate ?? DateTime.UtcNow.AddMonths(1),
                            UpdatedAt = DateTime.UtcNow
                        };
                        user.Subscription = newSub; // Link it directly to the user object
                        _context.Subscriptions.Add(newSub);
                    }
                    else
                    {
                        if (dto.SubscriptionStartDate.HasValue)
                            user.Subscription.StartDate = dto.SubscriptionStartDate.Value;

                        if (dto.SubscriptionEndDate.HasValue)
                            user.Subscription.EndDate = dto.SubscriptionEndDate.Value;
                        
                        user.Subscription.UpdatedAt = DateTime.UtcNow;
                    }
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
                    .Include(u => u.FirebaseTokens)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return NotFound(new { message = "User not found" });

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(Roles.Admin))
                {
                    return BadRequest(new { message = "انتا بتعمل اييييييييييييييه؟" });
                }
                if (user.FirebaseTokens != null && user.FirebaseTokens.Any())
                {
                    _context.FirebaseTokens.RemoveRange(user.FirebaseTokens);

                }
                if (user.RefreshTokens != null && user.RefreshTokens.Any())
                {
                    _context.RefreshTokens.RemoveRange(user.RefreshTokens);
                }

                if (user.ServiceProvider != null)
                    _context.ServiceProviders.Remove(user.ServiceProvider);

                if (user.Subscription != null)
                    _context.Subscriptions.Remove(user.Subscription);

                var result = await _userManager.DeleteAsync(user);

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
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

        // ═══════════════════════════════════════════════════════════════════
        // KYC — Identity Verification
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// List all pending KYC submissions (queue for admin review).
        /// GET /api/v1/Admin/kyc/pending?pageNumber=1&pageSize=20
        /// </summary>
        [HttpGet("kyc/pending")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetPendingKyc(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var submissions = await _kycService.GetPendingKycSubmissionsAsync(pageNumber, pageSize);
                return Ok(new { data = submissions, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Approve or reject a KYC submission.
        /// PUT /api/v1/Admin/kyc/{kycId}/review
        /// Body: { "status": "approved" | "rejected" | "edit_required", "rejectionReason": "optional" }
        /// </summary>
        [HttpPut("kyc/{kycId:int}/review")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ReviewKyc(int kycId, [FromBody] ReviewKycDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var adminId = User.FindFirst("uid")?.Value ?? string.Empty;
                var result  = await _kycService.ReviewKycAsync(kycId, adminId, dto.Status, dto.RejectionReason);

                // Send Firebase Notification based on new status
                string title = "تحديث طلب التحقق الشخصي";
                string body = dto.Status switch
                {
                    "approved" => "تهانينا! تم الموافقة على التحقق من هويتك وتفعيل حسابك بالكامل.",
                    "rejected" => $"تم رفض طلب التحقق الشخصي. السبب: {dto.RejectionReason ?? "غير محدد"}",
                    "edit_required" => $"الصور المرفوعة غير واضحة أو غير كاملة: {dto.RejectionReason ?? "يرجى إعادة تصوير البطاقة الشخصية بوضوح وإعادة الرفع."}",
                    _ => "تم تحديث حالة التحقق الشخصي الخاصة بك"
                };

                try
                {
                    await _notificationService.SendNotificationToUser(result.UserId, title, body);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send KYC notification to user {UserId}", result.UserId);
                }

                string responseMsg = dto.Status switch
                {
                    "approved" => "تمت الموافقة على التحقق",
                    "rejected" => "تم رفض التحقق",
                    "edit_required" => "تم طلب تعديل المستندات وإشعار المستخدم",
                    _ => "تم مراجعة الطلب"
                };

                return Ok(new
                {
                    message = responseMsg,
                    data    = new { result.Id, result.UserId, result.Status, result.ReviewedAt, result.RejectionReason }
                });
            }
            catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex)                 { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Deposits
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// List all pending deposits.
        /// GET /api/v1/Admin/deposits/pending
        /// </summary>
        [HttpGet("deposits/pending")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetPendingDeposits(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var deposits = await _walletService.GetPendingDepositsAsync(pageNumber, pageSize);
                var formatted = deposits.Select(d => new
                {
                    depositId = d.Id,
                    userId = d.UserId,
                    userName = d.User?.UserName,
                    firstName = d.User?.FirstName,
                    lastName = d.User?.LastName,
                    amount = d.Amount,
                    receiptImagePath = d.ReceiptImagePath,
                    sourceWalletNumber = d.SourceWalletNumber,
                    status = d.Status,
                    createdAt = d.CreatedAt
                });
                return Ok(new { data = formatted, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Review deposit request (Accept/Reject).
        /// PUT /api/v1/Admin/deposits/{id}/review
        /// </summary>
        [HttpPut("deposits/{id:int}/review")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ReviewDeposit(int id, [FromBody] ReviewKycDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var adminId = User.FindFirst("uid")?.Value ?? string.Empty;
                var result = await _walletService.ReviewDepositRequestAsync(id, adminId, dto.Status, dto.RejectionReason);
                return Ok(new
                {
                    message = dto.Status == "approved" ? "تم قبول طلب الإيداع" : "تم رفض طلب الإيداع",
                    data = result
                });
            }
            catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex)                 { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Withdrawals
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// List all pending withdrawals.
        /// GET /api/v1/Admin/withdrawals/pending
        /// </summary>
        [HttpGet("withdrawals/pending")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetPendingWithdrawals(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var withdrawals = await _walletService.GetPendingWithdrawalsAsync(pageNumber, pageSize);
                var formatted = withdrawals.Select(w => new
                {
                    withdrawId = w.Id,
                    userId = w.UserId,
                    userName = w.User?.UserName,
                    firstName = w.User?.FirstName,
                    lastName = w.User?.LastName,
                    amount = w.Amount,
                    destinationWalletNumber = w.DestinationWalletNumber,
                    walletOwnerName = w.WalletOwnerName,
                    status = w.Status,
                    createdAt = w.CreatedAt
                });
                return Ok(new { data = formatted, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Review withdrawal request (Accept/Reject).
        /// PUT /api/v1/Admin/withdrawals/{id}/review
        /// </summary>
        [HttpPut("withdrawals/{id:int}/review")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ReviewWithdraw(int id, [FromBody] ReviewKycDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var adminId = User.FindFirst("uid")?.Value ?? string.Empty;
                var result = await _walletService.ReviewWithdrawRequestAsync(id, adminId, dto.Status, dto.RejectionReason);
                return Ok(new
                {
                    message = dto.Status == "approved" ? "تم قبول طلب السحب" : "تم رفض طلب السحب",
                    data = result
                });
            }
            catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex)                 { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Complaints
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// List all complaints — optionally filter by status.
        /// GET /api/v1/Admin/complaints?status=open&pageNumber=1&pageSize=20
        /// status options: open | under_review | resolved | rejected
        /// </summary>
        [HttpGet("complaints")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetComplaints(
            [FromQuery] string? status     = null,
            [FromQuery] int pageNumber     = 1,
            [FromQuery] int pageSize       = 20)
        {
            try
            {
                var (items, total) = await _complaintService.GetAllComplaintsAsync(status, pageNumber, pageSize);
                return Ok(new
                {
                    data        = items,
                    totalCount  = total,
                    pageNumber,
                    pageSize,
                    totalPages  = (int)Math.Ceiling(total / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Review a complaint — change its status and optionally leave a note.
        /// PUT /api/v1/Admin/complaints/{id}/review
        /// Body: { "status": "under_review" | "resolved" | "rejected", "adminNote": "optional" }
        /// </summary>
        [HttpPut("complaints/{id:int}/review")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ReviewComplaint(int id, [FromBody] ReviewComplaintDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var adminId   = User.FindFirst("uid")?.Value ?? string.Empty;
                var complaint = await _complaintService.ReviewComplaintAsync(id, adminId, dto.Status, dto.AdminNote);

                return Ok(new
                {
                    message = "تم تحديث حالة الشكوى بنجاح",
                    data    = new
                    {
                        complaint.Id,
                        complaint.Status,
                        complaint.AdminNote,
                        complaint.ResolvedAt,
                        complaint.ContractId
                    }
                });
            }
            catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex)                 { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
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

    public class ReviewKycDto
    {
        /// <summary>approved | rejected</summary>
        [Required]
        public string Status { get; set; } = string.Empty;

        /// <summary>Required when Status == "rejected"</summary>
        public string? RejectionReason { get; set; }
    }

    public class ReviewComplaintDto
    {
        /// <summary>under_review | resolved | rejected</summary>
        [Required]
        public string Status { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? AdminNote { get; set; }
    }
}