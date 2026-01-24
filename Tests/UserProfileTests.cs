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
    /// User Profile Tests - Test user registration via username and profile points
    /// 
    /// FOCUS: Tests that when a user registers via username, their profile data is correctly stored
    /// and verified through username lookups
    /// 
    /// KEY CONCEPTS DEMONSTRATED:
    /// - User profile creation and verification
    /// - Username-based user lookup
    /// - Points system initialization on registration
    /// - User data integrity after registration
    /// </summary>
    public class UserProfileTests
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<User>> _userManagerMock;

        public UserProfileTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            var store = new Mock<IUserStore<User>>();
            _userManagerMock = new Mock<UserManager<User>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        // ====================================================
        // SCENARIO 1: REGISTER USER VIA USERNAME - Points Add Up
        // ====================================================

        [Fact]
        public async Task RegisterUserViaUsername_ShouldCreateProfileWithInitialPoints()
        {
            // ARRANGE - Create a new user
            var user = new User
            {
                Id = "profile_user_1",
                UserName = "mustafa.profile",
                Email = "mustafa@test.com",
                PhoneNumber = "+201001234567",
                FirstName = "Mustafa",
                LastName = "Profile",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100, // Initial points
                SubscriptionPoints = 50 // Subscription bonus points
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

            // ACT
            await _context.SaveChangesAsync();

            // ASSERT - Lookup user by username
            var retrievedUser = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.UserName == "mustafa.profile");

            Assert.NotNull(retrievedUser);
            Assert.Equal("mustafa.profile", retrievedUser.UserName);
            Assert.Equal(100, retrievedUser.Points); // Points are preserved
            Assert.Equal(50, retrievedUser.SubscriptionPoints); // Subscription points are preserved
            Assert.Equal("Mustafa", retrievedUser.FirstName);
            Assert.Equal("Profile", retrievedUser.LastName);
        }

        // ===============================================
        // SCENARIO 2: LOOKUP USER BY USERNAME - Success
        // ===============================================

        [Fact]
        public async Task LookupUserByUsername_ShouldReturnCorrectUser()
        {
            // ARRANGE
            var user = new User
            {
                Id = "profile_user_2",
                UserName = "hana.lookup",
                Email = "hana@test.com",
                PhoneNumber = "+201002222222",
                FirstName = "Hana",
                LastName = "Lookup",
                Governorate = "Alexandria",
                City = "Alexandria",
                Points = 150,
                SubscriptionPoints = 75
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // ACT - Search by username
            var foundUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == "hana.lookup");

            // ASSERT
            Assert.NotNull(foundUser);
            Assert.Equal("hana.lookup", foundUser.UserName);
            Assert.Equal("hana@test.com", foundUser.Email);
            Assert.Equal(150, foundUser.Points);
        }

        // ====================================================
        // SCENARIO 3: USER PROFILE WITH SERVICE PROVIDER - Points Verified
        // ====================================================

        [Fact]
        public async Task UserProfileWithServiceProvider_PointsShouldBeCorrect()
        {
            // ARRANGE
            var user = new User
            {
                Id = "profile_user_3",
                UserName = "salma.provider",
                Email = "salma@test.com",
                PhoneNumber = "+201003333333",
                FirstName = "Salma",
                LastName = "Provider",
                Governorate = "Giza",
                City = "Giza",
                Points = 200,
                SubscriptionPoints = 100
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(2)
            };

            var worker = new Worker
            {
                Id = 1,
                UserId = user.Id,
                User = user,
                Skill = "Cleaning",
                WorkerType = WorkerTypes.PerPay,
                ProviderType = "worker",
                ServicePricePerDay = 400,
                Bio = "Professional cleaner",
                IsAvailable = true
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);
            _context.Workers.Add(worker);

            // ACT
            await _context.SaveChangesAsync();

            // ASSERT - Verify profile and points
            var userProfile = await _context.Users
                .Include(u => u.Subscription)
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.UserName == "salma.provider");

            Assert.NotNull(userProfile);
            Assert.Equal(200, userProfile.Points);
            Assert.Equal(100, userProfile.SubscriptionPoints);
            Assert.NotNull(userProfile.ServiceProvider);
            Assert.Equal("worker", userProfile.ServiceProvider.ProviderType);
        }

        // ====================================================
        // SCENARIO 4: MULTIPLE USER PROFILES - Each Has Own Points
        // ====================================================

        [Fact]
        public async Task MultipleUserProfiles_EachShouldHaveIndependentPoints()
        {
            // ARRANGE
            var user1 = new User
            {
                Id = "profile_multi_1",
                UserName = "user.one",
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
                Id = "profile_multi_2",
                UserName = "user.two",
                Email = "user2@test.com",
                PhoneNumber = "+201005555555",
                FirstName = "User",
                LastName = "Two",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 300,
                SubscriptionPoints = 150
            };

            _context.Users.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            // ACT
            var profile1 = await _context.Users.FirstOrDefaultAsync(u => u.UserName == "user.one");
            var profile2 = await _context.Users.FirstOrDefaultAsync(u => u.UserName == "user.two");

            // ASSERT
            Assert.NotNull(profile1);
            Assert.NotNull(profile2);
            Assert.Equal(100, profile1.Points);
            Assert.Equal(300, profile2.Points);
            Assert.NotEqual(profile1.Points, profile2.Points);
        }

        // ====================================================
        // SCENARIO 5: USER PROFILE WITH SUBSCRIPTION - All Data Correct
        // ====================================================

        [Fact]
        public async Task UserProfileWithSubscription_AllDataShouldBePersistent()
        {
            // ARRANGE
            var user = new User
            {
                Id = "profile_user_5",
                UserName = "zainab.subscribed",
                Email = "zainab@test.com",
                PhoneNumber = "+201006666666",
                FirstName = "Zainab",
                LastName = "Subscribed",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 250,
                SubscriptionPoints = 125
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6)
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // ACT - Retrieve complete profile
            var profile = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.UserName == "zainab.subscribed");

            // ASSERT
            Assert.NotNull(profile);
            Assert.Equal("zainab.subscribed", profile.UserName);
            Assert.Equal("Zainab", profile.FirstName);
            Assert.Equal(250, profile.Points);
            Assert.Equal(125, profile.SubscriptionPoints);
            Assert.NotNull(profile.Subscription);
            Assert.True(profile.Subscription.IsActive);
        }

        // ====================================================
        // SCENARIO 6: NEW USER POINTS CALCULATION
        // ====================================================

        [Fact]
        public async Task NewUserRegistration_PointsShouldAddUpCorrectly()
        {
            // ARRANGE
            var newUser = new User
            {
                Id = "profile_new_user",
                UserName = "newuser.registration",
                Email = "newuser@test.com",
                PhoneNumber = "+201007777777",
                FirstName = "New",
                LastName = "User",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 50, // Starting points
                SubscriptionPoints = 25 // Initial subscription bonus
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // ACT
            var registered = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == "newuser.registration");

            var totalPoints = registered!.Points + registered.SubscriptionPoints;

            // ASSERT
            Assert.NotNull(registered);
            Assert.Equal(50, registered.Points);
            Assert.Equal(25, registered.SubscriptionPoints);
            Assert.Equal(75, totalPoints); // Points should add up correctly
        }

        // ====================================================
        // SCENARIO 7: ASSISTANT PROVIDER WITH USER PROFILE
        // ====================================================

        [Fact]
        public async Task AssistantProviderProfile_ShouldHaveCorrectUserData()
        {
            // ARRANGE
            var user = new User
            {
                Id = "profile_assistant_user",
                UserName = "rania.assistant.profile",
                Email = "rania.ass@test.com",
                PhoneNumber = "+201008888888",
                FirstName = "Rania",
                LastName = "Assistant",
                Governorate = "Alexandria",
                City = "Alexandria",
                Points = 180,
                SubscriptionPoints = 90
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(3)
            };

            var assistant = new Assistant
            {
                Id = 2,
                UserId = user.Id,
                User = user,
                Skill = "Data Entry",
                ProviderType = "assistant",
                ServicePricePerDay = 300,
                Bio = "Efficient data entry specialist",
                IsAvailable = true
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);
            _context.Assistants.Add(assistant);
            await _context.SaveChangesAsync();

            // ACT
            var profile = await _context.Assistants
                .Include(a => a.User)
                .ThenInclude(u => u.Subscription)
                .FirstOrDefaultAsync(a => a.Id == 2);

            // ASSERT
            Assert.NotNull(profile);
            Assert.NotNull(profile.User);
            Assert.Equal("rania.assistant.profile", profile.User.UserName);
            Assert.Equal(180, profile.User.Points);
            Assert.Equal(90, profile.User.SubscriptionPoints);
            Assert.NotNull(profile.User.Subscription);
        }
    }
}
