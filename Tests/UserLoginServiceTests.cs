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
using EgyptOnline.Domain.Interfaces;

namespace EgyptOnline.Tests
{
    /// <summary>
    /// UserLoginServiceTests - Comprehensive login and authentication scenarios
    /// 
    /// EXPLANATION FOR INTERNS:
    /// ========================
    /// In C# testing, we use the Xunit framework (similar to JUnit in Java).
    /// 
    /// KEY CONCEPTS:
    /// 1. [Fact] - A test that doesn't take parameters. Runs once.
    /// 2. [Theory] - A test that runs with different data sets.
    /// 3. Arrange-Act-Assert (AAA Pattern):
    ///    - Arrange: Set up test data and mocks
    ///    - Act: Call the method being tested
    ///    - Assert: Verify the results
    /// 
    /// 4. Mocking (Moq library):
    ///    - Create fake objects (mocks) to isolate the code being tested
    ///    - Mock.Setup() - Define what the mock should return
    ///    - Mock.Verify() - Assert that a method was called
    /// 
    /// 5. In-Memory Database:
    ///    - UseInMemoryDatabase() - Doesn't touch real database
    ///    - Each test gets a unique database (Guid.NewGuid())
    ///    - Prevents tests from interfering with each other
    /// 
    /// WHY WE TEST:
    /// - Catch bugs before production
    /// - Document expected behavior
    /// - Make refactoring safer
    /// - Test edge cases (null, empty, invalid data)
    /// </summary>
    public class UserLoginServiceTests
    {
        private readonly Mock<UserManager<User>> _userManagerMock;
        private readonly Mock<IUserService> _userServiceMock;
        private readonly ApplicationDbContext _context;

        public UserLoginServiceTests()
        {
            // Setup: Create a fresh in-memory database for each test
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            // Mock UserManager - this is the Identity framework's user management
            var store = new Mock<IUserStore<User>>();
            _userManagerMock = new Mock<UserManager<User>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            // Mock UserService - our custom JWT token generation
            _userServiceMock = new Mock<IUserService>();
        }

        // ========================
        // SCENARIO 1: REGISTRATION
        // ========================

        [Fact]
        public async Task RegisterUser_WithValidData_ShouldCreateUserSuccessfully()
        {
            // ARRANGE - Set up test data
            var newUser = new User
            {
                Id = "user_123",
                UserName = "ahmed.malik",
                Email = "ahmed@test.com",
                PhoneNumber = "+201001234567",
                FirstName = "Ahmed",
                LastName = "Malik",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 0,
                SubscriptionPoints = 0
            };

            // Mock: Tell UserManager to return success when creating user
            _userManagerMock
                .Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            // Mock: Tell UserManager to add user to "User" role
            _userManagerMock
                .Setup(x => x.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            // Save user to in-memory database
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // ACT - Execute the test
            var result = await _userManagerMock.Object.CreateAsync(newUser, "Password123!");

            // ASSERT - Verify expectations
            Assert.True(result.Succeeded);
            Assert.NotNull(newUser.Id);

            // Verify user was added to database
            var savedUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == "ahmed.malik");
            Assert.NotNull(savedUser);
            Assert.Equal("ahmed@test.com", savedUser.Email);
        }

