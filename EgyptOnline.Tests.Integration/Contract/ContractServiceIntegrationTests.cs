using EgyptOnline.Application.Services.Contract;
using EgyptOnline.Application.Services.Complaint;
using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Data;
using EgyptOnline.Domain.Models;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Infrastructure;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EgyptOnline.Tests.Integration.Contract;

/// <summary>
/// Integration tests for ContractService against a real PostgreSQL database.
///
/// These tests verify:
/// - Balance locking / unlocking in transactions.
/// - ContractDay generation and relations.
/// - Dispute reporting with associated Complaint file creation.
/// - Admin adjusting and resuming under real SQL constraints.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ContractServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    private readonly INotificationService _notif = A.Fake<INotificationService>();
    private readonly IEmailService _email = A.Fake<IEmailService>();
    private readonly ILogger<WalletService> _walletLogger = A.Fake<ILogger<WalletService>>();
    private readonly ILogger<ContractService> _contractLogger = A.Fake<ILogger<ContractService>>();
    private readonly ILogger<ComplaintService> _complaintLogger = A.Fake<ILogger<ComplaintService>>();

    public ContractServiceIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => await _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ContractService BuildService(DbContext db)
    {
        var appDb = (ApplicationDbContext)db;
        var wallet = new WalletService(appDb, _notif, _walletLogger, _email, A.Fake<Microsoft.AspNetCore.Identity.UserManager<User>>());
        var complaint = new ComplaintService(appDb, _notif, _email, A.Fake<Microsoft.AspNetCore.Identity.UserManager<User>>(), wallet);
        return new ContractService(appDb, _notif, wallet, complaint, _contractLogger);
    }

    private async Task<User> SeedUser(string userId, string phone, int walletBalance = 0)
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

    private async Task<Models.Contract> SeedContract(
        string clientId, string providerPhone,
        string status = "pending",
        int totalDays = 3,
        int dailySalary = 200,
        int penalty = 100,
        int clientBalance = 0,
        int providerBalance = 0)
    {
        await SeedUser(clientId, "+201000000001", clientBalance);
        await SeedUser("provider-" + clientId, providerPhone, providerBalance);

        using var db = _fixture.GetDbContext();
        var contract = new Models.Contract
        {
            ClientUserId = clientId,
            ServiceProviderPhoneNumber = providerPhone,
            StartDate = DateTime.UtcNow.AddDays(-totalDays).Date,
            ShiftStartTime = TimeSpan.FromHours(8),
            ShiftEndTime = TimeSpan.FromHours(16),
            TotalDays = totalDays,
            DailySalary = dailySalary,
            TotalAmount = totalDays * dailySalary,
            PenaltyAmount = penalty,
            Status = status,
            Governorate = "Cairo",
            City = "Cairo",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var days = Enumerable.Range(1, totalDays).Select(d => new ContractDay
        {
            ContractId = contract.Id,
            DayNumber = d,
            Date = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-totalDays + d - 1).Date, DateTimeKind.Utc),
            ProviderArrived = false,
            Status = ContractDayStatus.Pending,
            IsProcessed = false
        }).ToList();

        db.ContractDays.AddRange(days);
        await db.SaveChangesAsync();
        return contract;
    }

    // ── Creation & Balance Freezing Integration ───────────────────────────────

    [Fact]
    public async Task CreateContract_FreezesClientFunds_PersistsAcrossContexts()
    {
        await SeedUser("client-i1", "+201700000001", walletBalance: 1200);
        await SeedUser("provider-i1", "+201700000002", walletBalance: 0);

        var contract = new Models.Contract
        {
            ClientUserId = "client-i1",
            ServiceProviderPhoneNumber = "+201700000002",
            StartDate = DateTime.UtcNow.AddDays(1),
            ShiftStartTime = TimeSpan.FromHours(8),
            TotalDays = 3,
            DailySalary = 300,
            TotalAmount = 900,
            PenaltyAmount = 100,
            Governorate = "Cairo",
            City = "Cairo"
        };

        using (var db = _fixture.GetDbContext())
        {
            var svc = BuildService(db);
            var result = await svc.CreateContractAsync(contract);
            Assert.True(result.Id > 0);
        }

        // Verify balance and contract in separate clean context
        using (var verifyDb = _fixture.GetDbContext())
        {
            var wallet = await verifyDb.UserWallets.FirstAsync(w => w.UserId == "client-i1");
            Assert.Equal(200, wallet.FreeBalance);       // 1200 - 1000 (900+100)
            Assert.Equal(1000, wallet.FrozenBalance);    // 1000 frozen

            var inDb = await verifyDb.Contracts.Include(c => c.ContractDays).FirstAsync(c => c.ClientUserId == "client-i1");
            Assert.Equal("pending", inDb.Status);
            Assert.Equal(3, inDb.ContractDays.Count);
        }
    }

    // ── Provider Accept/Reject Integration ────────────────────────────────────

    [Fact]
    public async Task ProviderAccept_FreezesProviderPenalty_UpdatesInDb()
    {
        var contract = await SeedContract("client-i2", "+201800000001",
            status: "pending", penalty: 200, providerBalance: 500);

        using (var db = _fixture.GetDbContext())
        {
            var svc = BuildService(db);
            await svc.ProviderAcceptContractAsync(contract.Id, "provider-client-i2");
        }

        using (var verifyDb = _fixture.GetDbContext())
        {
            var providerWallet = await verifyDb.UserWallets.FirstAsync(w => w.UserId == "provider-client-i2");
            Assert.Equal(300, providerWallet.FreeBalance);    // 500 - 200
            Assert.Equal(200, providerWallet.FrozenBalance);  // 200 frozen

            var updatedContract = await verifyDb.Contracts.FindAsync(contract.Id);
            Assert.Equal("active", updatedContract!.Status);
        }
    }

    [Fact]
    public async Task ProviderReject_RefundsClient_UpdatesInDb()
    {
        var contract = await SeedContract("client-i3", "+201800000002",
            status: "pending", totalDays: 2, dailySalary: 250, penalty: 50);

        // Manually setup the frozen balance for client
        using (var db = _fixture.GetDbContext())
        {
            var wallet = await db.UserWallets.FirstAsync(w => w.UserId == "client-i3");
            wallet.FrozenBalance = 550; // 500 salary + 50 penalty
            await db.SaveChangesAsync();
        }

        using (var db = _fixture.GetDbContext())
        {
            var svc = BuildService(db);
            await svc.ProviderRejectContractAsync(contract.Id, "provider-client-i3");
        }

        using (var verifyDb = _fixture.GetDbContext())
        {
            var clientWallet = await verifyDb.UserWallets.FirstAsync(w => w.UserId == "client-i3");
            Assert.Equal(550, clientWallet.FreeBalance);
            Assert.Equal(0, clientWallet.FrozenBalance);

            var updatedContract = await verifyDb.Contracts.FindAsync(contract.Id);
            Assert.Equal("cancelled", updatedContract!.Status);
        }
    }

    // ── Dispute Reporting Integration ──────────────────────────────────────────

    [Fact]
    public async Task ReportDispute_SuspendsContract_CreatesDbComplaintRecord()
    {
        var contract = await SeedContract("client-i4", "+201900000001", status: "active");

        using (var db = _fixture.GetDbContext())
        {
            var svc = BuildService(db);
            await svc.ReportDisputeAsync(contract.Id, 1, "المزود لم يحضر", "client-i4");
        }

        using (var verifyDb = _fixture.GetDbContext())
        {
            var updatedContract = await verifyDb.Contracts.FindAsync(contract.Id);
            Assert.Equal("suspended", updatedContract!.Status);

            var complaint = await verifyDb.Complaints.FirstAsync(c => c.ContractId == contract.Id);
            Assert.Equal("open", complaint.Status);
            Assert.Equal("daily_dispute", complaint.ReportType);
            Assert.Contains("المزود لم يحضر", complaint.Description);
        }
    }

    // ── Admin Adjust & Resume Integration ──────────────────────────────────────

    [Fact]
    public async Task AdminAdjust_ClientToWorker_DistributesFundsAndResolvesComplaint()
    {
        var contract = await SeedContract("client-i5", "+202000000001",
            status: "suspended", totalDays: 3, dailySalary: 200, penalty: 100);

        // Pre-configure balance state
        using (var db = _fixture.GetDbContext())
        {
            var clientWallet = await db.UserWallets.FirstAsync(w => w.UserId == "client-i5");
            clientWallet.FrozenBalance = 700; // 600 salary + 100 penalty

            // Add an open complaint
            db.Complaints.Add(new Complaint
            {
                ContractId = contract.Id,
                ReporterUserId = "client-i5",
                Reason = "Dispute",
                Description = "disputed day 1",
                Status = "open",
                ReportType = "daily_dispute",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Admin rules: 2 days worked → pay provider 400
        using (var db = _fixture.GetDbContext())
        {
            var svc = BuildService(db);
            await svc.AdminAdjustAndResumeAsync(
                contract.Id,
                daysWorked: 2,
                direction: "client_to_worker",
                newStartDate: DateTime.UtcNow.AddDays(1),
                adminUserId: "admin-user-1",
                comment: "ruled in favor of worker for 2 days");
        }

        // Verify outputs in clean context
        using (var verifyDb = _fixture.GetDbContext())
        {
            var providerWallet = await verifyDb.UserWallets.FirstAsync(w => w.UserId == "provider-client-i5");
            Assert.Equal(400, providerWallet.FreeBalance); // 2 * 200

            var clientWallet = await verifyDb.UserWallets.FirstAsync(w => w.UserId == "client-i5");
            Assert.Equal(300, clientWallet.FrozenBalance); // 700 - 400 = 300 still frozen

            var complaint = await verifyDb.Complaints.FirstAsync(c => c.ContractId == contract.Id);
            Assert.Equal("resolved", complaint.Status);
            Assert.Equal("admin-user-1", complaint.ResolvedByAdminId);

            var updatedContract = await verifyDb.Contracts.FindAsync(contract.Id);
            Assert.Equal("active", updatedContract!.Status);
        }
    }
}
