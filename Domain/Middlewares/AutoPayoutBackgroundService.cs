using EgyptOnline.Data;
using EgyptOnline.Domain.Models;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Models;
using EgyptOnline.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class AutoPayoutBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public AutoPayoutBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("AutoPayoutBackgroundService started at: {Time}", EgyptTimeHelper.NowInEgypt());

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAutoPayoutsAsync(stoppingToken);
                await ExpireStaleContractsAsync(stoppingToken);
                await CompleteIncompleteContractsAsync(stoppingToken);
                await ProcessContractNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred during AutoPayoutBackgroundService cycle");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        Log.Information("AutoPayoutBackgroundService stopped at: {Time}", EgyptTimeHelper.NowInEgypt());
    }

    // ── SCENARIO: Daily payout ────────────────────────────────────────────────

    private async Task ProcessAutoPayoutsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var walletService = scope.ServiceProvider.GetRequiredService<WalletService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        // Current Egypt local time
        var currentEgyptTime = EgyptTimeHelper.NowInEgypt();

        var activeContracts = await context.Contracts
            .Include(c => c.ContractDays)
            .Where(c => c.Status == "active")
            .ToListAsync(stoppingToken);

        foreach (var contract in activeContracts)
        {
            if (stoppingToken.IsCancellationRequested) break;
            await ProcessContractDaysAsync(context, walletService, notificationService, contract, currentEgyptTime, stoppingToken);
        }
    }

    private async Task ProcessContractDaysAsync(
        ApplicationDbContext context,
        WalletService walletService,
        INotificationService notificationService,
        Contract contract,
        DateTime currentEgyptTime,
        CancellationToken stoppingToken)
    {
        foreach (var contractDay in contract.ContractDays)
        {
            if (stoppingToken.IsCancellationRequested) break;

            if (contractDay.IsProcessed) continue;

            // Batch contracts: process payout based on batch date, not arrival/confirmation
            if (contract.ContractType == ContractType.Batch)
            {
                var batchTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
                var batchDayLocal = TimeZoneInfo.ConvertTimeFromUtc(contractDay.Date, batchTimeZone);

                // Batch payout occurs at the scheduled batch date
                if (currentEgyptTime >= batchDayLocal)
                {
                    await ProcessDayPayoutAsync(context, walletService, notificationService, contract, contractDay, currentEgyptTime);
                }
                continue;
            }

            // PerDay contracts: process based on arrival/confirmation
            if (!contractDay.ProviderArrived) continue;
            if (contractDay.Status == ContractDayStatus.AbsentDisputed) continue;

            // Convert contract day date (UTC) to Egypt local time for shift calculations
            var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            var contractDayEgyptLocal = TimeZoneInfo.ConvertTimeFromUtc(contractDay.Date, egyptTimeZone);

            // Shift times are stored as TimeSpan representing Egypt local time
            var shiftEndTime = contract.ShiftEndTime ?? contract.ShiftStartTime;
            var shiftEnd = contractDayEgyptLocal.Date.Add(shiftEndTime);

            bool shouldPayout;

            if (contractDay.ClientConfirmed)
            {
                // ── Scenario 1: Client confirmed → release exactly at shift end ──
                shouldPayout = currentEgyptTime >= shiftEnd;
            }
            else if (contractDay.ArrivalTime.HasValue)
            {
                // ── Scenario 2: Provider arrived but client didn't confirm → auto-pay after 3 hours ──
                var arrivalTimeEgypt = TimeZoneInfo.ConvertTimeFromUtc(contractDay.ArrivalTime.Value, egyptTimeZone);
                var threeHoursAfterArrival = arrivalTimeEgypt.AddHours(3);
                shouldPayout = currentEgyptTime >= threeHoursAfterArrival;
            }
            else
            {
                // ── Scenario 3: No arrival yet → don't payout ──
                shouldPayout = false;
            }

            if (!shouldPayout) continue;

            await ProcessDayPayoutAsync(context, walletService, notificationService, contract, contractDay, currentEgyptTime);
        }
    }

    private async Task ProcessDayPayoutAsync(
        ApplicationDbContext context,
        WalletService walletService,
        INotificationService notificationService,
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

            var providerUser = await context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

            // Execute daily money transfer for PerDay contracts
            if (contract.ContractType == ContractType.PerDay)
            {
                // Transfer daily salary: client frozen → worker free
                await walletService.SubtractFromFrozenBalanceAsync(contract.ClientUserId, contract.DailySalary);

                if (providerUser != null)
                    await walletService.AddToFreeBalanceAsync(providerUser.Id, contract.DailySalary);

                // Notify provider about the daily payout
                try
                {
                    var clientUser = await context.Users.FindAsync(contract.ClientUserId);
                    var clientName = clientUser != null ? $"{clientUser.FirstName} {clientUser.LastName}" : "العميل";

                    await notificationService.SendNotificationToUser(
                        providerUser.Id,
                        "دفع يومي مستلم",
                        $"تم استلام {contract.DailySalary} جنيه من {clientName} عن يوم {contractDay.DayNumber} من العقد #{contract.Id}",
                        "wallet"
                    );
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send payout notification to provider {ProviderId}", providerUser?.Id);
                }
            }

            // Execute batch payment for Batch contracts
            if (contract.ContractType == ContractType.Batch)
            {
                if (!contractDay.BatchAmount.HasValue)
                {
                    Log.Error("BatchAmount is null for ContractDay {ContractDayId} in Contract {ContractId}. Skipping payout.", contractDay.Id, contract.Id);
                    throw new InvalidOperationException($"مبلغ الدفعة غير محدد للدفعة رقم {contractDay.DayNumber} في العقد #{contract.Id}");
                }
                var batchAmount = contractDay.BatchAmount.Value;
                await walletService.SubtractFromFrozenBalanceAsync(contract.ClientUserId, batchAmount);

                if (providerUser != null)
                    await walletService.AddToFreeBalanceAsync(providerUser.Id, batchAmount);

                // Notify provider about the batch payout
                try
                {
                    var clientUser = await context.Users.FindAsync(contract.ClientUserId);
                    var clientName = clientUser != null ? $"{clientUser.FirstName} {clientUser.LastName}" : "العميل";

                    await notificationService.SendNotificationToUser(
                        providerUser.Id,
                        "دفعة مستلمة",
                        $"تم استلام {batchAmount} جنيه من {clientName} عن الدفعة رقم {contractDay.DayNumber} من العقد #{contract.Id}",
                        "wallet"
                    );
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send batch payout notification to provider {ProviderId}", providerUser?.Id);
                }
            }

            // Check if all days are now processed → complete the contract
            var allDaysProcessed = contract.ContractDays.All(cd => cd.IsProcessed);
            if (allDaysProcessed)
            {
                contract.Status = "completed";
                contract.CompletedAt = DateTime.UtcNow;

                // Batch contracts: No additional payout at completion since each batch is paid individually
                // EndOfDays contracts: Single lump payout of full TotalAmount at contract end
                if (contract.ContractType == ContractType.EndOfDays && providerUser != null)
                {
                    // EndOfDays contract: Pay the full TotalAmount (not derived from DailySalary)
                    await walletService.SubtractFromFrozenBalanceAsync(contract.ClientUserId, contract.TotalAmount);
                    await walletService.AddToFreeBalanceAsync(providerUser.Id, contract.TotalAmount);
                    Log.Information("Contract {ContractId} completed (EndOfDays). Paid full TotalAmount {Amount} to provider", contract.Id, contract.TotalAmount);
                }

                // Calculate remaining frozen balance for client (includes unused daily salary + client's penalty)
                var clientWallet = await context.UserWallets.FirstOrDefaultAsync(w => w.UserId == contract.ClientUserId);
                var remainingFrozenBalance = clientWallet?.FrozenBalance ?? 0;

                // Return all remaining frozen money to client
                if (remainingFrozenBalance > 0)
                {
                    await walletService.TransferFrozenToFreeAsync(contract.ClientUserId, remainingFrozenBalance);
                    Log.Information("Contract {ContractId} completed ({ContractType}). Returned {Amount} remaining frozen balance to client", contract.Id, contract.ContractType, remainingFrozenBalance);
                }

                // Release provider's penalty deposit back to free balance
                if (contract.PenaltyAmount > 0 && providerUser != null)
                {
                    await walletService.TransferFrozenToFreeAsync(providerUser.Id, contract.PenaltyAmount);
                    Log.Information("Contract {ContractId} completed. Provider's penalty of {Amount} released", contract.Id, contract.PenaltyAmount);
                }

                Log.Information("Contract {ContractId} completed. Payouts processed for type {ContractType}, remaining funds and penalties released", contract.Id, contract.ContractType);

                // Send rating notification to client exactly once per contract
                if (!contract.RatingNotificationSent)
                {
                    contract.RatingNotificationSent = true;
                    try
                    {
                        if (providerUser != null)
                        {
                            await notificationService.SendNotificationToUser(
                                contract.ClientUserId,
                                "انتهى العقد. يمكنك الآن تقييم مقدم الخدمة",
                                $"انتهى العقد #{contract.Id}. يمكنك الآن تقييم مقدم الخدمة {providerUser.FirstName} {providerUser.LastName}",
                                "rating",
                                providerUser.Id,
                                $"{providerUser.FirstName} {providerUser.LastName}",
                                contract.Id
                            );
                            Log.Information("Sent rating notification for completed Contract {ContractId}", contract.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to send rating notification for completed Contract {ContractId}", contract.Id);
                    }
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            var mode = contractDay.ClientConfirmed ? "immediate (client confirmed)" : "grace period (23:59:59 Egypt time)";
            Log.Information(
                "Auto-payout [{Mode}] for Contract {ContractId} ({ContractType}), Day {DayNumber}.",
                mode, contract.Id, contract.ContractType, contractDay.DayNumber);
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
        var egyptToday = EgyptTimeHelper.TodayInEgypt();

        // Any pending contract whose start date is now in the past (compare Egypt dates)
        var staleContracts = await context.Contracts
            .Where(c => c.Status == "pending" && EgyptTimeHelper.ToEgyptDate(c.StartDate) < egyptToday)
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
            // Return client's frozen funds (daily salary + client's penalty)
            await walletService.TransferFrozenToFreeAsync(contract.ClientUserId, contract.TotalAmount + contract.PenaltyAmount);

            // Return provider's frozen penalty deposit
            var providerUser = await context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);
            if (providerUser != null && contract.PenaltyAmount > 0)
            {
                await walletService.TransferFrozenToFreeAsync(providerUser.Id, contract.PenaltyAmount);
            }

            contract.Status = "cancelled";
            contract.CancelledAt = DateTime.UtcNow;
            contract.CancelledBy = "System (auto-expired)";
            contract.TerminationReason = "انتهت صلاحية العقد - لم يتم قبوله قبل تاريخ البداية";

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information(
                "Contract {ContractId} auto-expired (start date {StartDate} passed without provider acceptance). Client funds unfrozen.",
                contract.Id, contract.StartDate.Date);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to auto-expire Contract {ContractId}", contract.Id);
        }
    }

    // ── SCENARIO: Complete incomplete contracts ─────────────────────────

    private async Task CompleteIncompleteContractsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var walletService = scope.ServiceProvider.GetRequiredService<WalletService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        // Egypt local date today
        var egyptToday = EgyptTimeHelper.TodayInEgypt();

        // Find active contracts where end date has passed (compare Egypt dates)
        var incompleteContracts = await context.Contracts
            .Include(c => c.ContractDays)
            .Where(c => c.Status == "active" &&
                        (c.ContractType == ContractType.EndOfDays
                            ? (c.EndDate.HasValue && EgyptTimeHelper.ToEgyptDate(c.EndDate.Value) < egyptToday)
                            : EgyptTimeHelper.ToEgyptDate(c.StartDate.AddDays(c.TotalDays - 1)) < egyptToday))
            .ToListAsync(stoppingToken);

        foreach (var contract in incompleteContracts)
        {
            if (stoppingToken.IsCancellationRequested) break;
            await CompleteContractAsync(context, walletService, notificationService, contract);
        }
    }

    private async Task CompleteContractAsync(
        ApplicationDbContext context,
        WalletService walletService,
        INotificationService notificationService,
        Contract contract)
    {
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // Mark all unprocessed days as completed without payout
            var unprocessedDays = contract.ContractDays.Where(cd => !cd.IsProcessed).ToList();
            foreach (var day in unprocessedDays)
            {
                day.Status = ContractDayStatus.Completed;
                day.IsProcessed = true;
                day.ProcessedAt = DateTime.UtcNow;
            }

            contract.Status = "completed";
            contract.CompletedAt = DateTime.UtcNow;
            contract.UpdatedAt = DateTime.UtcNow;

            var providerUser = await context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

            if (contract.ContractType == ContractType.EndOfDays && providerUser != null)
            {
                // EndOfDays contract: Pay the full TotalAmount (not derived from DailySalary)
                await walletService.SubtractFromFrozenBalanceAsync(contract.ClientUserId, contract.TotalAmount);
                await walletService.AddToFreeBalanceAsync(providerUser.Id, contract.TotalAmount);
                Log.Information("Contract {ContractId} auto-completed (EndOfDays). Paid full TotalAmount {Amount} to provider", contract.Id, contract.TotalAmount);
            }

            // Batch contracts: No additional payout at completion since each batch is paid individually
            // Contract simply completes when all batches are processed

            // Calculate remaining frozen balance for client (includes unused daily salary + client's penalty)
            var clientWallet = await context.UserWallets.FirstOrDefaultAsync(w => w.UserId == contract.ClientUserId);
            var remainingFrozenBalance = clientWallet?.FrozenBalance ?? 0;

            // Return all remaining frozen money to client (only if wallet exists)
            if (remainingFrozenBalance > 0 && clientWallet != null)
            {
                await walletService.TransferFrozenToFreeAsync(contract.ClientUserId, remainingFrozenBalance);
                Log.Information("Contract {ContractId} completed (incomplete). Returned {Amount} remaining frozen balance to client", contract.Id, remainingFrozenBalance);
            }
            else if (remainingFrozenBalance > 0 && clientWallet == null)
            {
                Log.Warning("Contract {ContractId} completed (incomplete). Client has {Amount} frozen balance but no wallet exists. Skipping fund return.", contract.Id, remainingFrozenBalance);
            }

            // Release provider's penalty deposit back to free balance (only if wallet exists)
            if (contract.PenaltyAmount > 0 && providerUser != null)
            {
                var providerWallet = await context.UserWallets.FirstOrDefaultAsync(w => w.UserId == providerUser.Id);
                if (providerWallet != null)
                {
                    await walletService.TransferFrozenToFreeAsync(providerUser.Id, contract.PenaltyAmount);
                    Log.Information("Contract {ContractId} completed (incomplete). Provider's penalty of {Amount} released", contract.Id, contract.PenaltyAmount);
                }
                else
                {
                    Log.Warning("Contract {ContractId} completed (incomplete). Provider has penalty of {Amount} but no wallet exists. Skipping fund return.", contract.Id, contract.PenaltyAmount);
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Send rating notification to client exactly once per contract
            if (!contract.RatingNotificationSent)
            {
                contract.RatingNotificationSent = true;
                try
                {
                    if (providerUser != null)
                    {
                        await notificationService.SendNotificationToUser(
                            contract.ClientUserId,
                            "انتهى العقد. يمكنك الآن تقييم مقدم الخدمة",
                            $"انتهى العقد #{contract.Id}. يمكنك الآن تقييم مقدم الخدمة {providerUser.FirstName} {providerUser.LastName}",
                            "rating",
                            providerUser.Id,
                            $"{providerUser.FirstName} {providerUser.LastName}",
                            contract.Id
                        );
                        Log.Information("Sent rating notification for auto-completed Contract {ContractId}", contract.Id);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send rating notification for auto-completed Contract {ContractId}", contract.Id);
                }
            }

            Log.Information(
                "Contract {ContractId} auto-completed (end date passed). {UnprocessedDays} days marked as completed without payout.",
                contract.Id, unprocessedDays.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to auto-complete Contract {ContractId}", contract.Id);
        }
    }

    // ── SCENARIO: Contract notifications ─────────────────────────────────────

    private async Task ProcessContractNotificationsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var currentEgyptTime = EgyptTimeHelper.NowInEgypt();
        var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

        // Get active contracts that may need notifications
        var activeContracts = await context.Contracts
            .Include(c => c.ContractDays)
            .Where(c => c.Status == "active")
            .ToListAsync(stoppingToken);

        foreach (var contract in activeContracts)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var providerUser = await context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);
            if (providerUser == null) continue;

            // Batch contracts: 24h notification before each batch payment
            if (contract.ContractType == ContractType.Batch)
            {
                foreach (var contractDay in contract.ContractDays)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    if (contractDay.IsProcessed) continue;

                    var contractDayEgyptLocal = TimeZoneInfo.ConvertTimeFromUtc(contractDay.Date, egyptTimeZone);
                    var notificationTime = contractDayEgyptLocal.AddHours(-24);

                    // Send 24h notification if we're within the notification window
                    if (currentEgyptTime >= notificationTime && currentEgyptTime < contractDayEgyptLocal)
                    {
                        if (!contractDay.BatchAmount.HasValue)
                        {
                            Log.Warning("BatchAmount is null for ContractDay {ContractDayId} in Contract {ContractId}. Skipping notification.", contractDay.Id, contract.Id);
                            continue;
                        }
                        try
                        {
                            await notificationService.SendNotificationToUser(
                                contract.ClientUserId,
                                "سيتم تسليم الدفعة خلال 24 ساعة",
                                $"سيتم تسليم دفعة {contractDay.BatchAmount.Value} جنيه عن الدفعة رقم {contractDay.DayNumber} من العقد #{contract.Id} خلال 24 ساعة",
                                "contract",
                                providerUser.Id,
                                $"{providerUser.FirstName} {providerUser.LastName}"
                            );
                            Log.Information("Sent 24h Batch notification for Contract {ContractId}, Batch {BatchNumber}", contract.Id, contractDay.DayNumber);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to send 24h Batch notification for Contract {ContractId}, Batch {BatchNumber}", contract.Id, contractDay.DayNumber);
                        }
                    }
                }
            }

            // EndOfDays contracts: 48h and 24h notifications before contract end
            if (contract.ContractType == ContractType.EndOfDays)
            {
                if (!contract.EndDate.HasValue)
                {
                    Log.Warning("Contract {ContractId} is EndOfDays type but has no EndDate. Skipping notifications.", contract.Id);
                    continue;
                }

                var contractEndEgyptLocal = TimeZoneInfo.ConvertTimeFromUtc(contract.EndDate.Value, egyptTimeZone).Date;

                var notification48hTime = contractEndEgyptLocal.AddHours(-48);
                var notification24hTime = contractEndEgyptLocal.AddHours(-24);

                // Send 48h notification
                if (currentEgyptTime >= notification48hTime && currentEgyptTime < notification24hTime)
                {
                    try
                    {
                        await notificationService.SendNotificationToUser(
                            contract.ClientUserId,
                            "سينتهي العقد وتسليم المستحقات خلال 48 ساعة",
                            $"سينتهي العقد #{contract.Id} وتسليم المستحقات خلال 48 ساعة",
                            "contract",
                            providerUser.Id,
                            $"{providerUser.FirstName} {providerUser.LastName}"
                        );
                        Log.Information("Sent 48h EndOfDays notification for Contract {ContractId}", contract.Id);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to send 48h EndOfDays notification for Contract {ContractId}", contract.Id);
                    }
                }

                // Send 24h notification
                if (currentEgyptTime >= notification24hTime && currentEgyptTime < contractEndEgyptLocal)
                {
                    try
                    {
                        await notificationService.SendNotificationToUser(
                            contract.ClientUserId,
                            "سينتهي العقد وتسليم المستحقات خلال 24 ساعة",
                            $"سينتهي العقد #{contract.Id} وتسليم المستحقات خلال 24 ساعة",
                            "contract",
                            providerUser.Id,
                            $"{providerUser.FirstName} {providerUser.LastName}"
                        );
                        Log.Information("Sent 24h EndOfDays notification for Contract {ContractId}", contract.Id);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to send 24h EndOfDays notification for Contract {ContractId}", contract.Id);
                    }
                }
            }
        }
    }
}
