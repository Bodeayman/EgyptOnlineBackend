using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using EgyptOnline.Services;
using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using System.Threading.Tasks;
using System;

namespace EgyptOnline.Tests
{
    public class UserSubscriptionServiceTests
    {
        private readonly Mock<UserPointService> _userPointServiceMock;
        private readonly ApplicationDbContext _context;
        private readonly UserSubscriptionServices _service;

        public UserSubscriptionServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
            _userPointServiceMock = new Mock<UserPointService>(_context);
            _service = new UserSubscriptionServices(_context, _userPointServiceMock.Object);
        }

        [Fact]
        public async Task RenewSubscription_WithReferral_ShouldAddPointsAndIncrementCount()
        {
            // Arrange
            var user = new User 
            { 
                Id = "user1", 
                UserName = "user1", 
                ReferrerUserName = "referrer", 
                Governorate="Cairo",
                ReferralRewardCount = 1,
                City="Cairo",
                ServiceProvider = new Worker { UserId = "user1", Skill = "Test", WorkerType = WorkerTypes.PerDay, ProviderType = "Worker" }
            };
            var subscription = new Subscription 
            { 
                UserId = "user1", 
                User = user, 
                StartDate = DateTime.Now.AddMonths(-1), 
                EndDate = DateTime.Now 
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            _userPointServiceMock.Setup(x => x.AddSubscriptionPointsToUser("referrer", It.IsAny<string>())).Returns(true);

            // Act
            var result = await _service.RenewSubscription(user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.User.ReferralRewardCount);
            _userPointServiceMock.Verify(x => x.AddSubscriptionPointsToUser("referrer", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RenewSubscription_AtLimit_ShouldNotAddPoints()
        {
            // Arrange
            var user = new User 
            { 
                Id = "user2", 
                UserName = "user2", 
                ReferrerUserName = "referrer", 
                ReferralRewardCount = 5,
                 Governorate="Cairo",
                City="Cairo",
                ServiceProvider = new Worker { UserId = "user2", Skill = "Test", WorkerType = WorkerTypes.PerDay, ProviderType = "Worker" }
            };
            var subscription = new Subscription 
            { 
                UserId = "user2", 
                User = user, 
                StartDate = DateTime.Now.AddMonths(-1), 
                EndDate = DateTime.Now 
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.RenewSubscription(user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.User.ReferralRewardCount);
            _userPointServiceMock.Verify(x => x.AddPointsToUser(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
