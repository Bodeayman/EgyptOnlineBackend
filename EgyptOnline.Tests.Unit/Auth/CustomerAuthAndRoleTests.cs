using System.Reflection;
using EgyptOnline.Application.Services.Contract;
using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Controllers;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Presentation.Controllers;
using EgyptOnline.Presentation.Controllers.V2;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using FakeItEasy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgyptOnline.Tests.Unit.Auth
{
    [Trait("Category", "Unit")]
    public class CustomerAuthAndRoleTests : UnitTestBase
    {
        private readonly IUserService _userServiceFake = A.Fake<IUserService>();
        private readonly IOTPService _otpServiceFake = A.Fake<IOTPService>();
        private readonly ICDNService _cdnServiceFake = A.Fake<ICDNService>();
        private readonly UserImageService _userImageService;
        private readonly UserPointService _userPointService;
        private readonly UserSubscriptionServices _userSubscriptionServices;
        private readonly UserRegisterationService _userRegistrationService;

        public CustomerAuthAndRoleTests()
        {
            _userImageService = new UserImageService(Context, _cdnServiceFake);
            _userPointService = new UserPointService(Context);
            _userSubscriptionServices = new UserSubscriptionServices(Context, _userPointService);

            A.CallTo(() => UserManagerFake.FindByEmailAsync(A<string>._)).Returns(Task.FromResult<User?>(null));
            A.CallTo(() => UserManagerFake.CreateAsync(A<User>._, A<string>._)).Returns(Task.FromResult(IdentityResult.Success));
            A.CallTo(() => UserManagerFake.AddToRoleAsync(A<User>._, A<string>._)).Returns(Task.FromResult(IdentityResult.Success));

            _userRegistrationService = new UserRegisterationService(
                UserManagerFake,
                _userPointService,
                _userSubscriptionServices,
                Context);
        }

        private AuthController BuildAuthController()
        {
            return new AuthController(
                UserManagerFake,
                _userRegistrationService,
                _userServiceFake,
                _otpServiceFake,
                Context,
                _userImageService);
        }

        [Fact]
        public async Task RegisterCustomer_CreatesCustomerWithoutWorkerProfile_AndAssignsCustomerRole()
        {
            var dto = new RegisterCustomerDto
            {
                FirstName = "Omar",
                LastName = "Customer",
                PhoneNumber = "01099887766",
                Password = "Password123!",
                Governorate = "Cairo",
                City = "Cairo",
                District = "Nasr City",
                Email = "omar.customer@example.com"
            };

            var result = await _userRegistrationService.RegisterCustomer(dto);

            Assert.Equal(IdentityResult.Success, result.Result);
            Assert.NotNull(result.User);

            // User should have wallet initialized
            var wallet = await Context.UserWallets.FirstOrDefaultAsync(w => w.UserId == result.User!.Id);
            Assert.NotNull(wallet);
            Assert.Equal(0, wallet.FreeBalance);

            // User should NOT have any ServicesProvider row attached
            var provider = await Context.ServiceProviders.FirstOrDefaultAsync(p => p.UserId == result.User!.Id);
            Assert.Null(provider);

            // User should have Customer role
            A.CallTo(() => UserManagerFake.AddToRoleAsync(result.User!, Roles.Customer)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task RegisterCustomer_DuplicatePhone_ReturnsFailure()
        {
            var dto1 = new RegisterCustomerDto
            {
                FirstName = "User1",
                PhoneNumber = "01012345678",
                Password = "Password123!",
                Governorate = "Cairo",
                City = "Cairo"
            };

            await _userRegistrationService.RegisterCustomer(dto1);

            var dto2 = new RegisterCustomerDto
            {
                FirstName = "User2",
                PhoneNumber = "01012345678",
                Password = "Password123!",
                Governorate = "Giza",
                City = "Giza"
            };

            var result = await _userRegistrationService.RegisterCustomer(dto2);

            Assert.False(result.Result.Succeeded);
            Assert.Equal("PhoneNumberAlreadyExists", result.Result.Errors.First().Code);
        }

        [Fact]
        public async Task Login_Customer_ReturnsRoleCustomer()
        {
            var user = new User
            {
                Id = "customer-login-id",
                UserName = "omar_customer",
                Email = "customer@example.com",
                PhoneNumber = "+201099887766",
                FirstName = "Omar",
                LastName = "Customer",
                Governorate = "Cairo",
                City = "Cairo",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            Context.Users.Add(user);
            await Context.SaveChangesAsync();

            A.CallTo(() => UserManagerFake.GetRolesAsync(A<User>._)).Returns(new List<string> { Roles.Customer });
            A.CallTo(() => UserManagerFake.CheckPasswordAsync(A<User>._, "Password123!")).Returns(true);
            A.CallTo(() => _userServiceFake.GenerateJwtToken(A<User>._, TokensTypes.AccessToken)).Returns("fake-access-token");
            A.CallTo(() => _userServiceFake.GenerateJwtToken(A<User>._, TokensTypes.RefreshToken)).Returns("fake-refresh-token");

            var controller = BuildAuthController();

            var actionResult = await controller.Login(new LoginWorkerDto
            {
                Email = "01099887766",
                Password = "Password123!"
            });

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var role = doc.RootElement.GetProperty("role").GetString();
            Assert.Equal(Roles.Customer, role);
        }

        [Fact]
        public async Task Login_Worker_ReturnsRoleUser()
        {
            var user = new User
            {
                Id = "worker-login-id",
                UserName = "ahmed_worker",
                Email = "worker@example.com",
                PhoneNumber = "+201011223344",
                FirstName = "Ahmed",
                LastName = "Worker",
                Governorate = "Cairo",
                City = "Cairo",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            Context.Users.Add(user);
            await Context.SaveChangesAsync();

            A.CallTo(() => UserManagerFake.GetRolesAsync(A<User>._)).Returns(new List<string> { Roles.User });
            A.CallTo(() => UserManagerFake.CheckPasswordAsync(A<User>._, "Password123!")).Returns(true);
            A.CallTo(() => _userServiceFake.GenerateJwtToken(A<User>._, TokensTypes.AccessToken)).Returns("fake-worker-access-token");
            A.CallTo(() => _userServiceFake.GenerateJwtToken(A<User>._, TokensTypes.RefreshToken)).Returns("fake-worker-refresh-token");

            var controller = BuildAuthController();

            var actionResult = await controller.Login(new LoginWorkerDto
            {
                Email = "01011223344",
                Password = "Password123!"
            });

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var role = doc.RootElement.GetProperty("role").GetString();
            Assert.Equal(Roles.User, role);
        }

        [Fact]
        public void AuthorizationRules_EnforceRoleBoundaries()
        {
            // 1. SearchController must be accessible ONLY to Worker (Roles.User) and NOT Customer
            var searchAuth = typeof(SearchController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(searchAuth);
            Assert.Equal(Roles.User, searchAuth.Roles);
            Assert.DoesNotContain(Roles.Customer, searchAuth.Roles);

            // 2. AdminController methods must be accessible ONLY to Admin
            var adminAuth = typeof(AdminController).GetMethod("GetPendingKYC")?.GetCustomAttribute<AuthorizeAttribute>()
                ?? typeof(AdminController).GetMethods().First(m => m.GetCustomAttribute<AuthorizeAttribute>() != null).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(adminAuth);
            Assert.Equal(Roles.Admin, adminAuth.Roles);

            // 3. CustomerController must be accessible ONLY to Customer
            var customerAuth = typeof(CustomerController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(customerAuth);
            Assert.Equal(Roles.Customer, customerAuth.Roles);

            // 4. ContractController class level allows both User and Customer
            var contractAuth = typeof(ContractController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(contractAuth);
            Assert.Contains(Roles.Customer, contractAuth.Roles);
            Assert.Contains(Roles.User, contractAuth.Roles);

            // 5. ContractController provider actions (Accept, Reject, Arrival) are strictly Worker (Roles.User)
            var acceptAuth = typeof(ContractController).GetMethod("Accept")?.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(acceptAuth);
            Assert.Equal(Roles.User, acceptAuth.Roles);

            var rejectAuth = typeof(ContractController).GetMethod("Reject")?.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(rejectAuth);
            Assert.Equal(Roles.User, rejectAuth.Roles);

            var arrivalAuth = typeof(ContractController).GetMethod("RegisterArrival")?.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(arrivalAuth);
            Assert.Equal(Roles.User, arrivalAuth.Roles);

            // 6. WalletController allows both Customer and User
            var walletAuth = typeof(WalletController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(walletAuth);
            Assert.Contains(Roles.Customer, walletAuth.Roles);
            Assert.Contains(Roles.User, walletAuth.Roles);

            // 7. RequestController allows both Customer and User at class level
            var requestAuth = typeof(RequestController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(requestAuth);
            Assert.Contains(Roles.Customer, requestAuth.Roles);
            Assert.Contains(Roles.User, requestAuth.Roles);

            // 8. RequestController worker actions (others, interest) are strictly Worker (Roles.User)
            var othersAuth = typeof(RequestController).GetMethod("GetOtherRequests")?.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(othersAuth);
            Assert.Equal(Roles.User, othersAuth.Roles);

            var interestAuth = typeof(RequestController).GetMethod("SetInterest")?.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(interestAuth);
            Assert.Equal(Roles.User, interestAuth.Roles);
        }
    }
}
