using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Services
{
    public class UserSubscriptionServices
    {
        private readonly ApplicationDbContext _context;
        public UserSubscriptionServices(ApplicationDbContext context)
        {
            _context = context;
        }
        public Subscription? AddSubscriptionForANewUser(User user)
        {
            try
            {
                Console.WriteLine("Subscription is added here");
                Subscription Subscription = new Subscription
                {
                    User = user,

                    UserId = user.Id,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(1)
                };
                _context.Subscriptions.Add(Subscription);
                return Subscription;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
        public async Task<Subscription?> RenewSubscription(User user)
        {
            try
            {
                Console.WriteLine("Subscription is added here");

                var FoundSubscription = await _context.Subscriptions.FirstOrDefaultAsync(U => U.UserId == user.Id);
                FoundSubscription.EndDate = DateTime.UtcNow.AddMonths(1);
                return FoundSubscription;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}