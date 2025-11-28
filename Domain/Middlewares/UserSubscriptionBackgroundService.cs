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

                var today = DateOnly.FromDateTime(DateTime.UtcNow); // use UTC for consistency


                foreach (var user in users)
                {
                    if (user.Subscription.EndDate <= today)
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

                        // Optional: mark the user entity as modified (not always needed)
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

            // Wait for 10 seconds for testing
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1); // next midnight UTC
            var delay = nextMidnight - now;

            // Safety: if somehow delay is zero or negative, wait 1 minute instead
            if (delay <= TimeSpan.Zero)
                delay = TimeSpan.FromMinutes(1);

            await Task.Delay(delay, stoppingToken);
        }
    }
}



// In production, replace with:
// var now = DateTime.UtcNow;
// var nextMidnight = now.Date.AddDays(1);
// var delay = nextMidnight - now;
// if (delay <= TimeSpan.Zero) delay = TimeSpan.FromSeconds(1);
// await Task.Delay(delay, stoppingToken);