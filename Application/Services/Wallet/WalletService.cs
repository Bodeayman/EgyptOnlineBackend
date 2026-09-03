using EgyptOnline.Data;
using EgyptOnline.Domain.Models;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Infrastructure;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Dtos.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text.RegularExpressions;

namespace EgyptOnline.Application.Services.Wallet
{
    public class WalletService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<WalletService> _logger;
        private readonly IEmailService _emailService;
        private readonly UserManager<User> _userManager;

        public WalletService(ApplicationDbContext context, INotificationService notificationService, ILogger<WalletService> logger, IEmailService emailService, UserManager<User> userManager)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
            _emailService = emailService;
            _userManager = userManager;
        }

        private bool ValidateEgyptianPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            // Remove spaces and dashes
            var cleaned = phoneNumber.Replace(" ", "").Replace("-", "");

            // Egyptian phone number pattern: +20 followed by 10 digits, or 11 digits starting with 01
            var pattern = @"^(\+20)?01[0125][0-9]{8}$";
            return Regex.IsMatch(cleaned, pattern);
        }

        private string NormalizeEgyptianPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return phoneNumber;

            // Remove spaces and dashes
            var cleaned = phoneNumber.Replace(" ", "").Replace("-", "");

            // If it doesn't already start with +20, add it
            if (!cleaned.StartsWith("+20"))
            {
                cleaned = "+2" + cleaned;
            }

            return cleaned;
        }

        /// <summary>
        /// Get wallet for the user. Self-heals by creating a digital wallet if missing.
        /// </summary>
        public async Task<UserWallet> GetWalletAsync(string userId)
        {
            var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new UserWallet
                {
                    UserId = userId,
                    FreeBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserWallets.Add(wallet);
                await _context.SaveChangesAsync();
            }
            return wallet;
        }

        public async Task<UserWallet> GetBalanceAsync(string userId)
        {
            return await GetWalletAsync(userId);
        }

        public async Task<UserWallet> DepositAsync(string userId, int amount)
        {
            if (amount <= 0)
                throw new InvalidOperationException("المبلغ يجب ان يكون اكبر من صفر");

            // Check KYC
            await RequireApprovedKyc(userId);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var wallet = await GetWalletAsync(userId);
                wallet.FreeBalance += amount;
                wallet.UpdatedAt = DateTime.UtcNow;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = userId,
                    Type = "deposit",
                    Amount = amount,
                    Description = "ايداع رصيد مباشر"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return wallet;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<UserWallet> WithdrawAsync(string userId, int amount)
        {
            if (amount <= 0)
                throw new InvalidOperationException("المبلغ يجب ان يكون اكبر من صفر");

            // Check KYC
            await RequireApprovedKyc(userId);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var wallet = await GetWalletAsync(userId);

                if (wallet.FreeBalance < amount)
                    throw new InvalidOperationException("الرصيد غير كافي");

                wallet.FreeBalance -= amount;
                wallet.UpdatedAt = DateTime.UtcNow;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = userId,
                    Type = "withdraw",
                    Amount = amount,
                    Description = "سحب رصيد مباشر"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return wallet;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<(UserWallet fromWallet, UserWallet toWallet)> TransferAsync(string fromUserId, string toUserId, int amount)
        {
            if (amount <= 0)
                throw new InvalidOperationException("المبلغ يجب ان يكون اكبر من صفر");

            if (fromUserId == toUserId)
                throw new InvalidOperationException("لا يمكن التحويل لنفس المحفظة");

            // Require sender to have approved KYC before transferring funds
            await RequireApprovedKyc(fromUserId);

            // Verify recipient user exists
            var toUserExists = await _context.Users.AnyAsync(u => u.Id == toUserId);
            if (!toUserExists)
                throw new InvalidOperationException("المستلم غير موجود");


            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var fromWallet = await GetWalletAsync(fromUserId);
                var toWallet = await GetWalletAsync(toUserId);

                if (fromWallet.FreeBalance < amount)
                    throw new InvalidOperationException("الرصيد غير كافي");

                fromWallet.FreeBalance -= amount;
                fromWallet.UpdatedAt = DateTime.UtcNow;
                toWallet.FreeBalance += amount;
                toWallet.UpdatedAt = DateTime.UtcNow;

                _context.WalletTransactions.AddRange(
                    new WalletTransaction
                    {
                        UserId = fromUserId,
                        Type = "transfer_out",
                        Amount = amount,
                        Description = "تحويل صادر",
                        FromUserId = fromUserId,
                        ToUserId = toUserId
                    },
                    new WalletTransaction
                    {
                        UserId = toUserId,
                        Type = "transfer_in",
                        Amount = amount,
                        Description = "تحويل وارد",
                        FromUserId = fromUserId,
                        ToUserId = toUserId
                    }
                );

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (fromWallet, toWallet);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<WalletTransaction>> GetTransactionHistoryAsync(string userId, int pageNumber = 1, int pageSize = 20)
        {
            return await _context.WalletTransactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        private async Task RequireApprovedKyc(string userId)
        {
            var kyc = await _context.KycSubmissions
                .Where(k => k.UserId == userId)
                .OrderByDescending(k => k.SubmittedAt)
                .FirstOrDefaultAsync();

            if (kyc == null || kyc.Status != "approved")
                throw new InvalidOperationException("يجب اعتماد التحقق من الهوية (KYC) اولا");
        }

        // ─── DEPOSIT REQUESTS ────────────────────────────────────────────────

        public async Task<DepositRequest> SubmitDepositRequestAsync(
            string userId,
            int amount,
            string sourceWalletNumber,
            string walletOwnerName,
            string receiptImagePath,
            PaymentType paymentType = PaymentType.MobileWallet)
        {
            if (amount <= 0)
                throw new InvalidOperationException("المبلغ يجب ان يكون اكبر من صفر");

            if (!ValidateEgyptianPhoneNumber(sourceWalletNumber))
                throw new InvalidOperationException("رقم المحفظة غير صحيح");

            // Normalize the wallet number to include +20 prefix
            sourceWalletNumber = NormalizeEgyptianPhoneNumber(sourceWalletNumber);

            await RequireApprovedKyc(userId);

            // Platform's wallet number (recipient) - should be configured
            var platformWalletNumber = "01000000000"; // TODO: Move to configuration

            var request = new DepositRequest
            {
                UserId = userId,
                Amount = amount,
                SourceWalletNumber = sourceWalletNumber,
                PaymentType = paymentType,
                WalletOwnerName = walletOwnerName,
                RecipientPhoneNumber = platformWalletNumber,
                ReceiptImagePath = receiptImagePath,
                Status = "pending"
            };

            _context.DepositRequests.Add(request);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            // Send email to all admins
            try
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                foreach (var admin in admins)
                {
                    if (!string.IsNullOrEmpty(admin.Email))
                    {
                        var subject = "طلب إيداع جديد - معاك";
                        var body = $"تم استلام طلب إيداع جديد:\n\n" +
                                  $"اسم المستخدم: {user?.FirstName} {user?.LastName}\n" +
                                  $"رقم الهاتف: {user?.PhoneNumber}\n" +
                                  $"المبلغ: {amount} ج.م\n" +
                                  $"نوع الدفع: {paymentType}\n" +
                                  $"رقم المحفظة المصدر: {sourceWalletNumber}\n" +
                                  $"اسم مالك المحفظة: {walletOwnerName}\n" +
                                  $"رقم المحفظة المستلمة: {platformWalletNumber}\n" +
                                  $"تاريخ الطلب: {request.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
                                  $"معرف المستخدم: {userId}\n" +
                                  $"معرف الطلب: {request.Id}\n\n" +
                                  $"يرجى مراجعة الطلب في لوحة التحكم.";

                        await _emailService.SendEmailAsync(admin.Email, subject, body);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send deposit request email to admins");
            }

            return request;
        }

        public async Task<List<object>> GetPendingDepositsAsync(string? search = null, int pageNumber = 1, int pageSize = 20)
        {
            var query = _context.DepositRequests
                .Include(r => r.User)
                .Where(r => r.Status == "pending");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(r =>
                    r.User.FirstName.ToLower().Contains(searchLower) ||
                    r.User.LastName.ToLower().Contains(searchLower) ||
                    r.User.PhoneNumber.Contains(searchLower) ||
                    r.User.UserName.ToLower().Contains(searchLower) ||
                    r.SourceWalletNumber.Contains(searchLower) ||
                    r.RecipientPhoneNumber.Contains(searchLower));
            }

            var requests = await query
                .OrderByDescending(r => r.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new List<object>();
            foreach (var request in requests)
            {
                result.Add(new
                {
                    request.Id,
                    request.UserId,
                    userName = request.User?.UserName,
                    firstName = request.User?.FirstName,
                    lastName = request.User?.LastName,
                    phoneNumber = request.User?.PhoneNumber,
                    request.Amount,
                    request.SourceWalletNumber,
                    request.PaymentType,
                    request.WalletOwnerName,
                    request.RecipientPhoneNumber,
                    request.ReceiptImagePath,
                    request.RejectionReason,
                    request.Status,
                    request.CreatedAt
                });
            }

            return result;
        }

        public async Task<DepositRequest> ReviewDepositRequestAsync(
            int depositId,
            string adminUserId,
            string status,
            string? rejectionReason)
        {
            var request = await _context.DepositRequests
                .FirstOrDefaultAsync(r => r.Id == depositId)
                ?? throw new KeyNotFoundException("طلب الإيداع غير موجود");

            if (request.Status != "pending")
                throw new InvalidOperationException("هذا الطلب تمت مراجعته بالفعل");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                request.Status = status;
                request.ReviewedByAdminId = adminUserId;
                request.ReviewedAt = DateTime.UtcNow;

                if (status == "approved")
                {
                    var wallet = await GetWalletAsync(request.UserId);
                    wallet.FreeBalance += request.Amount;
                    wallet.UpdatedAt = DateTime.UtcNow;

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = request.UserId,
                        Type = "deposit",
                        Amount = request.Amount,
                        Description = $"إيداع رصيد - طلب #{request.Id}"
                    });

                    // Send success notification using Firebase
                    await SafeNotify(request.UserId, "تم شحن المحفظة", $"تم إضافة {request.Amount} جنيه إلى حسابك");
                }
                else if (status == "rejected")
                {
                    request.RejectionReason = rejectionReason;

                    // Send reject notification using Firebase
                    await SafeNotify(request.UserId, "رفض طلب الإيداع", $"تم رفض معاملة الإيداع اللي بـ {request.Amount} جنيه. السبب: {rejectionReason ?? "غير محدد"}");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return request;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ─── WITHDRAW REQUESTS ───────────────────────────────────────────────

        public async Task<WithdrawRequest> SubmitWithdrawRequestAsync(
            string userId,
            int amount,
            string destinationWalletNumber,
            string walletOwnerName,
            PaymentType paymentType = PaymentType.MobileWallet)
        {
            if (amount <= 0)
                throw new InvalidOperationException("المبلغ يجب ان يكون اكبر من صفر");

            if (!ValidateEgyptianPhoneNumber(destinationWalletNumber))
                throw new InvalidOperationException("رقم المحفظة غير صحيح");

            // Normalize the wallet number to include +20 prefix
            destinationWalletNumber = NormalizeEgyptianPhoneNumber(destinationWalletNumber);

            await RequireApprovedKyc(userId);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var wallet = await GetWalletAsync(userId);
                if (wallet.FreeBalance < amount)
                    throw new InvalidOperationException("الرصيد غير كافي لطلب السحب");

                // Lock/deduct the funds immediately upon request to prevent double-spending
                wallet.FreeBalance -= amount;
                wallet.UpdatedAt = DateTime.UtcNow;

                // Get user's phone number as source wallet number
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                var sourceWalletNumber = user?.PhoneNumber ?? string.Empty;

                var request = new WithdrawRequest
                {
                    UserId = userId,
                    Amount = amount,
                    DestinationWalletNumber = destinationWalletNumber,
                    PaymentType = paymentType,
                    WalletOwnerName = walletOwnerName,
                    SourceWalletNumber = sourceWalletNumber,
                    Status = "pending"
                };

                _context.WithdrawRequests.Add(request);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Send email to all admins
                try
                {
                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    foreach (var admin in admins)
                    {
                        if (!string.IsNullOrEmpty(admin.Email))
                        {
                            var subject = "طلب سحب جديد - معاك";
                            var body = $"تم استلام طلب سحب جديد:\n\n" +
                                      $"اسم المستخدم: {user?.FirstName} {user?.LastName}\n" +
                                      $"رقم الهاتف: {user?.PhoneNumber}\n" +
                                      $"المبلغ: {amount} ج.م\n" +
                                      $"نوع الدفع: {paymentType}\n" +
                                      $"رقم المحفظة المصدر: {sourceWalletNumber}\n" +
                                      $"رقم المحفظة المستلمة: {destinationWalletNumber}\n" +
                                      $"اسم مالك المحفظة: {walletOwnerName}\n" +
                                      $"تاريخ الطلب: {request.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
                                      $"معرف المستخدم: {userId}\n" +
                                      $"معرف الطلب: {request.Id}\n\n" +
                                      $"يرجى مراجعة الطلب في لوحة التحكم.";

                            await _emailService.SendEmailAsync(admin.Email, subject, body);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send withdrawal request email to admins");
                }

                return request;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<object>> GetPendingWithdrawalsAsync(string? search = null, int pageNumber = 1, int pageSize = 20)
        {
            var query = _context.WithdrawRequests
                .Include(r => r.User)
                .Where(r => r.Status == "pending");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(r =>
                    r.User.FirstName.ToLower().Contains(searchLower) ||
                    r.User.LastName.ToLower().Contains(searchLower) ||
                    r.User.PhoneNumber.Contains(searchLower) ||
                    r.User.UserName.ToLower().Contains(searchLower) ||
                    r.DestinationWalletNumber.Contains(searchLower) ||
                    r.SourceWalletNumber.Contains(searchLower));
            }

            var requests = await query
                .OrderByDescending(r => r.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new List<object>();
            foreach (var request in requests)
            {
                result.Add(new
                {
                    request.Id,
                    request.UserId,
                    userName = request.User?.UserName,
                    firstName = request.User?.FirstName,
                    lastName = request.User?.LastName,
                    phoneNumber = request.User?.PhoneNumber,
                    request.Amount,
                    request.DestinationWalletNumber,
                    request.PaymentType,
                    request.WalletOwnerName,
                    request.RejectionReason,
                    request.Status,
                    request.CreatedAt
                });
            }


            return result;
        }

        public async Task<WithdrawRequest> ReviewWithdrawRequestAsync(
            int withdrawId,
            string adminUserId,
            string status,
            string? rejectionReason)
        {
            var request = await _context.WithdrawRequests
                .FirstOrDefaultAsync(r => r.Id == withdrawId)
                ?? throw new KeyNotFoundException("طلب السحب غير موجود");

            if (request.Status != "pending")
                throw new InvalidOperationException("هذا الطلب تمت مراجعته بالفعل");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                request.Status = status;
                request.ReviewedByAdminId = adminUserId;
                request.ReviewedAt = DateTime.UtcNow;

                if (status == "approved")
                {
                    // Money was already deducted on request creation, so we just log the transaction
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = request.UserId,
                        Type = "withdraw",
                        Amount = request.Amount,
                        Description = $"سحب رصيد - طلب #{request.Id}"
                    });

                    // Send success notification using Firebase
                    await SafeNotify(request.UserId, "تم السحب بنجاح", $"تم سحب {request.Amount} جنيه من حسابك، وتم التحويل إلى المحفظة رقم {request.DestinationWalletNumber}");
                }
                else if (status == "rejected")
                {
                    request.RejectionReason = rejectionReason;

                    // Refund the locked money back to user's wallet
                    var wallet = await GetWalletAsync(request.UserId);
                    wallet.FreeBalance += request.Amount;
                    wallet.UpdatedAt = DateTime.UtcNow;

                    // Send reject notification using Firebase
                    await SafeNotify(request.UserId, "رفض طلب السحب", $"تم رفض معاملة السحب اللي بـ {request.Amount} جنيه. السبب: {rejectionReason ?? "غير محدد"}");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return request;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────

        private async Task SafeNotify(string userId, string title, string body)
        {
            try
            {
                await _notificationService.SendNotificationToUser(userId, title, body, "wallet");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send notification to {UserId}: {Title}", userId, title);
            }
        }

        #region Free/Frozen Balance Methods (New 2-Party System)

        public async Task<UserWallet> GetWalletByUserIdAsync(string userId)
        {
            var wallet = await _context.UserWallets
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
            {
                throw new InvalidOperationException($"المحفظة غير موجودة لمعرف المستخدم: {userId}");
            }

            return wallet;
        }

        public async Task<UserWallet> GetWalletByPhoneNumberAsync(string phoneNumber)
        {
            var wallet = await _context.UserWallets
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.User.PhoneNumber == phoneNumber);

            if (wallet == null)
            {
                throw new InvalidOperationException($"المحفظة غير موجودة لرقم الهاتف: {phoneNumber}");
            }

            return wallet;
        }

        public async Task<UserWallet> CreateWalletAsync(string userId)
        {
            var existingWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (existingWallet != null)
            {
                throw new InvalidOperationException($"المحفظة موجودة بالفعل لمعرف المستخدم: {userId}");
            }

            var wallet = new UserWallet
            {
                UserId = userId,
                FreeBalance = 0,
                FrozenBalance = 0,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserWallets.Add(wallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created wallet for user {UserId}", userId);
            return wallet;
        }

        public async Task<bool> HasSufficientFreeBalanceAsync(string userId, int amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            return wallet.FreeBalance >= amount;
        }

        public async Task<bool> HasSufficientFrozenBalanceAsync(string userId, int amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            return wallet.FrozenBalance >= amount;
        }

        public async Task TransferFreeToFrozenAsync(string userId, int amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);

            if (wallet.FreeBalance < amount)
            {
                throw new InvalidOperationException($"الرصيد المتاح غير كافٍ. المطلوب: {amount}، المتاح: {wallet.FreeBalance}");
            }

            wallet.FreeBalance -= amount;
            wallet.FrozenBalance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Transferred {Amount} from free to frozen for user {UserId}", amount, userId);
        }

        public async Task TransferFrozenToFreeAsync(string userId, int amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);

            if (wallet.FrozenBalance < amount)
            {
                throw new InvalidOperationException($"الرصيد المجمد غير كافٍ. المطلوب: {amount}، المتاح: {wallet.FrozenBalance}");
            }

            wallet.FrozenBalance -= amount;
            wallet.FreeBalance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Transferred {Amount} from frozen to free for user {UserId}", amount, userId);
        }

        public async Task TransferFreeBetweenUsersAsync(string fromUserId, string toUserId, int amount)
        {
            var fromWallet = await GetWalletByUserIdAsync(fromUserId);
            var toWallet = await GetWalletByUserIdAsync(toUserId);

            if (fromWallet.FreeBalance < amount)
            {
                throw new InvalidOperationException($"الرصيد المتاح للمرسل غير كافٍ. المطلوب: {amount}، المتاح: {fromWallet.FreeBalance}");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                fromWallet.FreeBalance -= amount;
                fromWallet.UpdatedAt = DateTime.UtcNow;

                toWallet.FreeBalance += amount;
                toWallet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Transferred {Amount} free balance from {FromUserId} to {ToUserId}", amount, fromUserId, toUserId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task TransferFrozenBetweenUsersAsync(string fromUserId, string toUserId, int amount)
        {
            var fromWallet = await GetWalletByUserIdAsync(fromUserId);
            var toWallet = await GetWalletByUserIdAsync(toUserId);

            if (fromWallet.FrozenBalance < amount)
            {
                throw new InvalidOperationException($"الرصيد المجمد للمرسل غير كافٍ. المطلوب: {amount}، المتاح: {fromWallet.FrozenBalance}");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                fromWallet.FrozenBalance -= amount;
                fromWallet.UpdatedAt = DateTime.UtcNow;

                toWallet.FrozenBalance += amount;
                toWallet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Transferred {Amount} frozen balance from {FromUserId} to {ToUserId}", amount, fromUserId, toUserId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task AddToFreeBalanceAsync(string userId, int amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            wallet.FreeBalance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Added {Amount} to free balance for user {UserId}", amount, userId);
        }

        public async Task SubtractFromFreeBalanceAsync(string userId, int amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);

            if (wallet.FreeBalance < amount)
            {
                throw new InvalidOperationException($"الرصيد المتاح غير كافٍ. المطلوب: {amount}، المتاح: {wallet.FreeBalance}");
            }

            wallet.FreeBalance -= amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Subtracted {Amount} from free balance for user {UserId}", amount, userId);
        }

        public async Task AddToFrozenBalanceAsync(string userId, int amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            wallet.FrozenBalance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Added {Amount} to frozen balance for user {UserId}", amount, userId);
        }

        public async Task SubtractFromFrozenBalanceAsync(string userId, int amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);

            if (wallet.FrozenBalance < amount)
            {
                throw new InvalidOperationException($"الرصيد المجمد غير كافٍ. المطلوب: {amount}، المتاح: {wallet.FrozenBalance}");
            }

            wallet.FrozenBalance -= amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Subtracted {Amount} from frozen balance for user {UserId}", amount, userId);
        }

        public async Task AdminOverrideBalanceAsync(string userId, int newFreeBalance, int newFrozenBalance, string reason)
        {
            var wallet = await GetWalletByUserIdAsync(userId);

            var oldFree = wallet.FreeBalance;
            var oldFrozen = wallet.FrozenBalance;

            wallet.FreeBalance = newFreeBalance;
            wallet.FrozenBalance = newFrozenBalance;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogWarning("Admin override for user {UserId}. Free: {OldFree} -> {NewFree}, Frozen: {OldFrozen} -> {NewFrozen}. Reason: {Reason}",
                userId, oldFree, newFreeBalance, oldFrozen, newFrozenBalance, reason);
        }

        public async Task AdminDepositAsync(string phoneNumber, int amount, string reference)
        {
            var wallet = await GetWalletByPhoneNumberAsync(phoneNumber);

            wallet.FreeBalance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin deposit of {Amount} to phone {PhoneNumber}. Reference: {Reference}", amount, phoneNumber, reference);
        }

        public async Task<bool> CanInitiateWithdrawalAsync(string userId, int amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            return wallet.FreeBalance >= amount;
        }

        public async Task AdminCompleteWithdrawalAsync(string userId, int amount, string reference)
        {
            var wallet = await GetWalletByUserIdAsync(userId);

            if (wallet.FreeBalance < amount)
            {
                throw new InvalidOperationException($"الرصيد المتاح غير كافٍ للسحب. المطلوب: {amount}، المتاح: {wallet.FreeBalance}");
            }

            wallet.FreeBalance -= amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin completed withdrawal of {Amount} for user {UserId}. Reference: {Reference}", amount, userId, reference);
        }

        public async Task<UserWallet> OverrideUserBalanceAsync(string userId, string balanceType, int amount, string operation, string reason, string adminId)
        {
            var wallet = await GetWalletByUserIdAsync(userId);

            int oldValue = balanceType == "free" ? wallet.FreeBalance : wallet.FrozenBalance;
            int newValue = operation == "add" ? oldValue + amount : oldValue - amount;

            if (newValue < 0)
            {
                throw new InvalidOperationException($"لا يمكن خصم {amount} من رصيد {balanceType}. الحالي: {oldValue}");
            }

            if (balanceType == "free")
            {
                wallet.FreeBalance = newValue;
            }
            else
            {
                wallet.FrozenBalance = newValue;
            }

            wallet.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogWarning("Admin {AdminId} override for user {UserId}. {BalanceType}: {OldValue} -> {NewValue} ({Operation} {Amount}). Reason: {Reason}",
                adminId, userId, balanceType, oldValue, newValue, operation, amount, reason);

            return wallet;
        }


        public async Task<PagedBalanceTransactionsResponse> GetBalanceTransactionsAsync(
            BalanceAuditQueryFilter filter,
            int pageNumber = 1,
            int pageSize = 20)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var query = _context.WalletTransactions
                .Include(t => t.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.UserId))
            {
                query = query.Where(t => t.UserId == filter.UserId);
            }

            if (filter.BalanceType.HasValue)
            {
                query = query.Where(t => t.BalanceType == filter.BalanceType.Value);
            }

            if (filter.OperationType.HasValue)
            {
                query = query.Where(t => t.OperationType == filter.OperationType.Value);
            }

            if (filter.From.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= filter.From.Value);
            }

            if (filter.To.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= filter.To.Value);
            }

            var totalCount = await query.CountAsync();

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new BalanceTransactionDto
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    UserName = t.User != null ? t.User.FirstName + " " + t.User.LastName : string.Empty,
                    UserPhone = t.User != null ? t.User.PhoneNumber : string.Empty,
                    BalanceType = t.BalanceType,
                    OperationType = t.OperationType,
                    Amount = t.Amount,
                    BalanceBefore = t.BalanceBefore,
                    BalanceAfter = t.BalanceAfter,
                    Description = t.Description,
                    ReferenceId = t.ContractId,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return new PagedBalanceTransactionsResponse
            {
                Items = transactions,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            };
        }

        #endregion
    }
}
