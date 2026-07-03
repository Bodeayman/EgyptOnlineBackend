using EgyptOnline.Data;
using EgyptOnline.Domain.Models;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class AutoPayoutBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _gracePeriod = TimeSpan.FromHours(3);

    public AutoPayoutBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("AutoPayoutBackgroundService started at: {Time}", DateTimeOffset.UtcNow);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAutoPayoutsAsync(stoppingToken);
                await ExpireStaleContractsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred during AutoPayoutBackgroundService cycle");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        Log.Information("AutoPayoutBackgroundService stopped at: {Time}", DateTimeOffset.UtcNow);
    }

    // ── SCENARIO: Daily payout ────────────────────────────────────────────────

    private async Task ProcessAutoPayoutsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var walletService = scope.ServiceProvider.GetRequiredService<WalletService>();

        // Current Egypt local time (UTC+3)
        var currentEgyptTime = DateTime.UtcNow.AddHours(3);

        var activeContracts = await context.Contracts
            .Include(c => c.ContractDays)
            .Where(c => c.Status == "active")
            .ToListAsync(stoppingToken);

        foreach (var contract in activeContracts)
        {
            if (stoppingToken.IsCancellationRequested) break;
            await ProcessContractDaysAsync(context, walletService, contract, currentEgyptTime, stoppingToken);
        }
    }

    private async Task ProcessContractDaysAsync(
        ApplicationDbContext context,
        WalletService walletService,
        Contract contract,
        DateTime currentEgyptTime,
        CancellationToken stoppingToken)
    {
        foreach (var contractDay in contract.ContractDays)
        {
            if (stoppingToken.IsCancellationRequested) break;

            if (contractDay.IsProcessed) continue;
            if (!contractDay.ProviderArrived) continue;
            if (contractDay.Status == ContractDayStatus.AbsentDisputed) continue;

            // ShiftEndTime stored in Egypt-local terms. Date is also Egypt-local.
            var shiftEnd = contractDay.Date.Date.Add(contract.ShiftEndTime);

            bool shouldPayout;

            if (contractDay.ClientConfirmed)
            {
                // ── Scenario 1: Client confirmed → release exactly at shift end ──
                shouldPayout = currentEgyptTime >= shiftEnd;
            }
            else
            {
                // ── Scenario 2: No confirmation → 3-hour safety grace period ──
                shouldPayout = currentEgyptTime >= shiftEnd.Add(_gracePeriod);
            }

            if (!shouldPayout) continue;

            await ProcessDayPayoutAsync(context, walletService, contract, contractDay, currentEgyptTime);
        }
    }

    private async Task ProcessDayPayoutAsync(
        ApplicationDbContext context,
        WalletService walletService,
        Contract contract,
        ContractDay contractDay,
        DateTime currentEgyptTime)
    {
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            contractDay.Status = ContractDayStatus.Completed;
            contractDay.IsProcessed = true;
            contractDay.ProcessedAt = DateTime.UtcNow;

            // Transfer daily salary: client frozen → worker free
            await walletService.SubtractFromFrozenBalanceAsync(contract.ClientUserId, contract.DailySalary);

            var providerUser = await context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);
            if (providerUser != null)
                await walletService.AddToFreeBalanceAsync(providerUser.Id, contract.DailySalary);

            // Check if all days are now processed → complete the contract
            var allDaysProcessed = contract.ContractDays.All(cd => cd.IsProcessed);
            if (allDaysProcessed)
            {
                contract.Status = "completed";
                contract.CompletedAt = DateTime.UtcNow;

                // Release both penalty deposits back to free balance
                if (contract.PenaltyAmount > 0)
                {
                    await walletService.TransferFrozenToFreeAsync(contract.ClientUserId, contract.PenaltyAmount);
                    if (providerUser != null)
                        await walletService.TransferFrozenToFreeAsync(providerUser.Id, contract.PenaltyAmount);
                }

                Log.Information("Contract {ContractId} completed. All day payouts processed, penalties released", contract.Id);
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            var mode = contractDay.ClientConfirmed ? "immediate (client confirmed)" : "grace period (3h)";
            Log.Information(
                "Auto-payout [{Mode}] for Contract {ContractId}, Day {DayNumber}. Amount: {Amount}",
                mode, contract.Id, contractDay.DayNumber, contract.DailySalary);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to process auto-payout for Contract {ContractId}, Day {DayNumber}", contract.Id, contractDay.DayNumber);
        }
    }

    // ── SCENARIO: Auto-expire stale pending contracts ─────────────────────────

    private async Task ExpireStaleContractsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var walletService = scope.ServiceProvider.GetRequiredService<WalletService>();

        // Egypt local date today
        var egyptDate = DateTime.UtcNow.AddHours(3).Date;

        // Any pending contract whose start date is now in the past
        var staleContracts = await context.Contracts
            .Where(c => c.Status == "pending" && c.StartDate.Date < egyptDate)
            .ToListAsync(stoppingToken);

        foreach (var contract in staleContracts)
        {
            if (stoppingToken.IsCancellationRequested) break;
            await ExpireContractAsync(context, walletService, contract);
        }
    }

    private async Task ExpireContractAsync(
        ApplicationDbContext context,
        WalletService walletService,
        Contract contract)
    {
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // Unfreeze total + penalty back to client's free balance
            var totalFrozen = contract.TotalAmount + contract.PenaltyAmount;
            await walletService.TransferFrozenToFreeAsync(contract.ClientUserId, totalFrozen);

            contract.Status = "cancelled";
            contract.CancelledAt = DateTime.UtcNow;
            contract.CancelledBy = "System (auto-expired)";
            contract.TerminationReason = "انتهت صلاحية العقد - لم يتم قبوله قبل تاريخ البداية";

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information(
                "Contract {ContractId} auto-expired (start date {StartDate} passed without provider acceptance). Funds unfrozen.",
                contract.Id, contract.StartDate.Date);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to auto-expire Contract {ContractId}", contract.Id);
        }
    }
}
