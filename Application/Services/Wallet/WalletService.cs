using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EgyptOnline.Application.Services.Wallet
{
    public class WalletService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public WalletService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get or create a wallet for the user.
        /// </summary>
        public async Task<UserWallet> GetOrCreateWalletAsync(string userId)
        {
            var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new UserWallet { UserId = userId };
                _context.UserWallets.Add(wallet);
                await _context.SaveChangesAsync();
            }
            return wallet;
        }

        public async Task<UserWallet> GetBalanceAsync(string userId)
        {
            return await GetOrCreateWalletAsync(userId);
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
                var wallet = await GetOrCreateWalletAsync(userId);
                wallet.Balance += amount;
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
                var wallet = await GetOrCreateWalletAsync(userId);

                if (wallet.Balance < amount)
                    throw new InvalidOperationException("الرصيد غير كافي");

                wallet.Balance -= amount;
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
                var fromWallet = await GetOrCreateWalletAsync(fromUserId);
                var toWallet = await GetOrCreateWalletAsync(toUserId);

                if (fromWallet.Balance < amount)
                    throw new InvalidOperationException("الرصيد غير كافي");

                fromWallet.Balance -= amount;
                fromWallet.UpdatedAt = DateTime.UtcNow;
                toWallet.Balance += amount;
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
            string receiptImagePath)
        {
            if (amount <= 0)
                throw new InvalidOperationException("المبلغ يجب ان يكون اكبر من صفر");

            await RequireApprovedKyc(userId);

            var request = new DepositRequest
            {
                UserId = userId,
                Amount = amount,
                SourceWalletNumber = sourceWalletNumber,
                ReceiptImagePath = receiptImagePath,
                Status = "pending"
            };

            _context.DepositRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task<List<DepositRequest>> GetPendingDepositsAsync(int pageNumber = 1, int pageSize = 20)
        {
            return await _context.DepositRequests
                .Include(r => r.User)
                .Where(r => r.Status == "pending")
                .OrderBy(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<DepositRequest> ReviewDepositRequestAsync(
            int depositId,
            string adminUserId,
            string status,
            string? rejectionReason)
        {
            var request = await _context.DepositRequests
                .Include(r => r.User)
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
                    var wallet = await GetOrCreateWalletAsync(request.UserId);
                    wallet.Balance += request.Amount;
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
                var wallet = await GetOrCreateWalletAsync(userId);
                if (wallet.Balance < amount)
                    throw new InvalidOperationException("الرصيد غير كافي لطلب السحب");

                // Lock/deduct the funds immediately upon request to prevent double-spending
                wallet.Balance -= amount;
                wallet.UpdatedAt = DateTime.UtcNow;

                var request = new WithdrawRequest
                {
                    UserId = userId,
                    Amount = amount,
                    DestinationWalletNumber = destinationWalletNumber,
                    WalletOwnerName = walletOwnerName,
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

        public async Task<List<WithdrawRequest>> GetPendingWithdrawalsAsync(int pageNumber = 1, int pageSize = 20)
        {
            return await _context.WithdrawRequests
                .Include(r => r.User)
                .Where(r => r.Status == "pending")
                .OrderBy(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<WithdrawRequest> ReviewWithdrawRequestAsync(
            int withdrawId,
            string adminUserId,
            string status,
            string? rejectionReason)
        {
            var request = await _context.WithdrawRequests
                .Include(r => r.User)
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
                    var wallet = await GetOrCreateWalletAsync(request.UserId);
                    wallet.Balance += request.Amount;
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
                await _notificationService.SendNotificationToUser(userId, title, body);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send notification to {UserId}: {Title}", userId, title);
            }
        }
    }
}
