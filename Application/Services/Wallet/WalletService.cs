using EgyptOnline.Data;
using EgyptOnline.Domain.Models;
using EgyptOnline.Models;
using EgyptOnline.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EgyptOnline.Application.Services.Wallet
{
    public class WalletService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<WalletService> _logger;

        public WalletService(ApplicationDbContext context, INotificationService notificationService, ILogger<WalletService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
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

        public async Task<UserWallet> DepositAsync(string userId, decimal amount)
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

        public async Task<UserWallet> WithdrawAsync(string userId, decimal amount)
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

        public async Task<(UserWallet fromWallet, UserWallet toWallet)> TransferAsync(string fromUserId, string toUserId, decimal amount)
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
            decimal amount,
            string sourceWalletNumber,
            string walletOwnerName,
            string receiptImagePath)
        {
            if (amount <= 0)
                throw new InvalidOperationException("المبلغ يجب ان يكون اكبر من صفر");

            await RequireApprovedKyc(userId);

            // Platform's wallet number (recipient) - should be configured
            var platformWalletNumber = "01000000000"; // TODO: Move to configuration

            var request = new DepositRequest
            {
                UserId = userId,
                Amount = amount,
                SourceWalletNumber = sourceWalletNumber,
                WalletOwnerName = walletOwnerName,
                RecipientPhoneNumber = platformWalletNumber,
                ReceiptImagePath = receiptImagePath,
                Status = "pending"
            };

            _context.DepositRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task<List<object>> GetPendingDepositsAsync(int pageNumber = 1, int pageSize = 20)
        {
            var requests = await _context.DepositRequests
                .Where(r => r.Status == "pending")
                .OrderBy(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userIds = requests.Select(r => r.UserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName, u.PhoneNumber })
                .ToDictionaryAsync(u => u.Id);

            var result = new List<object>();
            foreach (var request in requests)
            {
                var user = users.GetValueOrDefault(request.UserId);
                result.Add(new
                {
                    request.Id,
                    request.UserId,
                    userName = user?.UserName,
                    firstName = user?.FirstName,
                    lastName = user?.LastName,
                    phoneNumber = user?.PhoneNumber,
                    request.Amount,
                    request.SourceWalletNumber,
                    request.WalletOwnerName,
                    request.RecipientPhoneNumber,
                    request.ReceiptImagePath,
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
            decimal amount,
            string destinationWalletNumber,
            string walletOwnerName)
        {
            if (amount <= 0)
                throw new InvalidOperationException("المبلغ يجب ان يكون اكبر من صفر");

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
                    WalletOwnerName = walletOwnerName,
                    SourceWalletNumber = sourceWalletNumber,
                    Status = "pending"
                };

                _context.WithdrawRequests.Add(request);
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

        public async Task<List<object>> GetPendingWithdrawalsAsync(int pageNumber = 1, int pageSize = 20)
        {
            var requests = await _context.WithdrawRequests
                .Where(r => r.Status == "pending")
                .OrderBy(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userIds = requests.Select(r => r.UserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName, u.PhoneNumber })
                .ToDictionaryAsync(u => u.Id);

            var result = new List<object>();
            foreach (var request in requests)
            {
                var user = users.GetValueOrDefault(request.UserId);
                result.Add(new
                {
                    request.Id,
                    request.UserId,
                    userName = user?.UserName,
                    firstName = user?.FirstName,
                    lastName = user?.LastName,
                    phoneNumber = user?.PhoneNumber,
                    request.Amount,
                    request.DestinationWalletNumber,
                    request.WalletOwnerName,
                    request.SourceWalletNumber,
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

        public async Task<bool> HasSufficientFreeBalanceAsync(string userId, decimal amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            return wallet.FreeBalance >= amount;
        }

        public async Task<bool> HasSufficientFrozenBalanceAsync(string userId, decimal amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            return wallet.FrozenBalance >= amount;
        }

        public async Task TransferFreeToFrozenAsync(string userId, decimal amount)
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

        public async Task TransferFrozenToFreeAsync(string userId, decimal amount)
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

        public async Task TransferFreeBetweenUsersAsync(string fromUserId, string toUserId, decimal amount)
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

        public async Task TransferFrozenBetweenUsersAsync(string fromUserId, string toUserId, decimal amount)
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

        public async Task AddToFreeBalanceAsync(string userId, decimal amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            wallet.FreeBalance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Added {Amount} to free balance for user {UserId}", amount, userId);
        }

        public async Task SubtractFromFreeBalanceAsync(string userId, decimal amount)
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

        public async Task AddToFrozenBalanceAsync(string userId, decimal amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            wallet.FrozenBalance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Added {Amount} to frozen balance for user {UserId}", amount, userId);
        }

        public async Task SubtractFromFrozenBalanceAsync(string userId, decimal amount)
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

        public async Task AdminOverrideBalanceAsync(string userId, decimal newFreeBalance, decimal newFrozenBalance, string reason)
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

        public async Task AdminDepositAsync(string phoneNumber, decimal amount, string reference)
        {
            var wallet = await GetWalletByPhoneNumberAsync(phoneNumber);

            wallet.FreeBalance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin deposit of {Amount} to phone {PhoneNumber}. Reference: {Reference}", amount, phoneNumber, reference);
        }

        public async Task<bool> CanInitiateWithdrawalAsync(string userId, decimal amount)
        {
            var wallet = await GetWalletByUserIdAsync(userId);
            return wallet.FreeBalance >= amount;
        }

        public async Task AdminCompleteWithdrawalAsync(string userId, decimal amount, string reference)
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

        public async Task<UserWallet> OverrideUserBalanceAsync(string userId, string balanceType, decimal amount, string operation, string reason, string adminId)
        {
            var wallet = await GetWalletByUserIdAsync(userId);

            decimal oldValue = balanceType == "free" ? wallet.FreeBalance : wallet.FrozenBalance;
            decimal newValue = operation == "add" ? oldValue + amount : oldValue - amount;

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

        #endregion
    }
}
