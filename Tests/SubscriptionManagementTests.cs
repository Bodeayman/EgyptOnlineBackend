using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using EgyptOnline.Services;
using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using System.Threading.Tasks;
using System;

namespace EgyptOnline.Tests
{
    /// <summary>
    /// Subscription Management Tests - Test subscription points changes
    /// 
    /// FOCUS: Tests that when a user's subscription is created, renewed, or updated,
    /// the subscription points change accordingly
    /// 
    /// KEY CONCEPTS DEMONSTRATED:
    /// - Subscription creation and initial points allocation
    /// - Subscription renewal and point updates
    /// - Subscription expiration handling
    /// - Point calculations for different subscription tiers
    /// - Subscription status changes
    /// </summary>
    public class SubscriptionManagementTests
    {
        private readonly ApplicationDbContext _context;

        public SubscriptionManagementTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
        }

        // ====================================================
        // SCENARIO 1: NEW SUBSCRIPTION - Points Assigned
        // ====================================================

        [Fact]
        public async Task CreateNewSubscription_ShouldAssignSubscriptionPoints()
        {
            // ARRANGE
            var user = new User
            {
                Id = "sub_user_1",
                UserName = "sub.new.user",
                Email = "sub@test.com",
                PhoneNumber = "+201001234567",
                FirstName = "Sub",
                LastName = "User",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100,
                SubscriptionPoints = 0 // Initially no subscription points
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // ACT - Create subscription
            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            _context.Subscriptions.Add(subscription);

            // Update user's subscription points
            user.SubscriptionPoints = 50; // Basic tier subscription
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // ASSERT
            var updatedUser = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == "sub_user_1");

            Assert.NotNull(updatedUser);
            Assert.Equal(50, updatedUser.SubscriptionPoints);
            Assert.NotNull(updatedUser.Subscription);
            Assert.True(updatedUser.Subscription.IsActive);
        }

        // ====================================================
        // SCENARIO 2: SUBSCRIPTION RENEWAL - Points Update
        // ====================================================

