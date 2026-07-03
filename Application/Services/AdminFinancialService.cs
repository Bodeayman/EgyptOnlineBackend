using EgyptOnline.Data;
using EgyptOnline.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgyptOnline.Application.Services
{
    public class AdminFinancialService
    {
        private readonly ApplicationDbContext _context;
        private readonly WalletService _walletService;
        private readonly ILogger<AdminFinancialService> _logger;

        public AdminFinancialService(
            ApplicationDbContext context,
            WalletService walletService,
            ILogger<AdminFinancialService> logger)
        {
            _context = context;
            _walletService = walletService;
            _logger = logger;
        }

        #region Deposit Operations

        public async Task AdminApproveDepositAsync(string phoneNumber, decimal amount, string reference, string adminUserId)
        {
            if (amount <= 0)
                throw new InvalidOperationException("Deposit amount must be positive");

            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new InvalidOperationException("Phone number is required");

            if (string.IsNullOrWhiteSpace(reference))
                throw new InvalidOperationException("Reference is required");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            if (user == null)
                throw new InvalidOperationException($"User not found with phone number: {phoneNumber}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _walletService.AdminDepositAsync(phoneNumber, amount, reference);

                await transaction.CommitAsync();

                _logger.LogInformation("Admin {AdminUserId} approved deposit of {Amount} to phone {PhoneNumber}. Reference: {Reference}",
                    adminUserId, amount, phoneNumber, reference);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Withdrawal Operations

        public async Task<bool> ValidateWithdrawalRequestAsync(string userId, decimal amount)
        {
            if (amount <= 0)
                return false;

            return await _walletService.CanInitiateWithdrawalAsync(userId, amount);
        }

        public async Task AdminCompleteWithdrawalAsync(string userId, decimal amount, string reference, string adminUserId)
        {
            if (amount <= 0)
                throw new InvalidOperationException("Withdrawal amount must be positive");

            if (string.IsNullOrWhiteSpace(reference))
                throw new InvalidOperationException("Reference is required");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new InvalidOperationException($"User not found with ID: {userId}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _walletService.AdminCompleteWithdrawalAsync(userId, amount, reference);

                await transaction.CommitAsync();

                _logger.LogInformation("Admin {AdminUserId} completed withdrawal of {Amount} for user {UserId}. Reference: {Reference}",
                    adminUserId, amount, userId, reference);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task AdminRejectWithdrawalAsync(string userId, decimal amount, string reason, string adminUserId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Rejection reason is required");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new InvalidOperationException($"User not found with ID: {userId}");

            _logger.LogWarning("Admin {AdminUserId} rejected withdrawal of {Amount} for user {UserId}. Reason: {Reason}",
                adminUserId, amount, userId, reason);
        }

        #endregion

        #region Balance Override Operations

        public async Task AdminOverrideBalanceAsync(string userId, decimal newFreeBalance, decimal newFrozenBalance, string reason, string adminUserId)
        {
            if (newFreeBalance < 0)
                throw new InvalidOperationException("Free balance cannot be negative");

            if (newFrozenBalance < 0)
                throw new InvalidOperationException("Frozen balance cannot be negative");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Reason is required");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new InvalidOperationException($"User not found with ID: {userId}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _walletService.AdminOverrideBalanceAsync(userId, newFreeBalance, newFrozenBalance, reason);

                await transaction.CommitAsync();

                _logger.LogWarning("Admin {AdminUserId} overrode balance for user {UserId}. New Free: {NewFree}, New Frozen: {NewFrozen}. Reason: {Reason}",
                    adminUserId, userId, newFreeBalance, newFrozenBalance, reason);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Audit Operations

        public async Task<Dictionary<string, decimal>> GetSystemBalanceSummaryAsync()
        {
            var wallets = await _context.UserWallets.ToListAsync();

            var summary = new Dictionary<string, decimal>
            {
                ["TotalFreeBalance"] = wallets.Sum(w => w.FreeBalance),
                ["TotalFrozenBalance"] = wallets.Sum(w => w.FrozenBalance),
                ["TotalBalance"] = wallets.Sum(w => w.FreeBalance + w.FrozenBalance),
                ["TotalWallets"] = wallets.Count
            };

            _logger.LogInformation("System balance summary retrieved: {Summary}", summary);

            return summary;
        }

        #endregion
    }
}
