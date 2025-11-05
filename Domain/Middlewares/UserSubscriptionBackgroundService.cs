using EgyptOnline.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

public class SubscriptionCheckerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SubscriptionCheckerService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate delay until next 12:00 AM
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1); // next midnight
            var delay = nextRun - now;

            await Task.Delay(delay, stoppingToken); // wait until midnight

            // Execute the job
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var Users = await db.Users
                    .Include(U => U.Subscription)
                    .Include(U => U.ServiceProvider)
                    .Where(s => s.Subscription!.IsActive)
                    .ToListAsync(stoppingToken);

                foreach (var user in Users)
                {
                    user.ServiceProvider.IsAvailable = false;
                }

                await db.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
