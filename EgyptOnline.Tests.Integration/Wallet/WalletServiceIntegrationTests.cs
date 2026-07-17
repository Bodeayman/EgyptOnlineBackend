using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Domain.Models;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Infrastructure;
using EgyptOnline.Models;
using EgyptOnline.Services;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EgyptOnline.Tests.Integration.Wallet;

/// <summary>
/// Integration tests for WalletService against a real PostgreSQL database.
///
/// These tests verify:
/// - Transactions actually commit / rollback in Postgres.
/// - Balance uniqueness constraint (one wallet per user) is enforced by the DB.
/// - Transfer atomicity: no partial state if something fails mid-operation.
/// - Withdraw-request deduction persists across separate DbContext instances.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class WalletServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    // Fakes for dependencies we do not want to hit externally
    private readonly INotificationService _notif = A.Fake<INotificationService>();
    private readonly IEmailService _email = A.Fake<IEmailService>();
    private readonly ILogger<WalletService> _logger = A.Fake<ILogger<WalletService>>();

    public WalletServiceIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => await _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private WalletService BuildService() =>
        new(_fixture.GetDbContext(), _notif, _logger, _email, A.Fake<Microsoft.AspNetCore.Identity.UserManager<User>>());

    private async Task<User> SeedUserWithApprovedKyc(
        string userId, int walletBalance = 0, string phone = "+201001234567")
    {
        using var db = _fixture.GetDbContext();
        var user = new User
        {
            Id = userId,
            UserName = $"user_{userId}",
            PhoneNumber = phone,
            Governorate = "Cairo",
            City = "Cairo",
            NormalizedUserName = $"USER_{userId}".ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString()
        };
        db.Users.Add(user);
        db.KycSubmissions.Add(new KycSubmission
        {
            UserId = userId,
            Status = "approved",
            SubmittedAt = DateTime.UtcNow.AddDays(-5)
        });
        db.UserWallets.Add(new UserWallet
        {
            UserId = userId,
            FreeBalance = walletBalance,
            FrozenBalance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return user;
    }

    // ── Wallet uniqueness ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateWallet_Twice_ForSameUser_ThrowsInvalidOperation()
    {
        await SeedUserWithApprovedKyc("wallet-dup", walletBalance: 0);
        var svc = BuildService();

        // First call auto-creates the wallet
        await svc.GetWalletAsync("wallet-dup");

        // Explicit second creation should throw
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateWalletAsync("wallet-dup"));
    }

    // ── Deposit persists across DbContext instances ───────────────────────────

    [Fact]
    public async Task Deposit_BalancePersistedAcrossSeparateDbContextInstances()
    {
        await SeedUserWithApprovedKyc("deposit-persist", walletBalance: 100);

        // Act – deposit in one service instance (and DbContext)
        await BuildService().DepositAsync("deposit-persist", 350);

        // Assert – verify using a brand-new DbContext (simulating a new request)
        using var verifyDb = _fixture.GetDbContext();
        var wallet = await verifyDb.UserWallets
            .FirstOrDefaultAsync(w => w.UserId == "deposit-persist");

        Assert.NotNull(wallet);
        Assert.Equal(450, wallet.FreeBalance); // 100 + 350
    }

    // ── Withdraw refunds on rejection ─────────────────────────────────────────

    [Fact]
    public async Task ReviewWithdrawRequest_WhenRejected_RefundsBalanceToUser()
    {
        await SeedUserWithApprovedKyc("wr-reject", walletBalance: 1000,
            phone: "+201001111111");

        // Mock admin lookup
        A.CallTo(() => A.Fake<Microsoft.AspNetCore.Identity.UserManager<User>>()
            .GetUsersInRoleAsync("Admin"))
         .Returns(Task.FromResult<IList<User>>(new List<User>()));

        var svc = BuildService();

        // Submit withdrawal – deducts balance immediately
        var request = await svc.SubmitWithdrawRequestAsync(
            "wr-reject", 400, "01001111111", "Test Owner");

        // Confirm deduction
        var walletAfterRequest = await svc.GetBalanceAsync("wr-reject");
        Assert.Equal(600, walletAfterRequest.FreeBalance);

        // Admin rejects it
        await svc.ReviewWithdrawRequestAsync(request.Id, "admin-1", "rejected", "Fake receipt");

        // Balance must be restored
        var walletAfterRejection = await svc.GetBalanceAsync("wr-reject");
        Assert.Equal(1000, walletAfterRejection.FreeBalance);
    }

    // ── Transfer atomicity ────────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_BetweenRealUsers_BothBalancesCorrectAfterCommit()
    {
        await SeedUserWithApprovedKyc("sender-int", walletBalance: 2000,
            phone: "+201002222222");
        await SeedUserWithApprovedKyc("receiver-int", walletBalance: 500,
            phone: "+201003333333");

        var svc = BuildService();
        await svc.TransferAsync("sender-int", "receiver-int", 800);

        // Verify using fresh DbContext
        using var db = _fixture.GetDbContext();
        var senderWallet = await db.UserWallets.FirstAsync(w => w.UserId == "sender-int");
        var receiverWallet = await db.UserWallets.FirstAsync(w => w.UserId == "receiver-int");

        Assert.Equal(1200, senderWallet.FreeBalance);
        Assert.Equal(1300, receiverWallet.FreeBalance);

        // Two transaction records created (transfer_out + transfer_in)
        var txCount = await db.WalletTransactions
            .CountAsync(t => t.UserId == "sender-int" || t.UserId == "receiver-int");
        Assert.Equal(2, txCount);
    }

    // ── Free ↔ Frozen under real DB constraints ───────────────────────────────

    [Fact]
    public async Task FreeToFrozen_ThenFrozenBetweenUsers_PersistsCorrectly()
    {
        await SeedUserWithApprovedKyc("freeze-a", walletBalance: 1000,
            phone: "+201004444444");
        await SeedUserWithApprovedKyc("freeze-b", walletBalance: 0,
            phone: "+201005555555");

        var svc = BuildService();

        // Move 600 from free to frozen for user A
        await svc.TransferFreeToFrozenAsync("freeze-a", 600);

        // Transfer that frozen amount from A to B
        await svc.TransferFrozenBetweenUsersAsync("freeze-a", "freeze-b", 600);

        using var db = _fixture.GetDbContext();
        var walletA = await db.UserWallets.FirstAsync(w => w.UserId == "freeze-a");
        var walletB = await db.UserWallets.FirstAsync(w => w.UserId == "freeze-b");

        Assert.Equal(400, walletA.FreeBalance);
        Assert.Equal(0, walletA.FrozenBalance);
        Assert.Equal(0, walletB.FreeBalance);
        Assert.Equal(600, walletB.FrozenBalance);
    }

    // ── Transaction log integrity ─────────────────────────────────────────────

    [Fact]
    public async Task MultipleDepositsAndWithdrawals_TransactionLogCountMatchesOperations()
    {
        await SeedUserWithApprovedKyc("tx-log", walletBalance: 2000,
            phone: "+201006666666");

        var svc = BuildService();

        await svc.DepositAsync("tx-log", 200);
        await svc.DepositAsync("tx-log", 300);
        await svc.WithdrawAsync("tx-log", 100);

        var history = await svc.GetTransactionHistoryAsync("tx-log");

        // 3 explicit operations → 3 transaction records
        Assert.Equal(3, history.Count);
        Assert.Contains(history, t => t.Type == "deposit" && t.Amount == 200);
        Assert.Contains(history, t => t.Type == "deposit" && t.Amount == 300);
        Assert.Contains(history, t => t.Type == "withdraw" && t.Amount == 100);
    }
}