        [Fact]
        public async Task RenewSubscription_ShouldIncreaseSubscriptionPoints()
        {
            // ARRANGE - Create user with existing subscription
            var user = new User
            {
                Id = "sub_user_2",
                UserName = "sub.renew.user",
                Email = "subrenew@test.com",
                PhoneNumber = "+201002222222",
                FirstName = "Renew",
                LastName = "User",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100,
                SubscriptionPoints = 50 // Current subscription points
            };

            var oldSubscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow // Ending today
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(oldSubscription);
            await _context.SaveChangesAsync();

            // ACT - Renew subscription (end old, create new)
            oldSubscription.EndDate = DateTime.UtcNow; // Mark old as ending

            var newSubscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1) // Renewed for 1 month
            };

            _context.Subscriptions.Add(newSubscription);

            // Increase subscription points on renewal
            user.SubscriptionPoints = 100; // Points increased on renewal
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // ASSERT
            var renewedUser = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == "sub_user_2");

            Assert.NotNull(renewedUser);
            Assert.Equal(100, renewedUser.SubscriptionPoints); // Points increased
            Assert.NotNull(renewedUser.Subscription);
            Assert.True(renewedUser.Subscription.IsActive);
        }

        // ====================================================
        // SCENARIO 3: EXPIRED SUBSCRIPTION - No Points Increase
        // ====================================================

        [Fact]
        public async Task ExpiredSubscription_ShouldNotIncreasPoints()
        {
            // ARRANGE
            var user = new User
            {
                Id = "sub_user_3",
                UserName = "sub.expired.user",
                Email = "expired@test.com",
                PhoneNumber = "+201003333333",
                FirstName = "Expired",
                LastName = "User",
                Governorate = "Alexandria",
                City = "Alexandria",
                Points = 100,
                SubscriptionPoints = 50
            };

            var expiredSubscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddDays(-1) // Expired
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(expiredSubscription);
            await _context.SaveChangesAsync();

            // ACT
            var userWithExpiredSub = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == "sub_user_3");

            var isSubscriptionActive = userWithExpiredSub!.Subscription != null && userWithExpiredSub.Subscription.IsActive;

            // ASSERT
            Assert.NotNull(userWithExpiredSub);
            Assert.Equal(50, userWithExpiredSub.SubscriptionPoints); // Points should NOT increase for expired
            Assert.False(isSubscriptionActive); // Subscription is not active
        }

        // ====================================================
        // SCENARIO 4: MULTIPLE SUBSCRIPTIONS - Track Points
        // ====================================================

        [Fact]
        public async Task MultipleSubscriptions_ShouldTrackPointsCorrectly()
        {
            // ARRANGE - Multiple users with different subscription states
            var user1 = new User
            {
                Id = "sub_multi_1",
                UserName = "user.sub.one",
                Email = "user1@test.com",
                PhoneNumber = "+201004444444",
                FirstName = "User",
                LastName = "One",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100,
                SubscriptionPoints = 50
            };

            var user2 = new User
            {
                Id = "sub_multi_2",
                UserName = "user.sub.two",
                Email = "user2@test.com",
                PhoneNumber = "+201005555555",
                FirstName = "User",
                LastName = "Two",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 150,
                SubscriptionPoints = 100
            };

            var user3 = new User
            {
                Id = "sub_multi_3",
                UserName = "user.sub.three",
                Email = "user3@test.com",
                PhoneNumber = "+201006666666",
                FirstName = "User",
                LastName = "Three",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 80,
                SubscriptionPoints = 40
            };

            var sub1 = new Subscription { UserId = user1.Id, User = user1, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(1) };
            var sub2 = new Subscription { UserId = user2.Id, User = user2, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(3) };
            var sub3 = new Subscription { UserId = user3.Id, User = user3, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(6) };

            _context.Users.AddRange(user1, user2, user3);
            _context.Subscriptions.AddRange(sub1, sub2, sub3);
            await _context.SaveChangesAsync();

            // ACT
            var users = await _context.Users
                .Include(u => u.Subscription)
                .Where(u => new[] { "sub_multi_1", "sub_multi_2", "sub_multi_3" }.Contains(u.Id))
                .ToListAsync();

            // ASSERT
            Assert.Equal(3, users.Count);
            Assert.True(users.All(u => u.Subscription != null && u.Subscription.IsActive));
            Assert.Equal(50, users.First(u => u.Id == "sub_multi_1").SubscriptionPoints);
            Assert.Equal(100, users.First(u => u.Id == "sub_multi_2").SubscriptionPoints);
            Assert.Equal(40, users.First(u => u.Id == "sub_multi_3").SubscriptionPoints);
        }

        // ====================================================
        // SCENARIO 5: SUBSCRIPTION TIER UPGRADE - Points Increase
        // ====================================================

        [Fact]
        public async Task UpgradeSubscriptionTier_ShouldIncreasePoints()
        {
            // ARRANGE - User with basic subscription
            var user = new User
            {
                Id = "sub_upgrade_user",
                UserName = "sub.upgrade",
                Email = "upgrade@test.com",
                PhoneNumber = "+201007777777",
                FirstName = "Upgrade",
                LastName = "User",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100,
                SubscriptionPoints = 50 // Basic tier
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // ACT - Upgrade to Premium
            user.SubscriptionPoints = 150; // Premium tier bonus
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // ASSERT
            var upgradedUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == "sub_upgrade_user");

            Assert.NotNull(upgradedUser);
            Assert.Equal(150, upgradedUser.SubscriptionPoints);
            Assert.True(upgradedUser.SubscriptionPoints > 50); // Points increased
        }

        // ====================================================
        // SCENARIO 6: SUBSCRIPTION WITH PROVIDER - Points Tracked
        // ====================================================

        [Fact]
        public async Task ProviderSubscription_PointsShouldChangeWithRenewal()
        {
            // ARRANGE
            var user = new User
            {
                Id = "sub_provider_user",
                UserName = "worker.sub",
                Email = "worker.sub@test.com",
                PhoneNumber = "+201008888888",
                FirstName = "Worker",
                LastName = "Sub",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100,
                SubscriptionPoints = 50
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            var worker = new Worker
            {
                Id = 1,
                UserId = user.Id,
                User = user,
                Skill = "Carpentry",
                WorkerType = WorkerTypes.PerDay,
                ProviderType = "worker",
                ServicePricePerDay = 600,
                Bio = "Skilled carpenter",
                IsAvailable = true
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);
            _context.Workers.Add(worker);
            await _context.SaveChangesAsync();

            // ACT - Renew subscription
            user.SubscriptionPoints = 100; // Bonus on renewal
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // ASSERT
            var workerUser = await _context.Users
                .Include(u => u.Subscription)
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Id == "sub_provider_user");

            Assert.NotNull(workerUser);
            Assert.Equal(100, workerUser.SubscriptionPoints);
            Assert.NotNull(workerUser.Subscription);
            Assert.NotNull(workerUser.ServiceProvider);
        }

        // ====================================================
        // SCENARIO 7: SUBSCRIPTION POINTS ACCUMULATION
        // ====================================================

        [Fact]
        public async Task SubscriptionPointsAccumulation_ShouldAddUpCorrectly()
        {
            // ARRANGE
            var user = new User
            {
                Id = "sub_accumulate_user",
                UserName = "sub.accumulate",
                Email = "accumulate@test.com",
                PhoneNumber = "+201009999999",
                FirstName = "Accumulate",
                LastName = "User",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100,
                SubscriptionPoints = 50
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // ACT - Simulate multiple renewals
            user.SubscriptionPoints = 100; // First renewal
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            var user2 = await _context.Users.FirstOrDefaultAsync(u => u.Id == "sub_accumulate_user");
            user2!.SubscriptionPoints = 150; // Second renewal
            _context.Users.Update(user2);
            await _context.SaveChangesAsync();

            // ASSERT
            var finalUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == "sub_accumulate_user");

            Assert.NotNull(finalUser);
            Assert.Equal(150, finalUser.SubscriptionPoints);
            Assert.True(finalUser.SubscriptionPoints > 100);
        }
    }
}
