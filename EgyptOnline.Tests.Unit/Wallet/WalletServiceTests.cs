using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Dtos.Wallet;
using EgyptOnline.Domain.Models;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Infrastructure;
using EgyptOnline.Models;
using EgyptOnline.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using System.Linq;
using Xunit;

namespace EgyptOnline.Tests.Unit.Wallet;

/// <summary>
/// Unit tests for WalletService.
///
/// Strategy: use in-memory DB for state, fake all external I/O
/// (notifications, email, UserManager). Tests focus on business
/// invariants — balance consistency, KYC gate, double-spend
/// prevention — things that can silently break in production.
/// </summary>
[Trait("Category", "Unit")]
public class WalletServiceTests : UnitTestBase
{
    private readonly INotificationService _notifFake = A.Fake<INotificationService>();
    private readonly IEmailService _emailFake = A.Fake<IEmailService>();
    private readonly ILogger<WalletService> _loggerFake = A.Fake<ILogger<WalletService>>();

    private WalletService BuildService() =>
        new(Context, _notifFake, _loggerFake, _emailFake, UserManagerFake);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<User> SeedUserWithApprovedKyc(string userId = "user-1", int walletBalance = 0)
    {
        var user = new User
        {
            Id = userId,
            UserName = $"testuser_{userId}",
            PhoneNumber = "+201001234567",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        Context.KycSubmissions.Add(new KycSubmission
        {
            UserId = userId,
            Status = "approved",
            SubmittedAt = DateTime.UtcNow.AddDays(-3)
        });

        Context.UserWallets.Add(new UserWallet
        {
            UserId = userId,
            FreeBalance = walletBalance,
            FrozenBalance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await Context.SaveChangesAsync();
        return user;
    }

    // ── KYC Gate ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deposit_WithoutApprovedKyc_ThrowsBeforeBalanceChanges()
    {
        // Arrange – user exists but has NO KYC submission at all
        var user = new User
        {
            Id = "user-no-kyc",
            UserName = "nokyc",
            PhoneNumber = "+201001234567",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);
        Context.UserWallets.Add(new UserWallet
        {
            UserId = user.Id,
            FreeBalance = 500,
            FrozenBalance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var svc = BuildService();

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DepositAsync(user.Id, 100));

        // Assert – wallet balance untouched
        Assert.Contains("KYC", ex.Message);
        var wallet = await svc.GetBalanceAsync(user.Id);
        Assert.Equal(500, wallet.FreeBalance);
    }

    [Fact]
    public async Task Deposit_WithPendingKyc_ThrowsBeforeBalanceChanges()
    {
        // Arrange – KYC submitted but still pending
        var user = new User
        {
            Id = "user-pending-kyc",
            UserName = "pendingkyc",
            PhoneNumber = "+201001234567",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);
        Context.KycSubmissions.Add(new KycSubmission
        {
            UserId = user.Id,
            Status = "pending",
            SubmittedAt = DateTime.UtcNow.AddHours(-1)
        });
        Context.UserWallets.Add(new UserWallet
        {
            UserId = user.Id,
            FreeBalance = 200,
            FrozenBalance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var svc = BuildService();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DepositAsync(user.Id, 100));

        var wallet = await svc.GetBalanceAsync(user.Id);
        Assert.Equal(200, wallet.FreeBalance); // unchanged
    }

    // ── Withdrawal Guard ──────────────────────────────────────────────────────

    [Fact]
    public async Task Withdraw_MoreThanFreeBalance_ThrowsAndLeavesBalanceIntact()
    {
        // Arrange
        await SeedUserWithApprovedKyc("user-withdraw", walletBalance: 300);
        var svc = BuildService();

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.WithdrawAsync("user-withdraw", 500)); // 500 > 300

        // Assert
        Assert.Contains("كافي", ex.Message); // "insufficient"
        var wallet = await svc.GetBalanceAsync("user-withdraw");
        Assert.Equal(300, wallet.FreeBalance); // balance must be unchanged
    }

    [Fact]
    public async Task Withdraw_ExactBalance_LeavesZeroFreeBalance()
    {
        // Arrange – exact match should succeed
        await SeedUserWithApprovedKyc("user-exact", walletBalance: 500);
        var svc = BuildService();

        // Act
        var result = await svc.WithdrawAsync("user-exact", 500);

        // Assert
        Assert.Equal(0, result.FreeBalance);
    }

    [Fact]
    public async Task Withdraw_ZeroAmount_ThrowsImmediately()
    {
        await SeedUserWithApprovedKyc("user-zero", walletBalance: 1000);
        var svc = BuildService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.WithdrawAsync("user-zero", 0));

        Assert.Contains("صفر", ex.Message);
    }

    // ── Self-Transfer Guard ───────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_ToSelf_ThrowsWithoutAffectingBalance()
    {
        await SeedUserWithApprovedKyc("user-self", walletBalance: 1000);
        var svc = BuildService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.TransferAsync("user-self", "user-self", 100));

        Assert.Contains("نفس", ex.Message); // "same wallet"
        var wallet = await svc.GetBalanceAsync("user-self");
        Assert.Equal(1000, wallet.FreeBalance); // unchanged
    }

    // ── Transfer Atomicity ────────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_ValidAmount_DebitsSenderAndCreditsRecipientAtomically()
    {
        // Arrange
        await SeedUserWithApprovedKyc("sender", walletBalance: 1000);
        var recipient = new User
        {
            Id = "recipient",
            UserName = "recipient_user",
            PhoneNumber = "+201009876543",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(recipient);
        Context.UserWallets.Add(new UserWallet
        {
            UserId = "recipient",
            FreeBalance = 200,
            FrozenBalance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Context.KycSubmissions.Add(new KycSubmission
        {
            UserId = "recipient",
            Status = "approved",
            SubmittedAt = DateTime.UtcNow.AddDays(-3)
        });
        await Context.SaveChangesAsync();

        var svc = BuildService();

        // Act
        var (fromWallet, toWallet) = await svc.TransferAsync("sender", "recipient", 400);

        // Assert – net balance preserved (1000 + 200 = 1200 = 600 + 600)
        Assert.Equal(600, fromWallet.FreeBalance);
        Assert.Equal(600, toWallet.FreeBalance);
    }

    [Fact]
    public async Task Transfer_ToNonExistentRecipient_ThrowsWithoutDebitingSender()
    {
        await SeedUserWithApprovedKyc("sender-2", walletBalance: 800);
        var svc = BuildService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.TransferAsync("sender-2", "ghost-user-id", 100));

        // Sender balance must be untouched
        var wallet = await svc.GetBalanceAsync("sender-2");
        Assert.Equal(800, wallet.FreeBalance);
    }

    // ── Free ↔ Frozen Balance ─────────────────────────────────────────────────

    [Fact]
    public async Task TransferFreeToFrozen_ReducesFreeAndIncreasesFrozenByExactAmount()
    {
        await SeedUserWithApprovedKyc("user-freeze", walletBalance: 1000);
        var svc = BuildService();

        await svc.TransferFreeToFrozenAsync("user-freeze", 300);

        var wallet = await svc.GetBalanceAsync("user-freeze");
        Assert.Equal(700, wallet.FreeBalance);
        Assert.Equal(300, wallet.FrozenBalance);
    }

    [Fact]
    public async Task TransferFreeToFrozen_InsufficientFree_ThrowsAndRollsBackBothBalances()
    {
        await SeedUserWithApprovedKyc("user-freeze-fail", walletBalance: 100);
        var svc = BuildService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.TransferFreeToFrozenAsync("user-freeze-fail", 500));

        var wallet = await svc.GetBalanceAsync("user-freeze-fail");
        Assert.Equal(100, wallet.FreeBalance);
        Assert.Equal(0, wallet.FrozenBalance);
    }

    [Fact]
    public async Task TransferFrozenToFree_RestoresBalanceCorrectly()
    {
        // Arrange – start with frozen balance
        await SeedUserWithApprovedKyc("user-unfreeze", walletBalance: 0);
        Context.UserWallets.First(w => w.UserId == "user-unfreeze").FrozenBalance = 500;
        await Context.SaveChangesAsync();

        var svc = BuildService();

        await svc.TransferFrozenToFreeAsync("user-unfreeze", 200);

        var wallet = await svc.GetBalanceAsync("user-unfreeze");
        Assert.Equal(200, wallet.FreeBalance);
        Assert.Equal(300, wallet.FrozenBalance);
    }

    // ── Self-Healing Wallet ───────────────────────────────────────────────────

    [Fact]
    public async Task GetBalance_ForUserWithoutWallet_AutoCreatesWalletWithZeroBalance()
    {
        // Arrange – user exists but no wallet row
        var user = new User
        {
            Id = "user-no-wallet",
            UserName = "nowallet",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var svc = BuildService();

        // Act
        var wallet = await svc.GetBalanceAsync("user-no-wallet");

        // Assert – wallet auto-created with zero balance
        Assert.NotNull(wallet);
        Assert.Equal(0, wallet.FreeBalance);
        Assert.Equal(0, wallet.FrozenBalance);
    }

    // ── Transaction History ───────────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionHistory_OnlyReturnsTransactionsForRequestedUser()
    {
        // Arrange – two users, each with their own transactions
        await SeedUserWithApprovedKyc("tx-user-a", 1000);
        await SeedUserWithApprovedKyc("tx-user-b", 1000);

        var svc = BuildService();
        await svc.WithdrawAsync("tx-user-a", 100);
        await svc.WithdrawAsync("tx-user-b", 200);
        await svc.WithdrawAsync("tx-user-a", 50);

        // Act
        var history = await svc.GetTransactionHistoryAsync("tx-user-a");

        // Assert – only user-a's 2 withdrawals, not user-b's
        Assert.Equal(2, history.Count);
        Assert.All(history, tx => Assert.Equal("tx-user-a", tx.UserId));
    }

    // ── Withdraw Request: balance deducted on submission ─────────────────────

    [Fact]
    public async Task SubmitWithdrawRequest_DeductsBalanceImmediatelyToPreventDoubleSpend()
    {
        // Arrange
        await SeedUserWithApprovedKyc("wr-user", 1000);
        A.CallTo(() => UserManagerFake.GetUsersInRoleAsync("Admin"))
         .Returns(Task.FromResult<IList<EgyptOnline.Models.User>>(new List<EgyptOnline.Models.User>()));

        var svc = BuildService();

        // Act – submit a withdrawal request
        await svc.SubmitWithdrawRequestAsync("wr-user", 400, "01001234567", "Test Owner");

        // Assert – funds locked immediately, not on approval
        var wallet = await svc.GetBalanceAsync("wr-user");
        Assert.Equal(600, wallet.FreeBalance); // 1000 - 400
    }

    [Fact]
    public async Task SubmitWithdrawRequest_WithInvalidPhoneNumber_ThrowsBeforeDeductingBalance()
    {
        await SeedUserWithApprovedKyc("wr-invalid-phone", 1000);
        var svc = BuildService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitWithdrawRequestAsync("wr-invalid-phone", 100, "not-a-phone", "Owner"));

        Assert.Contains("رقم", ex.Message);
        var wallet = await svc.GetBalanceAsync("wr-invalid-phone");
        Assert.Equal(1000, wallet.FreeBalance); // unchanged
    }

    // ── Balance Audit Tests ───────────────────────────────────────────────────

    [Fact]
    public async Task GetBalanceTransactionsAsync_ReturnsAllTransactions_WhenNoFilterProvided()
    {
        // Arrange
        await SeedUserWithApprovedKyc("audit-user-1", 1000);
        await SeedUserWithApprovedKyc("audit-user-2", 2000);

        var svc = BuildService();
        await svc.DepositAsync("audit-user-1", 100);
        await svc.WithdrawAsync("audit-user-2", 50);

        // Act
        var filter = new BalanceAuditQueryFilter();
        var result = await svc.GetBalanceTransactionsAsync(filter, 1, 20);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.Equal(2, result.TotalCount); // 2 transactions (1 deposit, 1 withdrawal)
    }

    [Fact]
    public async Task GetBalanceTransactionsAsync_FiltersByUserId()
    {
        // Arrange
        await SeedUserWithApprovedKyc("filter-user-1", 1000);
        await SeedUserWithApprovedKyc("filter-user-2", 2000);

        var svc = BuildService();
        await svc.DepositAsync("filter-user-1", 100);
        await svc.DepositAsync("filter-user-2", 200);

        // Act
        var filter = new BalanceAuditQueryFilter { UserId = "filter-user-1" };
        var result = await svc.GetBalanceTransactionsAsync(filter, 1, 20);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("filter-user-1", result.Items[0].UserId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetBalanceTransactionsAsync_FiltersByBalanceType()
    {
        // Arrange
        await SeedUserWithApprovedKyc("baltype-user", 1000);

        var svc = BuildService();
        await svc.DepositAsync("baltype-user", 100); // Free balance
        await svc.TransferFreeToFrozenAsync("baltype-user", 50); // Frozen balance

        // Act
        var filter = new BalanceAuditQueryFilter { BalanceType = BalanceType.Frozen };
        var result = await svc.GetBalanceTransactionsAsync(filter, 1, 20);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(BalanceType.Frozen, result.Items[0].BalanceType);
    }

    [Fact]
    public async Task GetBalanceTransactionsAsync_FiltersByOperationType()
    {
        // Arrange
        await SeedUserWithApprovedKyc("optype-user", 1000);

        var svc = BuildService();
        await svc.DepositAsync("optype-user", 100);
        await svc.WithdrawAsync("optype-user", 50);

        // Act
        var filter = new BalanceAuditQueryFilter { OperationType = OperationType.Deposit };
        var result = await svc.GetBalanceTransactionsAsync(filter, 1, 20);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(OperationType.Deposit, result.Items[0].OperationType);
    }

    [Fact]
    public async Task GetBalanceTransactionsAsync_FiltersByDateRange()
    {
        // Arrange
        await SeedUserWithApprovedKyc("date-user", 1000);

        var svc = BuildService();
        await svc.DepositAsync("date-user", 100);

        var fromDate = DateTime.UtcNow.AddMinutes(-5);
        var toDate = DateTime.UtcNow.AddMinutes(5);

        // Act
        var filter = new BalanceAuditQueryFilter { From = fromDate, To = toDate };
        var result = await svc.GetBalanceTransactionsAsync(filter, 1, 20);

        // Assert
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetBalanceTransactionsAsync_PaginationWorksCorrectly()
    {
        // Arrange
        await SeedUserWithApprovedKyc("page-user", 1000);

        var svc = BuildService();
        for (int i = 0; i < 5; i++)
        {
            await svc.DepositAsync("page-user", 10);
        }

        // Act - page 1 with page size 2
        var filter = new BalanceAuditQueryFilter { UserId = "page-user" };
        var page1 = await svc.GetBalanceTransactionsAsync(filter, 1, 2);

        // Assert
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);

        // Act - page 2
        var page2 = await svc.GetBalanceTransactionsAsync(filter, 2, 2);

        // Assert
        Assert.Equal(2, page2.Items.Count);
        Assert.True(page2.HasNextPage);
        Assert.True(page2.HasPreviousPage);
    }

    [Fact]
    public async Task GetBalanceTransactionsAsync_ReturnsBalanceBeforeAndAfter()
    {
        // Arrange
        await SeedUserWithApprovedKyc("balance-track-user", 1000);

        var svc = BuildService();
        await svc.DepositAsync("balance-track-user", 100);

        // Act
        var filter = new BalanceAuditQueryFilter { UserId = "balance-track-user" };
        var result = await svc.GetBalanceTransactionsAsync(filter, 1, 20);

        // Assert
        var transaction = result.Items.First();
        Assert.Equal(1000, transaction.BalanceBefore);
        Assert.Equal(1100, transaction.BalanceAfter);
        Assert.Equal(100, transaction.Amount);
    }

    [Fact]
    public async Task GetBalanceTransactionsAsync_IncludesUserData()
    {
        // Arrange
        var user = await SeedUserWithApprovedKyc("userdata-user", 1000);

        var svc = BuildService();
        await svc.DepositAsync("userdata-user", 100);

        // Act
        var filter = new BalanceAuditQueryFilter { UserId = "userdata-user" };
        var result = await svc.GetBalanceTransactionsAsync(filter, 1, 20);

        // Assert
        var transaction = result.Items.First();
        Assert.Equal("userdata-user", transaction.UserId);
        Assert.NotEmpty(transaction.UserName);
        Assert.NotEmpty(transaction.UserPhone);
    }
}
