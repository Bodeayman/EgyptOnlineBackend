using EgyptOnline.Data;
using EgyptOnline.Domain.Models;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EgyptOnline.Infrastructure.BackgroundWorkers
{
    public class ContractAutoPayoutWorker : BackgroundService
    {
        private readonly ApplicationDbContext _context;
        private readonly WalletService _walletService;
        private readonly ILogger<ContractAutoPayoutWorker> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _gracePeriod = TimeSpan.FromHours(3);

        public ContractAutoPayoutWorker(
            ApplicationDbContext context,
            WalletService walletService,
            ILogger<ContractAutoPayoutWorker> logger)
        {
            _context = context;
            _walletService = walletService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Contract Auto-Payout Worker started at: {Time}", DateTimeOffset.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAutoPayoutsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during auto-payout processing");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Contract Auto-Payout Worker stopped at: {Time}", DateTimeOffset.UtcNow);
        }

        private async Task ProcessAutoPayoutsAsync(CancellationToken stoppingToken)
        {
            var currentTime = DateTime.UtcNow;

            var activeContracts = await _context.Contracts
                .Include(c => c.ContractDays)
                .Where(c => c.Status == "active")
                .ToListAsync(stoppingToken);

            foreach (var contract in activeContracts)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                await ProcessContractDaysAsync(contract, currentTime, stoppingToken);
            }
        }

        private async Task ProcessContractDaysAsync(Contract contract, DateTime currentTime, CancellationToken stoppingToken)
        {
            foreach (var contractDay in contract.ContractDays)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                if (contractDay.IsProcessed)
                    continue;

                if (!contractDay.ProviderArrived)
                    continue;

                if (contractDay.Status == ContractDayStatus.AbsentDisputed)
                    continue;

                var shiftEndTime = contract.ShiftEndTime.Value;
                var gracePeriodEnd = contractDay.Date.Add(shiftEndTime).Add(_gracePeriod);

                if (currentTime <= gracePeriodEnd)
                    continue;

                await ProcessDayPayoutAsync(contract, contractDay, currentTime);
            }
        }

        private async Task ProcessDayPayoutAsync(Contract contract, ContractDay contractDay, DateTime currentTime)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                contractDay.Status = ContractDayStatus.Completed;
                contractDay.IsProcessed = true;
                contractDay.ProcessedAt = currentTime;

                await _walletService.SubtractFromFrozenBalanceAsync(contract.ClientUserId, contract.DailyRate.Value);

                await _walletService.SubtractFromFrozenBalanceAsync(contract.ServiceProviderUserId, contract.DailyRate.Value);
                await _walletService.AddToFreeBalanceAsync(contract.ServiceProviderUserId, contract.DailyRate.Value);

                var allDaysProcessed = contract.ContractDays.All(cd => cd.IsProcessed);
                if (allDaysProcessed)
                {
                    contract.Status = "completed";
                    contract.CompletedAt = currentTime;

                    _logger.LogInformation("Contract {ContractId} completed after auto-payout of final day", contract.Id);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Auto-payout processed for Contract {ContractId}, Day {DayNumber}. Amount: {Amount}. Grace period expired at: {GracePeriodEnd}",
                    contract.Id, contractDay.DayNumber, contract.DailyRate.Value, contractDay.Date.Add(contract.ShiftEndTime.Value).Add(_gracePeriod));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process auto-payout for Contract {ContractId}, Day {DayNumber}", contract.Id, contractDay.DayNumber);
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Contract Auto-Payout Worker is stopping...");
            await base.StopAsync(cancellationToken);
        }
    }
}
