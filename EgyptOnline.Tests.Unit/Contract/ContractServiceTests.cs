using EgyptOnline.Application.Services.Contract;
using EgyptOnline.Application.Services.Complaint;
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

namespace EgyptOnline.Tests.Unit.Contract;

/// <summary>
/// Unit tests for ContractService against an isolated in-memory database.
///
/// Scenarios covered:
/// - Contract creation: happy path, same-user rejection, insufficient balance
/// - Provider reject: refunds client, wrong provider rejected
/// - Provider accept: freezes provider penalty, insufficient penalty balance
/// - Arrival registration: before shift window, after midnight, double arrival
/// - Client confirm attendance: before arrival, double confirm, disputed day
/// - Dispute: suspends contract, can't dispute future days, non-party rejected
/// - Admin adjust & resume: client_to_free refunds client, client_to_worker pays provider
/// </summary>
[Trait("Category", "Unit")]
public class ContractServiceTests : UnitTestBase
{
    private readonly INotificationService _notif = A.Fake<INotificationService>();
    private readonly IEmailService _email = A.Fake<IEmailService>();
    private readonly ILogger<WalletService> _walletLogger = A.Fake<ILogger<WalletService>>();
    private readonly ILogger<ContractService> _contractLogger = A.Fake<ILogger<ContractService>>();
    private readonly ILogger<ComplaintService> _complaintLogger = A.Fake<ILogger<ComplaintService>>();

    private WalletService BuildWallet() =>
        new(Context, _notif, _walletLogger, _email, UserManagerFake);

    private ContractService BuildService()
    {
        var wallet = BuildWallet();
        var complaint = new ComplaintService(Context, _notif, _email, UserManagerFake, wallet);
        return new ContractService(Context, _notif, wallet, complaint, _contractLogger);
    }

    // ── Seed Helpers ──────────────────────────────────────────────────────────

    private async Task<User> SeedUser(string id, string phone, int walletBalance = 0)
    {
        var user = new User
        {
            Id = id,
            UserName = $"user_{id}",
            PhoneNumber = phone,
            Governorate = "Cairo",
            City = "Cairo",
            NormalizedUserName = $"USER_{id}".ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString()
        };
        Context.Users.Add(user);
        Context.UserWallets.Add(new UserWallet
        {
            UserId = id,
            FreeBalance = walletBalance,
            FrozenBalance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Build a contract that is already in the DB with given status and seeded users.
    /// Days are all set to yesterday so they're not in the future.
    /// </summary>
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
        Context.Contracts.Add(contract);
        await Context.SaveChangesAsync();

        // Create contract days (all in the past)
        var days = Enumerable.Range(1, totalDays).Select(d => new ContractDay
        {
            ContractId = contract.Id,
            DayNumber = d,
            Date = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-totalDays + d - 1).Date, DateTimeKind.Utc),
            ProviderArrived = false,
            Status = ContractDayStatus.Pending,
            IsProcessed = false
        }).ToList();

        Context.ContractDays.AddRange(days);
        await Context.SaveChangesAsync();
        return contract;
    }

