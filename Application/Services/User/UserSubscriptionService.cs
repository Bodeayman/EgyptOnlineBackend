using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Services
{
    public class UserSubscriptionServices
    {
        private readonly ApplicationDbContext _context;
        private readonly UserPointService _userPointService;
        public UserSubscriptionServices(ApplicationDbContext context, UserPointService userPointService)
        {
            _context = context;
            _userPointService = userPointService;
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
                    StartDate = EgyptTimeHelper.TodayInEgypt(),
                    EndDate = EgyptTimeHelper.TodayInEgypt().AddMonths(1)
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

                var FoundSubscription = await _context.Subscriptions.Include(s => s.User).FirstOrDefaultAsync(U => U.UserId == user.Id);
                FoundSubscription.EndDate = EgyptTimeHelper.TodayInEgypt().AddMonths(1);

                // Check for referral reward
                if (!string.IsNullOrEmpty(FoundSubscription.User.ReferrerUserName) && FoundSubscription.User.ReferralRewardCount < 5)
                {
                    _userPointService.AddSubscriptionPointsToUser(FoundSubscription.User.ReferrerUserName);
                    FoundSubscription.User.ReferralRewardCount++;
                }

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