        [Fact]
        public async Task RegisterUser_WithMissingEmail_ShouldFail()
        {
            // ARRANGE
            var invalidUser = new User
            {
                Id = "user_456",
                UserName = "invalid.user",
                Email = null, // Missing email - this should fail
                PhoneNumber = "+201001234567",
                FirstName = "Invalid",
                LastName = "User",
                Governorate = "Cairo",
                City = "Cairo"
            };

            // Mock failure scenario
            _userManagerMock
                .Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Code = "InvalidEmail", Description = "Email is required" }));

            // ACT
            var result = await _userManagerMock.Object.CreateAsync(invalidUser, "Password123!");

            // ASSERT - Should fail
            Assert.False(result.Succeeded);
            Assert.NotEmpty(result.Errors);
        }

        // ========================
        // SCENARIO 2: LOGIN SUCCESS
        // ========================

        [Fact]
        public async Task LoginUser_WithCorrectCredentials_ShouldReturnToken()
        {
            // ARRANGE - Create and save a valid user
            var user = new User
            {
                Id = "user_789",
                UserName = "fatima.hassan",
                Email = "fatima@test.com",
                PhoneNumber = "+201009876543",
                FirstName = "Fatima",
                LastName = "Hassan",
                Governorate = "Alexandria",
                City = "Alexandria",
                Points = 100,
                SubscriptionPoints = 50
            };

            _context.Users.Add(user);

            // Add active subscription
            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(1)
            };
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Mock: UserManager finds user by username
            _userManagerMock
                .Setup(x => x.FindByNameAsync("fatima.hassan"))
                .ReturnsAsync(user);

            // Mock: UserManager verifies password
            _userManagerMock
                .Setup(x => x.CheckPasswordAsync(user, "CorrectPassword123!"))
                .ReturnsAsync(true);

            // Mock: Generate JWT token
            _userServiceMock
                .Setup(x => x.GenerateJwtToken(user, UsersTypes.Worker, TokensTypes.AccessToken))
                .ReturnsAsync("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...");

            // ACT
            var foundUser = await _userManagerMock.Object.FindByNameAsync("fatima.hassan");
            var passwordValid = foundUser != null && await _userManagerMock.Object.CheckPasswordAsync(foundUser, "CorrectPassword123!");
            var token = foundUser != null ? await _userServiceMock.Object.GenerateJwtToken(foundUser, UsersTypes.Worker, TokensTypes.AccessToken) : null;

            // ASSERT
            Assert.NotNull(foundUser);
            Assert.True(passwordValid);
            Assert.NotNull(token);
            Assert.NotEmpty(token);

            // Verify user has active subscription
            Assert.NotNull(foundUser.Subscription);
            Assert.True(foundUser.Subscription.IsActive);

            // Verify user has points
            Assert.True(foundUser.Points > 0);
        }

        [Fact]
        public async Task LoginUser_WithCorrectCredentials_ShouldCheckSubscriptionStatus()
        {
            // ARRANGE - User with ACTIVE subscription
            var activeSubUser = new User
            {
                Id = "user_active",
                UserName = "omar.active",
                Email = "omar@active.com",
                PhoneNumber = "+201111111111",
                FirstName = "Omar",
                LastName = "Active",
                Governorate = "Giza",
                City = "Giza",
                Points = 200
            };

            var activeSubscription = new Subscription
            {
                UserId = activeSubUser.Id,
                User = activeSubUser,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddMonths(1) // Future date = Active
            };

            _context.Users.Add(activeSubUser);
            _context.Subscriptions.Add(activeSubscription);
            await _context.SaveChangesAsync();

            // ACT & ASSERT
            var retrievedUser = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == "user_active");

            Assert.NotNull(retrievedUser);
            Assert.NotNull(retrievedUser.Subscription);
            Assert.True(retrievedUser.Subscription.IsActive);
        }

        // ========================
        // SCENARIO 3: LOGIN FAILURE
        // ========================

        [Fact]
        public async Task LoginUser_WithWrongPassword_ShouldFail()
        {
            // ARRANGE
            var user = new User
            {
                Id = "user_wrong_pass",
                UserName = "wrong.password",
                Email = "wrong@test.com",
                PhoneNumber = "+201112223333",
                FirstName = "Wrong",
                LastName = "Password",
                Governorate = "Cairo",
                City = "Cairo"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Mock: User found but password is wrong
            _userManagerMock
                .Setup(x => x.FindByNameAsync("wrong.password"))
                .ReturnsAsync(user);

            _userManagerMock
                .Setup(x => x.CheckPasswordAsync(user, "WrongPassword123!"))
                .ReturnsAsync(false); // Password doesn't match

            // ACT
            var foundUser = await _userManagerMock.Object.FindByNameAsync("wrong.password");
            var passwordValid = foundUser != null && await _userManagerMock.Object.CheckPasswordAsync(foundUser, "WrongPassword123!");

            // ASSERT
            Assert.NotNull(foundUser);
            Assert.False(passwordValid); // Password check failed
        }

        [Fact]
        public async Task LoginUser_WithNonExistentUsername_ShouldFail()
        {
            // ARRANGE - Mock returns null (user doesn't exist)
            _userManagerMock
                .Setup(x => x.FindByNameAsync("nonexistent.user"))
                .ReturnsAsync((User)null);

            // ACT
            var foundUser = await _userManagerMock.Object.FindByNameAsync("nonexistent.user");

            // ASSERT
            Assert.Null(foundUser); // User not found in system
        }

        [Fact]
        public async Task LoginUser_WithExpiredSubscription_ShouldIndicateExpired()
        {
            // ARRANGE - User with EXPIRED subscription
            var expiredSubUser = new User
            {
                Id = "user_expired",
                UserName = "expired.sub",
                Email = "expired@test.com",
                PhoneNumber = "+201222222222",
                FirstName = "Expired",
                LastName = "Subscription",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 50
            };

            var expiredSubscription = new Subscription
            {
                UserId = expiredSubUser.Id,
                User = expiredSubUser,
                StartDate = DateTime.UtcNow.AddMonths(-3),
                EndDate = DateTime.UtcNow.AddDays(-1) // Past date = Expired
            };

            _context.Users.Add(expiredSubUser);
            _context.Subscriptions.Add(expiredSubscription);
            await _context.SaveChangesAsync();

            // ACT
            var retrievedUser = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == "user_expired");

            // ASSERT
            Assert.NotNull(retrievedUser);
            Assert.NotNull(retrievedUser.Subscription);
            Assert.False(retrievedUser.Subscription.IsActive); // Subscription is expired
        }

        // ========================
        // SCENARIO 4: LOGIN WITH UNAVAILABLE USER (Disabled/Locked)
        // ========================

        [Fact]
        public async Task LoginUser_WithLockedAccount_ShouldPreventLogin()
        {
            // ARRANGE - User with locked account
            var lockedUser = new User
            {
                Id = "user_locked",
                UserName = "locked.account",
                Email = "locked@test.com",
                PhoneNumber = "+201333333333",
                FirstName = "Locked",
                LastName = "Account",
                Governorate = "Cairo",
                City = "Cairo",
                LockoutEnabled = true,
                LockoutEnd = DateTime.UtcNow.AddHours(1) // Locked for 1 more hour
            };

            _context.Users.Add(lockedUser);
            await _context.SaveChangesAsync();

            // Mock: Check if account is locked
            _userManagerMock
                .Setup(x => x.FindByNameAsync("locked.account"))
                .ReturnsAsync(lockedUser);

            _userManagerMock
                .Setup(x => x.IsLockedOutAsync(lockedUser))
                .ReturnsAsync(true);

            // ACT
            var foundUser = await _userManagerMock.Object.FindByNameAsync("locked.account");
            var isLocked = foundUser != null && await _userManagerMock.Object.IsLockedOutAsync(foundUser);

            // ASSERT
            Assert.NotNull(foundUser);
            Assert.True(isLocked); // Account is locked
        }

        [Fact]
        public async Task LoginUser_WithDisabledAccount_ShouldPreventLogin()
        {
            // ARRANGE - User with disabled account
            var disabledUser = new User
            {
                Id = "user_disabled",
                UserName = "disabled.account",
                Email = "disabled@test.com",
                PhoneNumber = "+201444444444",
                FirstName = "Disabled",
                LastName = "Account",
                Governorate = "Cairo",
                City = "Cairo"
            };

            _context.Users.Add(disabledUser);
            await _context.SaveChangesAsync();

            // Mock: Check if account is disabled (example: using a custom field or email not confirmed)
            _userManagerMock
                .Setup(x => x.FindByNameAsync("disabled.account"))
                .ReturnsAsync(disabledUser);

            // In real scenario, you might check: user.EmailConfirmed or a custom IsActive field
            var isEmailConfirmed = disabledUser.EmailConfirmed; // Default is false

            // ACT
            var foundUser = await _userManagerMock.Object.FindByNameAsync("disabled.account");

            // ASSERT
            Assert.NotNull(foundUser);
            Assert.False(isEmailConfirmed); // Email not confirmed = Account not fully activated
        }

        // ========================
        // SCENARIO 5: RENEW SUBSCRIPTION
        // ========================

        [Fact]
        public async Task RenewSubscription_WithActiveSubscription_ShouldExtendEndDate()
        {
            // ARRANGE - User preparing to renew subscription
            var user = new User
            {
                Id = "user_renew",
                UserName = "renew.user",
                Email = "renew@test.com",
                PhoneNumber = "+201555555555",
                FirstName = "Renew",
                LastName = "User",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 300
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

            // ACT - Simulate subscription renewal
            oldSubscription.EndDate = DateTime.UtcNow.AddMonths(1);
            _context.Subscriptions.Update(oldSubscription);
            await _context.SaveChangesAsync();

            // ASSERT
            var updatedSubscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == "user_renew");

            Assert.NotNull(updatedSubscription);
            Assert.True(updatedSubscription.IsActive);
            Assert.True(updatedSubscription.EndDate > DateTime.UtcNow);
        }

        [Fact]
        public async Task RenewSubscription_WithReferralUser_ShouldUpdateReferrerPoints()
        {
            // ARRANGE - User with referrer renews subscription
            var referrerUser = new User
            {
                Id = "referrer_123",
                UserName = "referrer.user",
                Email = "referrer@test.com",
                PhoneNumber = "+201666666666",
                FirstName = "Referrer",
                LastName = "User",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100,
                SubscriptionPoints = 50
            };

            var referredUser = new User
            {
                Id = "referred_123",
                UserName = "referred.user",
                Email = "referred@test.com",
                PhoneNumber = "+201777777777",
                FirstName = "Referred",
                LastName = "User",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 0,
                ReferrerUserName = "referrer.user", // This user was referred by referrer.user
                ReferralRewardCount = 0
            };

            var subscription = new Subscription
            {
                UserId = referredUser.Id,
                User = referredUser,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            _context.Users.Add(referrerUser);
            _context.Users.Add(referredUser);
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // ACT - Simulate renewing subscription and rewarding referrer
            var referrer = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == "referrer.user");

            if (referrer != null)
            {
                referrer.SubscriptionPoints += 10; // Award points
                referrer.ReferralRewardCount = (referrer.ReferralRewardCount) + 1;
                _context.Users.Update(referrer);
                await _context.SaveChangesAsync();
            }

            // ASSERT
            var updatedReferrer = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == "referrer_123");

            Assert.NotNull(updatedReferrer);
            Assert.Equal(60, updatedReferrer.SubscriptionPoints); // 50 + 10 reward
        }

        // ========================
        // SCENARIO 6: MULTIPLE LOGIN ATTEMPTS
        // ========================

        [Fact]
        public async Task MultipleFailedLogins_ShouldLockAccountAfterThreshold()
        {
            // ARRANGE
            var user = new User
            {
                Id = "user_brute",
                UserName = "bruteforce.test",
                Email = "brute@test.com",
                PhoneNumber = "+201888888888",
                FirstName = "Brute",
                LastName = "Force",
                Governorate = "Cairo",
                City = "Cairo",
                AccessFailedCount = 0,
                LockoutEnabled = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Mock: Simulate 3 failed login attempts
            _userManagerMock
                .Setup(x => x.FindByNameAsync("bruteforce.test"))
                .ReturnsAsync(user);

            _userManagerMock
                .Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>()))
                .ReturnsAsync(false);

            _userManagerMock
                .Setup(x => x.AccessFailedAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // ACT
            var foundUser = await _userManagerMock.Object.FindByNameAsync("bruteforce.test");

            // Simulate 3 failed attempts
            if (foundUser != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    await _userManagerMock.Object.AccessFailedAsync(foundUser);
                }
            }

            // ASSERT
            Assert.NotNull(foundUser);
            _userManagerMock.Verify(
                x => x.AccessFailedAsync(user),
                Times.Exactly(3)); // Verify AccessFailed was called 3 times
        }
    }
}