    // ── Contract Creation ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateContract_HappyPath_ContractIsPendingAndFundsAreFrozen()
    {
        // Client has 1000 free. Contract costs 600 salary + 100 penalty = 700
        await SeedUser("client-1", "+201000000001", walletBalance: 1000);
        await SeedUser("provider-1", "+201000000002", walletBalance: 0);

        var contract = new Models.Contract
        {
            ClientUserId = "client-1",
            ServiceProviderPhoneNumber = "+201000000002",
            StartDate = DateTime.UtcNow.AddDays(1),
            ShiftStartTime = TimeSpan.FromHours(8),
            TotalDays = 3,
            DailySalary = 200,
            TotalAmount = 600,
            PenaltyAmount = 100,
            Governorate = "Cairo",
            City = "Cairo"
        };

        var svc = BuildService();
        var result = await svc.CreateContractAsync(contract);

        Assert.True(result.Id > 0);
        Assert.Equal("pending", result.Status);

        // Client's free balance should drop by 700 (600+100), frozen should be 700
        var wallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "client-1");
        Assert.Equal(300, wallet!.FreeBalance);
        Assert.Equal(700, wallet.FrozenBalance);
    }

    [Fact]
    public async Task CreateContract_WithSameUserAsProvider_ThrowsInvalidOperation()
    {
        await SeedUser("client-2", "+201000000003", walletBalance: 5000);

        var contract = new Models.Contract
        {
            ClientUserId = "client-2",
            ServiceProviderPhoneNumber = "+201000000003", // same phone as client
            StartDate = DateTime.UtcNow.AddDays(1),
            ShiftStartTime = TimeSpan.FromHours(8),
            TotalDays = 1,
            DailySalary = 100,
            TotalAmount = 100,
            PenaltyAmount = 0,
            Governorate = "Cairo",
            City = "Cairo"
        };

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateContractAsync(contract));
    }

    [Fact]
    public async Task CreateContract_WithInsufficientBalance_ThrowsInvalidOperation()
    {
        await SeedUser("client-3", "+201000000004", walletBalance: 100); // only 100
        await SeedUser("provider-3", "+201000000005", walletBalance: 0);

        var contract = new Models.Contract
        {
            ClientUserId = "client-3",
            ServiceProviderPhoneNumber = "+201000000005",
            StartDate = DateTime.UtcNow.AddDays(1),
            ShiftStartTime = TimeSpan.FromHours(8),
            TotalDays = 3,
            DailySalary = 200,
            TotalAmount = 600, // 600 + 100 penalty = 700, but only 100 in wallet
            PenaltyAmount = 100,
            Governorate = "Cairo",
            City = "Cairo"
        };

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateContractAsync(contract));
    }

    // ── Provider Reject ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProviderReject_OnPendingContract_RefundsClientAndCancels()
    {
        // Seed contract with money already frozen (simulate CreateContract)
        var contract = await SeedContract("client-r1", "+201100000001",
            status: "pending", totalDays: 2, dailySalary: 300, penalty: 50,
            clientBalance: 0);

        // Manually freeze client funds (as CreateContract would have done)
        var wallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "client-r1");
        wallet!.FrozenBalance = 650; // 600 salary + 50 penalty
        await Context.SaveChangesAsync();

        var svc = BuildService();
        var result = await svc.ProviderRejectContractAsync(contract.Id, "provider-client-r1");

        Assert.Equal("cancelled", result.Status);
        Assert.NotNull(result.CancelledAt);

        // Client's funds must be fully restored
        var updatedWallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "client-r1");
        Assert.Equal(650, updatedWallet!.FreeBalance);
        Assert.Equal(0, updatedWallet.FrozenBalance);
    }

    [Fact]
    public async Task ProviderReject_ByWrongUser_ThrowsUnauthorizedAccess()
    {
        var contract = await SeedContract("client-r2", "+201100000002", status: "pending");
        // SeedUser a 3rd party attacker
        await SeedUser("attacker-r2", "+201199999999", 0);

        var svc = BuildService();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ProviderRejectContractAsync(contract.Id, "attacker-r2"));
    }

    [Fact]
    public async Task ProviderReject_AlreadyActive_ThrowsInvalidOperation()
    {
        var contract = await SeedContract("client-r3", "+201100000003", status: "active");

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProviderRejectContractAsync(contract.Id, "provider-client-r3"));
    }

    // ── Provider Accept ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProviderAccept_WithSufficientBalance_FreezesPenaltyAndActivates()
    {
        var contract = await SeedContract("client-a1", "+201200000001",
            status: "pending", penalty: 150, providerBalance: 500);

        var svc = BuildService();
        var result = await svc.ProviderAcceptContractAsync(contract.Id, "provider-client-a1");

        Assert.Equal("active", result.Status);

        // Provider's penalty (150) should now be frozen
        var providerWallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "provider-client-a1");
        Assert.Equal(350, providerWallet!.FreeBalance);   // 500 - 150
        Assert.Equal(150, providerWallet.FrozenBalance);
    }

    [Fact]
    public async Task ProviderAccept_WithInsufficientPenaltyBalance_ThrowsInvalidOperation()
    {
        var contract = await SeedContract("client-a2", "+201200000002",
            status: "pending", penalty: 500, providerBalance: 100); // only 100, needs 500

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProviderAcceptContractAsync(contract.Id, "provider-client-a2"));
    }

    // ── Arrival Registration ──────────────────────────────────────────────────

    [Fact]
    public async Task RegisterArrival_BeforeShiftWindow_ThrowsInvalidOperation()
    {
        // Contract day is tomorrow so arrival registration before shift is invalid
        var contract = await SeedContract("client-arr1", "+201300000001", status: "active", totalDays: 1);
        // Move day 1 to tomorrow to make it a future day
        var day = Context.ContractDays.First(d => d.ContractId == contract.Id && d.DayNumber == 1);
        day.Date = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1).Date, DateTimeKind.Utc);
        // Set shift to start well in the future (end of day + 1)
        contract.ShiftStartTime = TimeSpan.FromHours(23); // 11pm shift — we are way before the 30-min grace
        await Context.SaveChangesAsync();

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegisterArrivalAsync(contract.Id, 1, "provider-client-arr1"));
    }

    [Fact]
    public async Task RegisterArrival_AlreadyArrived_ThrowsInvalidOperation()
    {
        var contract = await SeedContract("client-arr2", "+201300000002", status: "active", totalDays: 1);
        // Mark day 1 as already arrived
        var day = Context.ContractDays.First(d => d.ContractId == contract.Id && d.DayNumber == 1);
        day.ProviderArrived = true;
        await Context.SaveChangesAsync();

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegisterArrivalAsync(contract.Id, 1, "provider-client-arr2"));
    }

    [Fact]
    public async Task RegisterArrival_OnInactiveContract_ThrowsInvalidOperation()
    {
        var contract = await SeedContract("client-arr3", "+201300000003", status: "pending", totalDays: 1);

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegisterArrivalAsync(contract.Id, 1, "provider-client-arr3"));
    }

    // ── Client Confirm Attendance ─────────────────────────────────────────────

    [Fact]
    public async Task ClientConfirm_BeforeProviderArrival_ThrowsInvalidOperation()
    {
        var contract = await SeedContract("client-cc1", "+201400000001", status: "active");

        var svc = BuildService();
        // Provider has NOT arrived for day 1 yet
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ClientConfirmAttendanceAsync(contract.Id, 1, "client-cc1"));
    }

    [Fact]
    public async Task ClientConfirm_HappyPath_SetsConfirmedFlag()
    {
        var contract = await SeedContract("client-cc2", "+201400000002", status: "active");

        // Mark provider as arrived
        var day = Context.ContractDays.First(d => d.ContractId == contract.Id && d.DayNumber == 1);
        day.ProviderArrived = true;
        day.ArrivalTime = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        var svc = BuildService();
        var result = await svc.ClientConfirmAttendanceAsync(contract.Id, 1, "client-cc2");

        Assert.True(result.ClientConfirmed);
        Assert.NotNull(result.ClientConfirmedAt);
    }

    [Fact]
    public async Task ClientConfirm_AlreadyConfirmed_ThrowsInvalidOperation()
    {
        var contract = await SeedContract("client-cc3", "+201400000003", status: "active");

        var day = Context.ContractDays.First(d => d.ContractId == contract.Id && d.DayNumber == 1);
        day.ProviderArrived = true;
        day.ArrivalTime = DateTime.UtcNow;
        day.ClientConfirmed = true;
        day.ClientConfirmedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ClientConfirmAttendanceAsync(contract.Id, 1, "client-cc3"));
    }

    [Fact]
    public async Task ClientConfirm_ByWrongUser_ThrowsUnauthorizedAccess()
    {
        var contract = await SeedContract("client-cc4", "+201400000004", status: "active");
        await SeedUser("attacker-cc", "+201499999999", 0);

        var day = Context.ContractDays.First(d => d.ContractId == contract.Id && d.DayNumber == 1);
        day.ProviderArrived = true;
        await Context.SaveChangesAsync();

        var svc = BuildService();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ClientConfirmAttendanceAsync(contract.Id, 1, "attacker-cc"));
    }

    // ── Dispute ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReportDispute_ByClient_SuspendsContractAndSetsAbsentDisputed()
    {
        var contract = await SeedContract("client-d1", "+201500000001", status: "active");

        var svc = BuildService();
        var result = await svc.ReportDisputeAsync(contract.Id, 1, "غياب مقدم الخدمة", "client-d1");

        Assert.Equal("suspended", result.Status);

        var day = await Context.ContractDays.FindAsync(
            Context.ContractDays.First(d => d.ContractId == contract.Id && d.DayNumber == 1).Id);
        Assert.Equal(ContractDayStatus.AbsentDisputed, day!.Status);
        Assert.NotNull(day.DisputeReportedAt);
    }

    [Fact]
    public async Task ReportDispute_ByProvider_SuspendsContract()
    {
        var contract = await SeedContract("client-d2", "+201500000002", status: "active");

        var svc = BuildService();
        var result = await svc.ReportDisputeAsync(contract.Id, 1, "مشكلة في الموقع", "provider-client-d2");

        Assert.Equal("suspended", result.Status);
    }

    [Fact]
    public async Task ReportDispute_ByNonParty_ThrowsUnauthorizedAccess()
    {
        var contract = await SeedContract("client-d3", "+201500000003", status: "active");
        await SeedUser("non-party-d3", "+201599999999", 0);

        var svc = BuildService();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ReportDisputeAsync(contract.Id, 1, "some reason", "non-party-d3"));
    }

    [Fact]
    public async Task ReportDispute_OnFutureDay_ThrowsInvalidOperation()
    {
        var contract = await SeedContract("client-d4", "+201500000004", status: "active", totalDays: 3);

        // Push day 3 to the future
        var futureDay = Context.ContractDays.First(d => d.ContractId == contract.Id && d.DayNumber == 3);
        futureDay.Date = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(5).Date, DateTimeKind.Utc);
        await Context.SaveChangesAsync();

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ReportDisputeAsync(contract.Id, 3, "future day", "client-d4"));
    }

    [Fact]
    public async Task ReportDispute_OnAlreadySuspendedContract_ThrowsInvalidOperation()
    {
        var contract = await SeedContract("client-d5", "+201500000005", status: "suspended");

        var svc = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ReportDisputeAsync(contract.Id, 1, "double dispute", "client-d5"));
    }

    // ── Admin Adjust & Resume (Refund / Pay) ──────────────────────────────────

    [Fact]
    public async Task AdminAdjust_ClientToFree_RefundsClientForDisputedDays()
    {
        // Contract: 3 days @ 200 = 600. Frozen 700 (600+100 penalty). Dispute ruled for client.
        var contract = await SeedContract("client-adj1", "+201600000001",
            status: "suspended", totalDays: 3, dailySalary: 200, penalty: 100);

        // Seed wallet with correct frozen amounts
        var wallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "client-adj1");
        wallet!.FrozenBalance = 700;
        await Context.SaveChangesAsync();

        var svc = BuildService();
        // Admin rules: refund all 3 days to client
        var result = await svc.AdminAdjustAndResumeAsync(
            contract.Id,
            daysWorked: 3,
            direction: "client_to_free",
            newStartDate: null,
            adminUserId: "admin-1",
            comment: "provider was absent");

        Assert.Equal("active", result.Status);

        // Client's frozen balance reduced by salary for 3 days (600), moved to free
        var updatedWallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "client-adj1");
        Assert.Equal(600, updatedWallet!.FreeBalance);   // 600 refunded
        Assert.Equal(100, updatedWallet.FrozenBalance);  // penalty still frozen
    }

    [Fact]
    public async Task AdminAdjust_ClientToWorker_PaysProviderForWorkedDays()
    {
        // Contract: 2 days worked out of 3. Admin rules days were worked, pay provider.
        var contract = await SeedContract("client-adj2", "+201600000002",
            status: "suspended", totalDays: 3, dailySalary: 300, penalty: 50);

        // Seed wallets
        var clientWallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "client-adj2");
        clientWallet!.FrozenBalance = 950; // 900 salary + 50 penalty
        var providerWallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "provider-client-adj2");
        providerWallet!.FreeBalance = 0;
        await Context.SaveChangesAsync();

        var svc = BuildService();
        var result = await svc.AdminAdjustAndResumeAsync(
            contract.Id,
            daysWorked: 2,
            direction: "client_to_worker",
            newStartDate: DateTime.UtcNow.AddDays(1),
            adminUserId: "admin-1",
            comment: "provider worked 2 days");

        Assert.Equal("active", result.Status);

        // Provider receives 2 * 300 = 600 in free balance
        var updatedProviderWallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "provider-client-adj2");
        Assert.Equal(600, updatedProviderWallet!.FreeBalance);

        // Client's frozen balance reduced by 600
        var updatedClientWallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "client-adj2");
        Assert.Equal(350, updatedClientWallet!.FrozenBalance); // 950 - 600 = 350
    }

    [Fact]
    public async Task AdminAdjust_AlreadyProcessedDays_AreNotPaidTwice()
    {
        var contract = await SeedContract("client-adj3", "+201600000003",
            status: "suspended", totalDays: 3, dailySalary: 200, penalty: 100);

        // Mark day 1 as already processed (already paid)
        var day1 = Context.ContractDays.First(d => d.ContractId == contract.Id && d.DayNumber == 1);
        day1.IsProcessed = true;
        day1.ProcessedAt = DateTime.UtcNow;
        day1.Status = ContractDayStatus.Completed;

        var wallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "client-adj3");
        wallet!.FrozenBalance = 700;
        await Context.SaveChangesAsync();

        var svc = BuildService();
        // daysWorked = 3 but day 1 is already processed
        var result = await svc.AdminAdjustAndResumeAsync(
            contract.Id,
            daysWorked: 3,
            direction: "client_to_worker",
            newStartDate: null,
            adminUserId: "admin-1",
            comment: "resolve double-pay check");

        // Only 2 unprocessed days (days 2 and 3) should be paid: 2 * 200 = 400
        var providerWallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == "provider-client-adj3");
        Assert.Equal(400, providerWallet!.FreeBalance);
    }
}
