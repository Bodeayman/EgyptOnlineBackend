using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using EgyptOnline.Services;
using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Dtos;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace EgyptOnline.Tests
{
    public class UserRegisterationServiceTests
    {
        private readonly Mock<UserManager<User>> _userManagerMock;
        private readonly Mock<UserPointService> _userPointServiceMock;
        private readonly Mock<UserSubscriptionServices> _userSubscriptionServiceMock;
        private readonly ApplicationDbContext _context;

        public UserRegisterationServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            var store = new Mock<IUserStore<User>>();
            var identityOptionsMock = new Mock<IOptions<IdentityOptions>>();
            identityOptionsMock.Setup(o => o.Value).Returns(new IdentityOptions());

            _userManagerMock = new Mock<UserManager<User>>(
                store.Object, 
                identityOptionsMock.Object, 
                new Mock<IPasswordHasher<User>>().Object,
                new List<IUserValidator<User>>(),
                new List<IPasswordValidator<User>>(),
                new Mock<ILookupNormalizer>().Object,
                new IdentityErrorDescriber(),
                new Mock<IServiceProvider>().Object,
                new Mock<Microsoft.Extensions.Logging.ILogger<UserManager<User>>>().Object);
            
            _userPointServiceMock = new Mock<UserPointService>(_context);
            _userSubscriptionServiceMock = new Mock<UserSubscriptionServices>(_context, _userPointServiceMock.Object);
        }

        [Fact]
        public async Task RegisterUser_WithReferral_ShouldSetReferrerAndCount()
        {
            // Arrange
            var existingUser = new User 
            { 
                UserName = "referrerUser", 
                Email = "ref@test.com", 
                PhoneNumber = "+201000000000" ,
                 Governorate="Cairo",
                City="Cairo"
            };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var service = new UserRegisterationService(
                _userManagerMock.Object, 
                _userPointServiceMock.Object, 
                _userSubscriptionServiceMock.Object, 
                _context);

            var model = new RegisterWorkerDto
            {
                Email = "newuser@test.com",
                PhoneNumber = "01122334455",
                FirstName = "New",
                LastName = "User",
                Password = "Password123!",
                ReferralUserName = "referrerUser",
                 Governorate="Cairo",
                City="Cairo"
            };

            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
             _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            _userSubscriptionServiceMock.Setup(x => x.AddSubscriptionForANewUser(It.IsAny<User>()))
                .Returns(new Subscription { User = new User {  Governorate="Cairo",
                City="Cairo" }, UserId = "test" });
            _userPointServiceMock.Setup(x => x.AddPointsToUser(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

            // Act
            var result = await service.RegisterUser(model);

            // Assert
            Assert.True(result.Result.Succeeded);
            Assert.Equal("referrerUser", result.User.ReferrerUserName);
            Assert.Equal(0, result.User.ReferralRewardCount);
        }

        [Fact]
        public async Task RegisterUser_WithoutReferral_ShouldNotSetReferrer()
        {
            // Arrange
            var service = new UserRegisterationService(
                _userManagerMock.Object, 
                _userPointServiceMock.Object, 
                _userSubscriptionServiceMock.Object, 
                _context);

            var model = new RegisterWorkerDto
            {
                Email = "newuser2@test.com",
                PhoneNumber = "01122334466",
                FirstName = "New",
                LastName = "User2",
                Password = "Password123!",
                 Governorate="Cairo",
                City="Cairo"
            };

            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
                 _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            _userSubscriptionServiceMock.Setup(x => x.AddSubscriptionForANewUser(It.IsAny<User>()))
                .Returns(new Subscription { User = new User {  Governorate="Cairo",
                City="Cairo" }, UserId = "test" });

            // Act
            var result = await service.RegisterUser(model);

            // Assert
            Assert.True(result.Result.Succeeded);
            Assert.Null(result.User.ReferrerUserName);
            Assert.Equal(0, result.User.ReferralRewardCount);
        }
    }
}
