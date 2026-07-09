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
using EgyptOnline.Domain.Models.Enums;

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
        private readonly ICDNService _cdnService;
        private readonly EgyptOnline.Application.Services.Contract.ContractService _contractService;

        public AdminController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IUserService userService,
            KycService kycService,
            ComplaintService complaintService,
            WalletService walletService,
            INotificationService notificationService,
            ICDNService cdnService,
            EgyptOnline.Application.Services.Contract.ContractService contractService)
        {
            _context = context;
            _userManager = userManager;
            _userService = userService;
            _kycService = kycService;
            _complaintService = complaintService;
            _walletService = walletService;
            _notificationService = notificationService;
            _cdnService = cdnService;
            _contractService = contractService;
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
                        Wallet = _context.UserWallets
                            .Where(w => w.UserId == u.Id)
                            .Select(w => new
                            {
                                freeBalance = w.FreeBalance,
                                frozenBalance = w.FrozenBalance
                            })
                            .FirstOrDefault()
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

                // Update wallet balances if provided
                if (dto.FreeBalance.HasValue || dto.FrozenBalance.HasValue)
                {
                    var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == user.Id);
                    if (wallet == null)
                    {
                        // Create wallet if it doesn't exist
                        wallet = new UserWallet
                        {
                            UserId = user.Id,
                            FreeBalance = dto.FreeBalance ?? 0,
                            FrozenBalance = dto.FrozenBalance ?? 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.UserWallets.Add(wallet);
                    }
                    else
                    {
                        if (dto.FreeBalance.HasValue)
                            wallet.FreeBalance = dto.FreeBalance.Value;
                        if (dto.FrozenBalance.HasValue)
                            wallet.FrozenBalance = dto.FrozenBalance.Value;
                        wallet.UpdatedAt = DateTime.UtcNow;
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
        // Admin login moved to AuthController for centralized authentication handling.
        // See AuthController.AdminLogin
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
        /// GET /api/v1/Admin/kyc/pending?pageNumber=1&pageSize=20&search=keyword
        /// search: searches in user name, phone number
        /// </summary>
        [HttpGet("kyc/pending")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetPendingKyc(
            [FromQuery] string? search = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var submissions = await _kycService.GetPendingKycSubmissionsAsync(search, pageNumber, pageSize);
                var formatted = new List<object>();
                foreach (var s in submissions)
                {
                    dynamic submission = s;
                    formatted.Add(new
                    {
                        submission.Id,
                        submission.UserId,
                        submission.userName,
                        submission.firstName,
                        submission.lastName,
                        submission.phoneNumber,
                        submission.Status,
                        submission.SubmittedAt,
                        frontImageUrl = !string.IsNullOrEmpty(submission.FrontImagePath) ? await _cdnService.GetPresignedUrlAsync(submission.FrontImagePath) : null,
                        backImageUrl = !string.IsNullOrEmpty(submission.BackImagePath) ? await _cdnService.GetPresignedUrlAsync(submission.BackImagePath) : null,
                        selfieImageUrl = !string.IsNullOrEmpty(submission.SelfieImagePath) ? await _cdnService.GetPresignedUrlAsync(submission.SelfieImagePath) : null,
                        submission.RejectionReason,
                        submission.ReviewedAt
                    });
                }
                return Ok(new { data = formatted, pageNumber, pageSize });
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
                var result = await _kycService.ReviewKycAsync(kycId, adminId, dto.Status, dto.RejectionReason);

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
                    await _notificationService.SendNotificationToUser(result.UserId, title, body, "kyc");
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
                    data = new { result.Id, result.UserId, result.Status, result.ReviewedAt, result.RejectionReason }
                });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(409, new { message = "عذراً، لقد تم تعديل حالة هذا الطلب بالفعل من قبل مسؤول آخر" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }

        }

        // ═══════════════════════════════════════════════════════════════════
        // Deposits
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// List all pending deposits.
        /// GET /api/v1/Admin/deposits/pending?pageNumber=1&pageSize=20&search=keyword
        /// search: searches in user name, phone number, wallet number
        /// </summary>
        [HttpGet("deposits/pending")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetPendingDeposits(
            [FromQuery] string? search = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var deposits = await _walletService.GetPendingDepositsAsync(search, pageNumber, pageSize);
                var formatted = new List<object>();
                foreach (var d in deposits)
                {
                    dynamic deposit = d;
                    formatted.Add(new
                    {
                        depositId = deposit.Id,
                        userId = deposit.UserId,
                        userName = deposit.userName,
                        firstName = deposit.firstName,
                        lastName = deposit.lastName,
                        phoneNumber = deposit.phoneNumber,
                        amount = deposit.Amount,
                        receiptImageUrl = !string.IsNullOrEmpty(deposit.ReceiptImagePath) ? await _cdnService.GetPresignedUrlAsync(deposit.ReceiptImagePath) : null,
                        sourceWalletNumber = deposit.SourceWalletNumber,
                        paymentType = deposit.PaymentType,
                        walletOwnerName = deposit.WalletOwnerName,
                        recipientPhoneNumber = deposit.RecipientPhoneNumber,
                        status = deposit.Status,
                        rejectionReason = deposit.RejectionReason,
                        createdAt = deposit.CreatedAt
                    });
                }
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

                // Send Firebase Notification based on new status
                string title = "تحديث طلب الإيداع";
                string body = dto.Status switch
                {
                    "approved" => $"تهانينا! تم قبول طلب الإيداع بمبلغ {result.Amount} جنيه وإضافته إلى رصيدك.",
                    "rejected" => $"تم رفض طلب الإيداع بمبلغ {result.Amount} جنيه. السبب: {dto.RejectionReason ?? "غير محدد"}",
                    _ => "تم تحديث حالة طلب الإيداع الخاص بك"
                };

                try
                {
                    await _notificationService.SendNotificationToUser(result.UserId, title, body, "wallet");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send deposit notification to user {UserId}", result.UserId);
                }

                return Ok(new
                {
                    message = dto.Status == "approved" ? "تم قبول طلب الإيداع" : "تم رفض طلب الإيداع",
                    data = new
                    {
                        result.Id,
                        result.UserId,
                        result.Amount,
                        result.SourceWalletNumber,
                        result.WalletOwnerName,
                        result.Status,
                        result.RejectionReason,
                        result.CreatedAt,
                        result.ReviewedAt
                    }
                });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(409, new { message = "عذراً، لقد تم تعديل حالة طلب الإيداع بالفعل من قبل مسؤول آخر" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }

        }

        // ═══════════════════════════════════════════════════════════════════
        // Withdrawals
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// List all pending withdrawals.
        /// GET /api/v1/Admin/withdrawals/pending?pageNumber=1&pageSize=20&search=keyword
        /// search: searches in user name, phone number, wallet number
        /// </summary>
        [HttpGet("withdrawals/pending")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetPendingWithdrawals(
            [FromQuery] string? search = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var withdrawals = await _walletService.GetPendingWithdrawalsAsync(search, pageNumber, pageSize);
                var formatted = withdrawals.Select(w =>
                {
                    dynamic withdraw = w;
                    return new
                    {
                        withdrawId = withdraw.Id,
                        userId = withdraw.UserId,
                        userName = withdraw.userName,
                        firstName = withdraw.firstName,
                        lastName = withdraw.lastName,
                        phoneNumber = withdraw.phoneNumber,
                        amount = withdraw.Amount,
                        destinationWalletNumber = withdraw.DestinationWalletNumber,
                        paymentType = withdraw.PaymentType,
                        walletOwnerName = withdraw.WalletOwnerName,
                        sourceWalletNumber = withdraw.SourceWalletNumber,
                        status = withdraw.Status,
                        rejectionReason = withdraw.RejectionReason,
                        createdAt = withdraw.CreatedAt
                    };
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

                // Send Firebase Notification based on new status
                string title = "تحديث طلب السحب";
                string body = dto.Status switch
                {
                    "approved" => $"تهانينا! تم قبول طلب السحب بمبلغ {result.Amount} جنيه والتحويل إلى محفظتك.",
                    "rejected" => $"تم رفض طلب السحب بمبلغ {result.Amount} جنيه وتم إعادة المبلغ إلى رصيدك. السبب: {dto.RejectionReason ?? "غير محدد"}",
                    _ => "تم تحديث حالة طلب السحب الخاص بك"
                };

                try
                {
                    await _notificationService.SendNotificationToUser(result.UserId, title, body, "wallet");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send withdrawal notification to user {UserId}", result.UserId);
                }

                return Ok(new
                {
                    message = dto.Status == "approved" ? "تم قبول طلب السحب" : "تم رفض طلب السحب",
                    data = new
                    {
                        result.Id,
                        result.UserId,
                        result.Amount,
                        result.DestinationWalletNumber,
                        result.WalletOwnerName,
                        result.Status,
                        result.RejectionReason,
                        result.CreatedAt,
                        result.ReviewedAt
                    }
                });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(409, new { message = "عذراً، لقد تم تعديل حالة طلب السحب بالفعل من قبل مسؤول آخر" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }

        }

        // ═══════════════════════════════════════════════════════════════════
        // Complaints
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// List all complaints — optionally filter by status and search.
        /// GET /api/v1/Admin/complaints?status=open&pageNumber=1&pageSize=20&search=keyword
        /// status options: open | under_review | resolved | rejected
        /// search: searches in reason, description, reporter name, client name, provider name
        /// </summary>
        [HttpGet("complaints")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetComplaints(
            [FromQuery] string? status = null,
            [FromQuery] string? search = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var (items, total) = await _complaintService.GetAllComplaintsAsync(status, search, pageNumber, pageSize, true);
                return Ok(new
                {
                    data = items,
                    totalCount = total,
                    pageNumber,
                    pageSize,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
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
                var adminId = User.FindFirst("uid")?.Value ?? string.Empty;
                var complaint = await _complaintService.ReviewComplaintAsync(id, adminId, dto.Status, dto.AdminNote);

                return Ok(new
                {
                    message = "تم تحديث حالة الشكوى بنجاح",
                    data = new
                    {
                        complaint.Id,
                        complaint.Status,
                        complaint.AdminNote,
                        complaint.ResolvedAt,
                        complaint.ContractId
                    }
                });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Balance Control (Dispute Resolution)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// List terminated contracts for manual resolution.
        /// GET /api/v1/Admin/contracts/terminated?pageNumber=1&pageSize=20
        /// </summary>
        [HttpGet("contracts/terminated")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetTerminatedContracts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var contracts = await _context.Contracts
                    .Include(c => c.ClientUser)
                    .Include(c => c.ContractDays)
                    .Where(c => c.Status == "terminated")
                    .OrderByDescending(c => c.Id)
                    .ToListAsync();

                var totalCount = contracts.Count;
                var pagedContracts = contracts.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

                var formatted = pagedContracts.Select(c => new
                {
                    contractId = c.Id,
                    status = c.Status,
                    client = new
                    {
                        userId = c.ClientUserId,
                        phoneNumber = c.ClientUser?.PhoneNumber,
                        userName = c.ClientUser?.UserName
                    },
                    serviceProvider = new
                    {
                        phoneNumber = c.ServiceProviderPhoneNumber
                    },
                    contractDetails = new
                    {
                        totalAmount = c.TotalAmount,
                        totalDays = c.TotalDays,
                        penaltyAmount = c.PenaltyAmount,
                        governorate = c.Governorate,
                        city = c.City,
                        district = c.District
                    },
                    termination = new
                    {
                        terminatedAt = c.TerminatedAt,
                        terminatedBy = c.TerminatedBy,
                        reason = c.TerminationReason
                    },
                    processedDays = c.ContractDays.Count(cd => cd.Status == ContractDayStatus.Completed && cd.IsProcessed),
                    remainingDays = c.TotalDays - c.ContractDays.Count(cd => cd.Status == ContractDayStatus.Completed && cd.IsProcessed)
                });

                return Ok(new
                {
                    data = formatted,
                    pageNumber,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed contract information for dispute resolution.
        /// GET /api/v1/Admin/contracts/{id}/details
        /// </summary>
        [HttpGet("contracts/{id:int}/details")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetContractDetails(int id)
        {
            try
            {
                var contract = await _context.Contracts
                    .Include(c => c.ContractDays)
                    .Include(c => c.ClientUser)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (contract == null)
                    return NotFound(new { message = "Contract not found" });

                // Get provider user by phone number
                var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

                var clientWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == contract.ClientUserId);
                var providerWallet = providerUser != null ? await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == providerUser.Id) : null;

                var processedDays = contract.ContractDays.Count(cd => cd.Status == ContractDayStatus.Completed && cd.IsProcessed);
                var remainingDays = contract.TotalDays - processedDays;

                return Ok(new
                {
                    contractId = contract.Id,
                    status = contract.Status,
                    client = new
                    {
                        userId = contract.ClientUserId,
                        phoneNumber = contract.ClientUser?.PhoneNumber,
                        userName = contract.ClientUser?.UserName,
                        freeBalance = clientWallet?.FreeBalance ?? 0,
                        frozenBalance = clientWallet?.FrozenBalance ?? 0
                    },
                    serviceProvider = new
                    {
                        phoneNumber = contract.ServiceProviderPhoneNumber,
                        freeBalance = providerWallet?.FreeBalance ?? 0,
                        frozenBalance = providerWallet?.FrozenBalance ?? 0
                    },
                    contractDetails = new
                    {
                        startDate = contract.StartDate,
                        shiftStartTime = contract.ShiftStartTime,
                        shiftEndTime = contract.ShiftEndTime,
                        totalDays = contract.TotalDays,
                        totalAmount = contract.TotalAmount,
                        penaltyAmount = contract.PenaltyAmount,
                        governorate = contract.Governorate,
                        city = contract.City,
                        district = contract.District,
                        detailedAddress = contract.DetailedAddress,
                        notes = contract.Notes,
                        restrictedTerms = contract.RestrictedTerms
                    },
                    daysStatus = new
                    {
                        processedDays,
                        remainingDays,
                        totalDays = contract.TotalDays
                    },
                    createdAt = contract.CreatedAt,
                    cancelledAt = contract.CancelledAt,
                    cancelledBy = contract.CancelledBy,
                    terminatedAt = contract.TerminatedAt,
                    terminatedBy = contract.TerminatedBy,
                    terminationReason = contract.TerminationReason
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Override user balance for dispute resolution.
        /// PUT /api/v1/Admin/users/{userId}/balance/override
        /// Body: { "balanceType": "free" | "frozen", "amount": decimal, "operation": "add" | "deduct", "reason": string }
        /// </summary>
        [HttpPut("users/{userId}/balance/override")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> OverrideUserBalance(string userId, [FromBody] OverrideBalanceDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var adminId = User.FindFirst("uid")?.Value ?? string.Empty;
                var result = await _walletService.OverrideUserBalanceAsync(userId, dto.BalanceType, dto.Amount, dto.Operation, dto.Reason, adminId);

                return Ok(new
                {
                    message = "تم تعديل الرصيد بنجاح",
                    data = new
                    {
                        userId = result.UserId,
                        freeBalance = result.FreeBalance,
                        frozenBalance = result.FrozenBalance
                    }
                });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(409, new { message = "حدث تضارب أثناء تعديل الرصيد. يرجى إعادة تحميل الصفحة والمحاولة مرة أخرى" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }

        }

        /// <summary>
        /// Resolve contract dispute via Adjust & Resume.
        /// POST /api/v1/Admin/contracts/{id}/resolve/adjust-resume
        /// </summary>
        [HttpPost("contracts/{id}/resolve/adjust-resume")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ResolveAdjustResume(int id, [FromBody] AdminAdjustResumeDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var adminUserId = User.FindFirst("uid")?.Value ?? string.Empty;
                var contract = await _contractService.AdminAdjustAndResumeAsync(
                    id,
                    dto.DaysWorked,
                    dto.Direction,
                    dto.NewStartDate,
                    adminUserId,
                    dto.Comment
                );

                return Ok(new { message = "تمت تسوية وتفعيل العقد بنجاح", data = contract });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
        }

        /// <summary>
        /// Admin terminates a disputed contract.
        /// POST /api/v1/Admin/contracts/{id}/terminate
        /// </summary>
        [HttpPost("contracts/{id}/terminate")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> TerminateContract(int id, [FromBody] AdminTerminateContractDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var adminId = User.FindFirst("uid")?.Value ?? string.Empty;

                var contract = await _context.Contracts
                    .Include(c => c.ContractDays)
                    .FirstOrDefaultAsync(c => c.Id == id)
                    ?? throw new KeyNotFoundException("العقد غير موجود");

                if (contract.Status != "suspended")
                    throw new InvalidOperationException($"العقد يجب أن يكون معلقاً للإنهاء. الحالة الحالية: {contract.Status}");

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    contract.Status = "terminated";
                    contract.TerminatedAt = DateTime.UtcNow;
                    contract.TerminatedBy = adminId;
                    contract.CancelledBy = adminId;
                    contract.CancelledAt = DateTime.UtcNow;
                    contract.TerminationReason = dto.Reason;

                    // Mark all contract days as processed
                    foreach (var day in contract.ContractDays.Where(d => !d.IsProcessed))
                    {
                        day.IsProcessed = true;
                        day.ProcessedAt = DateTime.UtcNow;
                    }

                    // Resolve related open complaints
                    var complaints = await _context.Complaints
                        .Where(c => c.ContractId == id && c.Status == "open")
                        .ToListAsync();
                    foreach (var comp in complaints)
                    {
                        comp.Status = "resolved";
                        comp.ResolvedByAdminId = adminId;
                        comp.AdminNote = dto.Reason;
                        comp.ResolvedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Notify both parties about termination
                    var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);
                    if (providerUser != null)
                    {
                        await _notificationService.SendNotificationToUser(
                            providerUser.Id,
                            "تم إنهاء العقد",
                            $"تم إنهاء العقد #{contract.Id} من قبل الإدارة. السبب: {dto.Reason}",
                            "contract"
                        );
                    }

                    await _notificationService.SendNotificationToUser(
                        contract.ClientUserId,
                        "تم إنهاء العقد",
                        $"تم إنهاء العقد #{contract.Id} من قبل الإدارة. السبب: {dto.Reason}",
                        "contract"
                    );

                    return Ok(new { message = "تم إنهاء العقد بنجاح", data = contract });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
        }

        /// <summary>
        /// Admin resumes a suspended contract.
        /// POST /api/v1/Admin/contracts/{id}/resume
        /// </summary>
        [HttpPost("contracts/{id}/resume")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ResumeContract(int id, [FromBody] AdminResumeContractDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var adminId = User.FindFirst("uid")?.Value ?? string.Empty;

                var contract = await _context.Contracts
                    .Include(c => c.ContractDays)
                    .FirstOrDefaultAsync(c => c.Id == id)
                    ?? throw new KeyNotFoundException("العقد غير موجود");

                if (contract.Status != "suspended")
                    throw new InvalidOperationException($"العقد يجب أن يكون معلقاً للاستئناف. الحالة الحالية: {contract.Status}");

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    contract.Status = "active";

                    // Reset disputed day to pending status
                    var disputedDay = contract.ContractDays.FirstOrDefault(d => d.Status == ContractDayStatus.AbsentDisputed);
                    if (disputedDay != null)
                    {
                        disputedDay.Status = ContractDayStatus.Pending;
                        disputedDay.DisputeReportedAt = null;
                        disputedDay.DisputeReason = null;
                    }

                    // Handle contract amount adjustment if provided
                    if (dto.AdjustmentAmount.HasValue && dto.AdjustmentAmount != 0)
                    {
                        var adjustment = dto.AdjustmentAmount.Value;
                        var providerUserForWallet = await _context.Users
                            .FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

                        if (adjustment < 0)
                        {
                            // Decrease: refund to client, deduct from provider
                            var refundAmount = Math.Abs(adjustment);
                            await _walletService.SubtractFromFrozenBalanceAsync(providerUserForWallet?.Id ?? throw new InvalidOperationException("مقدم الخدمة غير موجود"), refundAmount);
                            await _walletService.AddToFreeBalanceAsync(contract.ClientUserId, refundAmount);
                            contract.TotalAmount -= refundAmount;
                        }
                        else
                        {
                            // Increase: charge client, add to provider frozen
                            await _walletService.SubtractFromFrozenBalanceAsync(contract.ClientUserId, adjustment);
                            await _walletService.AddToFrozenBalanceAsync(providerUserForWallet?.Id ?? throw new InvalidOperationException("مقدم الخدمة غير موجود"), adjustment);
                            contract.TotalAmount += adjustment;
                        }

                        // Recalculate daily salary
                        contract.DailySalary = contract.TotalDays > 0 ? contract.TotalAmount / contract.TotalDays : contract.DailySalary;
                    }

                    // Resolve related open complaints
                    var complaints = await _context.Complaints
                        .Where(c => c.ContractId == id && c.Status == "open")
                        .ToListAsync();
                    foreach (var comp in complaints)
                    {
                        comp.Status = "resolved";
                        comp.ResolvedByAdminId = adminId;
                        comp.AdminNote = dto.Reason;
                        comp.ResolvedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Notify both parties about resume
                    var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);
                    if (providerUser != null)
                    {
                        await _notificationService.SendNotificationToUser(
                            providerUser.Id,
                            "تم استئناف العقد",
                            $"تم استئناف العقد #{contract.Id} من قبل الإدارة. السبب: {dto.Reason}",
                            "contract"
                        );
                    }

                    await _notificationService.SendNotificationToUser(
                        contract.ClientUserId,
                        "تم استئناف العقد",
                        $"تم استئناف العقد #{contract.Id} من قبل الإدارة. السبب: {dto.Reason}",
                        "contract"
                    );

                    return Ok(new { message = "تم استئناف العقد بنجاح", data = contract });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
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

    public class OverrideBalanceDto
    {
        /// <summary>free | frozen</summary>
        [Required]
        public string BalanceType { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int Amount { get; set; }

        /// <summary>add | deduct</summary>
        [Required]
        public string Operation { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class AdminAdjustResumeDto
    {
        [Required]
        [Range(0, int.MaxValue)]
        public int DaysWorked { get; set; }

        [Required]
        [RegularExpression("^(client_to_free|client_to_worker)$", ErrorMessage = "Direction must be 'client_to_free' or 'client_to_worker'")]
        public string Direction { get; set; } = string.Empty;

        public DateTime? NewStartDate { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Comment { get; set; } = string.Empty;
    }

    public class AdminTerminateContractDto
    {
        [Required]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "السبب يجب أن يكون بين 5 و 500 حرف")]
        public string Reason { get; set; } = string.Empty;
    }

    public class AdminResumeContractDto
    {
        [Required]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "السبب يجب أن يكون بين 5 و 500 حرف")]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Optional adjustment to the contract total amount.
        /// Negative value = decrease (refund to client, deduct from provider)
        /// Positive value = increase (charge client, add to provider frozen)
        /// </summary>
        public int? AdjustmentAmount { get; set; }
    }
}
