using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Models;

namespace EgyptOnline.Services
{
    public class UserSubscriptionServices
    {
        private readonly ApplicationDbContext _context;
        public UserSubscriptionServices(ApplicationDbContext context)
        {
            _context = context;
        }
        public Subscription AddSubscription(User user)
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
    }
}