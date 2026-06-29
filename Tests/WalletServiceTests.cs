using Xunit;
using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Data;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Linq;

using Moq;
using EgyptOnline.Services;

namespace EgyptOnline.Tests
{
    public class WalletServiceTests
    {
        private readonly ApplicationDbContext _context;
        private readonly WalletService _service;

        public WalletServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new ApplicationDbContext(options);
            var mockNotificationService = new Mock<INotificationService>();
            _service = new WalletService(_context, mockNotificationService.Object);
        }

        private async Task SeedUserWithApprovedKyc(string userId)
        {
            var user = new User { Id = userId, UserName = userId, Email = $"{userId}@test.com", Governorate = "Cairo", City = "Cairo" };
            _context.Users.Add(user);
            
            var kyc = new KycSubmission 
            { 
                UserId = userId, 
                Status = "approved", 
                SubmittedAt = DateTime.UtcNow.AddDays(-1) 
            };
            _context.KycSubmissions.Add(kyc);
            
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetOrCreateWallet_ShouldCreateNewWallet_IfNoneExists()
        {
            // Arrange
            string userId = "user1";

            // Act
            var wallet = await _service.GetOrCreateWalletAsync(userId);

            // Assert
            Assert.NotNull(wallet);
            Assert.Equal(userId, wallet.UserId);
            Assert.Equal(0, wallet.Balance);
            
            var dbWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == userId);
            Assert.NotNull(dbWallet);
        }

        [Fact]
        public async Task Deposit_WithApprovedKyc_ShouldIncreaseBalance()
        {
            // Arrange
            string userId = "user2";
            await SeedUserWithApprovedKyc(userId);
            decimal depositAmount = 100.50m;

            // Act
            var wallet = await _service.DepositAsync(userId, depositAmount);

            // Assert
            Assert.Equal(depositAmount, wallet.Balance);
            
            var transaction = await _context.WalletTransactions.FirstOrDefaultAsync(t => t.UserId == userId);
            Assert.NotNull(transaction);
            Assert.Equal("deposit", transaction.Type);
            Assert.Equal(depositAmount, transaction.Amount);
        }

        [Fact]
        public async Task Deposit_WithoutApprovedKyc_ShouldThrow()
        {
            // Arrange
            string userId = "user3";
            var user = new User { Id = userId, UserName = userId, Email = "u3@test.com", Governorate = "Cairo", City = "Cairo" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DepositAsync(userId, 50m));
        }

        [Fact]
        public async Task Withdraw_WithSufficientBalance_ShouldDecreaseBalance()
        {
            // Arrange
            string userId = "user4";
            await SeedUserWithApprovedKyc(userId);
            await _service.DepositAsync(userId, 500m);

            // Act
            var wallet = await _service.WithdrawAsync(userId, 200m);

            // Assert
            Assert.Equal(300m, wallet.Balance);
            
            var transaction = await _context.WalletTransactions.OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync(t => t.UserId == userId);
            Assert.Equal("withdraw", transaction!.Type);
            Assert.Equal(200m, transaction.Amount);
        }

        [Fact]
        public async Task Withdraw_WithInsufficientBalance_ShouldThrow()
        {
            // Arrange
            string userId = "user5";
            await SeedUserWithApprovedKyc(userId);
            await _service.DepositAsync(userId, 50m);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.WithdrawAsync(userId, 100m));
        }

        [Fact]
        public async Task Transfer_BetweenUsers_ShouldUpdateBothBalances()
        {
            // Arrange
            string fromUserId = "sender";
            string toUserId = "receiver";
            
            await SeedUserWithApprovedKyc(fromUserId);
            var toUser = new User { Id = toUserId, UserName = toUserId, Email = "to@test.com", Governorate = "Cairo", City = "Cairo" };
            _context.Users.Add(toUser);
            await _context.SaveChangesAsync();

            await _service.DepositAsync(fromUserId, 1000m);

            // Act
            var (fromWallet, toWallet) = await _service.TransferAsync(fromUserId, toUserId, 400m);

            // Assert
            Assert.Equal(600m, fromWallet.Balance);
            Assert.Equal(400m, toWallet.Balance);

            var transactions = await _context.WalletTransactions.Where(t => t.FromUserId == fromUserId && t.ToUserId == toUserId).ToListAsync();
            Assert.Equal(2, transactions.Count);
            Assert.Contains(transactions, t => t.Type == "transfer_out" && t.UserId == fromUserId);
            Assert.Contains(transactions, t => t.Type == "transfer_in" && t.UserId == toUserId);
        }

        [Fact]
        public async Task Transfer_ToNonExistentUser_ShouldThrow()
        {
            // Arrange
            string fromUserId = "sender2";
            await SeedUserWithApprovedKyc(fromUserId);
            await _service.DepositAsync(fromUserId, 100m);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.TransferAsync(fromUserId, "non-existent", 50m));
        }

        [Fact]
        public async Task GetTransactionHistory_ShouldReturnCorrectEntries()
        {
            // Arrange
            string userId = "user6";
            await SeedUserWithApprovedKyc(userId);
            await _service.DepositAsync(userId, 100m);
            await _service.DepositAsync(userId, 200m);

            // Act
            var history = await _service.GetTransactionHistoryAsync(userId);

            // Assert
            Assert.Equal(2, history.Count);
            Assert.Equal(200m, history[0].Amount); // Descending order
            Assert.Equal(100m, history[1].Amount);
        }
    }
}
