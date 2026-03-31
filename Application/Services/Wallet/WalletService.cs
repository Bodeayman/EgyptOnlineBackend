using EgyptOnline.Data;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EgyptOnline.Application.Services.Wallet
{
    public class WalletService
    {
        private readonly ApplicationDbContext _context;

        public WalletService(ApplicationDbContext context)
        {
            _context = context;
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
                    Description = "ايداع رصيد"
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
                    Description = "سحب رصيد"
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
    }
}
