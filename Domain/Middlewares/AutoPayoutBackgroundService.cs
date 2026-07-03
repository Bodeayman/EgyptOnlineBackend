using EgyptOnline.Application.Services.Contract;
using Microsoft.Extensions.Hosting;
using Serilog;

public class AutoPayoutBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AutoPayoutBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("AutoPayoutBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Egypt Standard Time = UTC+3 (fixed offset, platform-independent)
                var egyptTime = DateTime.UtcNow.AddHours(3);

                // Only run heavy DB queries at or after 5:00 PM Egypt time
                if (egyptTime.TimeOfDay >= new TimeSpan(17, 0, 0))
                {
                    // ContractAutoPayoutWorker handles auto-payouts independently
                    // This service is kept for potential future scheduling logic
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AutoPayoutBackgroundService encountered an error");
            }

            // Check every 60 seconds
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
