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
        Console.WriteLine("SubscriptionCheckerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Fetch all users with subscriptions and related data
                var users = await db.Users
                                    .Include(u => u.Subscription)
                                    .Include(u => u.ServiceProvider)
                                    .Include(u => u.RefreshTokens)
                                    .Where(u => u.Subscription != null)
                                    .ToListAsync(stoppingToken);
                Console.WriteLine($"Checking {users.Count} users at {DateTime.UtcNow}");

                var today = EgyptTimeHelper.TodayInEgypt(); // Get today's date in Egypt

                foreach (var user in users)
                {
                    if (user.Subscription.EndDate < today)
                    {
                        // Revoke refresh tokens
                        if (user.RefreshTokens != null)
                        {
                            foreach (var token in user.RefreshTokens.Where(t => !t.IsRevoked))
                            {
                                token.IsRevoked = true;
                                token.Revoked = DateTime.UtcNow;
                                db.Entry(token).State = EntityState.Modified;
                            }
                        }

                        // Disable service provider
                        if (user.ServiceProvider != null)
                        {
                            user.ServiceProvider.IsAvailable = false;
                            db.Entry(user.ServiceProvider).State = EntityState.Modified;
                        }

                        db.Entry(user).State = EntityState.Modified;
                    }
                }

                // Save all changes
                await db.SaveChangesAsync(stoppingToken);
                Console.WriteLine("Database updated successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SubscriptionCheckerService: {ex.Message}");
            }

            // Calculate delay until next midnight in Egypt
            var nowEgypt = EgyptTimeHelper.NowInEgypt();
            var tomorrowMidnightEgypt = nowEgypt.Date.AddDays(1);
            var tomorrowMidnightUtc = EgyptTimeHelper.ToUtc(tomorrowMidnightEgypt);

            var delay = tomorrowMidnightUtc - DateTime.UtcNow;

            if (delay <= TimeSpan.Zero)
                delay = TimeSpan.FromMinutes(1);

            Console.WriteLine($"Next check at: {tomorrowMidnightEgypt} Egypt time (in {delay.TotalHours:F2} hours)");

            await Task.Delay(delay, stoppingToken);
        }
    }
}