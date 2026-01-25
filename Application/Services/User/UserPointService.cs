using EgyptOnline.Data;
using EgyptOnline.Utilities;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Services
{
    public class UserPointService
    {
        private readonly ApplicationDbContext _context;
        public UserPointService(ApplicationDbContext context)
        {
            _context = context;

        }
        /// <summary>
        /// Add registration points to user based on their provider type.
        /// Uses centralized pricing configuration for single source of truth.
        /// </summary>
        public bool AddPointsToUser(string userId, string providerType)
        {
            var user = _context.Users.Include(u => u.ServiceProvider).FirstOrDefault(u => u.UserName == userId);
            if (user != null)
            {
                int points = ProviderPricingConfig.GetRegistrationPoints(providerType);
                user.Points += points;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Add subscription renewal points to referrer user.
        /// Uses centralized pricing configuration for single source of truth.
        /// </summary>
        public bool AddSubscriptionPointsToUser(string referrerUserName, string providerType)
        {
            var user = _context.Users.Include(u => u.ServiceProvider).FirstOrDefault(u => u.UserName == referrerUserName);
            if (user != null)
            {
                int points = ProviderPricingConfig.GetSubscriptionPoints(providerType);
                user.SubscriptionPoints += points;
                return true;
            }
            return false;
        }
    }
}