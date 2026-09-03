using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Controllers;
using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Domain.Models;
using EgyptOnline.Dtos;
using EgyptOnline.Infrastructure;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xunit;

namespace EgyptOnline.Tests.Integration.Concurrency;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class WalletAndAuthConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public WalletAndAuthConcurrencyIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private WalletService CreateWalletService(ApplicationDbContext dbContext)
    {
        var notifFake = A.Fake<INotificationService>();
        var logger = A.Fake<ILogger<WalletService>>();
        var emailFake = A.Fake<IEmailService>();
        var userManagerFake = A.Fake<UserManager<User>>();
        return new WalletService(dbContext, notifFake, logger, emailFake, userManagerFake);
    }

    private async Task SeedUserWithApprovedKycAsync(ApplicationDbContext dbContext, string userId, string phoneNumber = "+201000000001")
    {
        var user = new User
        {
            Id = userId,
            UserName = $"user_{userId}",
            Email = $"{userId}@test.com",
            PhoneNumber = phoneNumber,
            Governorate = "Cairo",
            City = "Cairo"
        };
        dbContext.Users.Add(user);

        var kyc = new KycSubmission
        {
            UserId = userId,
            FrontImagePath = "path/front.jpg",
            BackImagePath = "path/back.jpg",
            SelfieImagePath = "path/selfie.jpg",
            Status = "approved"
        };
        dbContext.KycSubmissions.Add(kyc);

        var wallet = new UserWallet
        {
            UserId = userId,
            FreeBalance = 0,
            FrozenBalance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.UserWallets.Add(wallet);

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task ConcurrentWithdrawals_PreventsDoubleSpendingAndNegativeBalance()
    {
        var userId = "user-conc-withdraw";

        using (var setupDb = _fixture.GetDbContext())
        {
            await SeedUserWithApprovedKycAsync(setupDb, userId);
            var wallet = await setupDb.UserWallets.FirstAsync(w => w.UserId == userId);
            wallet.FreeBalance = 100;
            await setupDb.SaveChangesAsync();
        }

        // Launch 10 concurrent withdrawal tasks of 30 EGP each (Total requested = 300 EGP, available = 100 EGP)
        int numberOfTasks = 10;
        int withdrawAmount = 30;

        var tasks = Enumerable.Range(0, numberOfTasks).Select(async i =>
        {
            using var db = _fixture.GetDbContext();
            var walletService = CreateWalletService(db);
            try
            {
                await walletService.WithdrawAsync(userId, withdrawAmount);
                return true; // Succeeded
            }
            catch (InvalidOperationException)
            {
                return false; // Rejected due to insufficient balance
            }
        });

        var results = await Task.WhenAll(tasks);

        int succeededCount = results.Count(r => r);
        int failedCount = results.Count(r => !r);

        using (var verifyDb = _fixture.GetDbContext())
        {
            var finalWallet = await verifyDb.UserWallets.AsNoTracking().FirstAsync(w => w.UserId == userId);

            // Exactly 3 withdrawals of 30 EGP should succeed (90 EGP total), 7 should fail
            Assert.Equal(3, succeededCount);
            Assert.Equal(7, failedCount);
            Assert.Equal(10, finalWallet.FreeBalance); // 100 - 90 = 10
        }
    }

    [Fact]
    public async Task ConcurrentDepositsAndWithdrawals_CalculatesCorrectFinalBalance()
    {
        var userId = "user-conc-dep-with";

        using (var setupDb = _fixture.GetDbContext())
        {
            await SeedUserWithApprovedKycAsync(setupDb, userId);
        }

        // 5 deposits of 100 EGP (+500) and 5 withdrawals of 50 EGP (-250) run concurrently
        var depositTasks = Enumerable.Range(0, 5).Select(async i =>
        {
            using var db = _fixture.GetDbContext();
            var walletService = CreateWalletService(db);
            await walletService.DepositAsync(userId, 100);
        });

        var withdrawTasks = Enumerable.Range(0, 5).Select(async i =>
        {
            // Give deposits a tiny head start to populate initial balance
            await Task.Delay(10);
            using var db = _fixture.GetDbContext();
            var walletService = CreateWalletService(db);
            try
            {
                await walletService.WithdrawAsync(userId, 50);
            }
            catch (InvalidOperationException)
            {
                // Might fail if withdrawal executed before deposit committed
            }
        });

        await Task.WhenAll(depositTasks.Concat(withdrawTasks));

        using (var verifyDb = _fixture.GetDbContext())
        {
            var finalWallet = await verifyDb.UserWallets.AsNoTracking().FirstAsync(w => w.UserId == userId);
            var transactions = await verifyDb.WalletTransactions.Where(t => t.UserId == userId).ToListAsync();

            int totalDeposited = transactions.Where(t => t.Type == "deposit").Sum(t => t.Amount);
            int totalWithdrawn = transactions.Where(t => t.Type == "withdraw").Sum(t => t.Amount);

            Assert.Equal(500, totalDeposited);
            Assert.Equal(totalDeposited - totalWithdrawn, finalWallet.FreeBalance);
        }
    }

    [Fact]
    public async Task ConcurrentTransfers_PreservesTotalMoneyWithoutDeadlocks()
    {
        var userA = "user-transfer-a";
        var userB = "user-transfer-b";

        using (var setupDb = _fixture.GetDbContext())
        {
            await SeedUserWithApprovedKycAsync(setupDb, userA, "+201000000010");
            await SeedUserWithApprovedKycAsync(setupDb, userB, "+201000000020");

            var walletA = await setupDb.UserWallets.FirstAsync(w => w.UserId == userA);
            var walletB = await setupDb.UserWallets.FirstAsync(w => w.UserId == userB);
            walletA.FreeBalance = 1000;
            walletB.FreeBalance = 1000;
            await setupDb.SaveChangesAsync();
        }

        // 5 tasks transferring 100 EGP from A to B and 5 tasks transferring 100 EGP from B to A concurrently
        var tasksAtoB = Enumerable.Range(0, 5).Select(async i =>
        {
            using var db = _fixture.GetDbContext();
            var walletService = CreateWalletService(db);
            await walletService.TransferAsync(userA, userB, 100);
        });

        var tasksBtoA = Enumerable.Range(0, 5).Select(async i =>
        {
            using var db = _fixture.GetDbContext();
            var walletService = CreateWalletService(db);
            await walletService.TransferAsync(userB, userA, 100);
        });

        // Run simultaneously - must complete without deadlocks
        await Task.WhenAll(tasksAtoB.Concat(tasksBtoA));

        using (var verifyDb = _fixture.GetDbContext())
        {
            var finalA = await verifyDb.UserWallets.AsNoTracking().FirstAsync(w => w.UserId == userA);
            var finalB = await verifyDb.UserWallets.AsNoTracking().FirstAsync(w => w.UserId == userB);

            // Total money across both wallets MUST equal 2000 EGP
            Assert.Equal(2000, finalA.FreeBalance + finalB.FreeBalance);
        }
    }

    [Fact]
    public async Task ConcurrentRefreshTokenRequests_RotatesTokenSafelyWithGracePeriod()
    {
        var userId = "user-conc-refresh";
        var initialToken = "refresh-token-concurrent-test";

        using (var setupDb = _fixture.GetDbContext())
        {
            var user = new User
            {
                Id = userId,
                UserName = "user_refresh_conc",
                Email = "ref@test.com",
                PhoneNumber = "+201099999999",
                Governorate = "Cairo",
                City = "Cairo"
            };
            setupDb.Users.Add(user);

            var refreshToken = new RefreshToken
            {
                Token = initialToken,
                UserId = userId,
                Expires = DateTime.UtcNow.AddDays(90),
                Created = DateTime.UtcNow,
                IsRevoked = false
            };
            setupDb.RefreshTokens.Add(refreshToken);
            await setupDb.SaveChangesAsync();
        }

        // Fake UserService for JWT generation/validation
        var userServiceFake = A.Fake<IUserService>();
        var claims = new List<Claim>
        {
            new Claim("uid", userId),
            new Claim("token_type", TokensTypes.RefreshToken.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        A.CallTo(() => userServiceFake.ValidateRefreshToken(initialToken)).Returns(principal);
        A.CallTo(() => userServiceFake.GenerateJwtToken(A<User>._, TokensTypes.AccessToken)).Returns("new-access-token");
        A.CallTo(() => userServiceFake.GenerateJwtToken(A<User>._, TokensTypes.RefreshToken)).ReturnsLazily(() => $"new-refresh-{Guid.NewGuid()}");

        // 5 concurrent requests with the SAME refresh token
        int numberOfRequests = 5;
        var refreshTasks = Enumerable.Range(0, numberOfRequests).Select(async i =>
        {
            using var db = _fixture.GetDbContext();
            var userManagerFake = A.Fake<UserManager<User>>();
            var otpFake = A.Fake<IOTPService>();
            var cdnFake = A.Fake<ICDNService>();
            var imageService = new UserImageService(db, cdnFake);

            var controller = new AuthController(userManagerFake, null!, userServiceFake, otpFake, db, imageService)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            return await controller.Refresh(new RefreshRequest { RefreshToken = initialToken });
        });

        var responses = await Task.WhenAll(refreshTasks);

        // All 5 responses should be 200 OK (1 rotated + 4 grace window responses)
        foreach (var res in responses)
        {
            Assert.IsType<OkObjectResult>(res);
        }

        using (var verifyDb = _fixture.GetDbContext())
        {
            var originalTokenInDb = await verifyDb.RefreshTokens.FirstAsync(t => t.Token == initialToken);
            Assert.True(originalTokenInDb.IsRevoked);

            // Exactly ONE new active refresh token should be created in DB
            var activeTokens = await verifyDb.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync();
            Assert.Single(activeTokens);
        }
    }

    [Fact]
    public async Task ConcurrentRefreshAndLogout_RevokesTokenAtomically()
    {
        var userId = "user-ref-logout";
        var tokenString = "refresh-logout-token";

        using (var setupDb = _fixture.GetDbContext())
        {
            var user = new User
            {
                Id = userId,
                UserName = "user_ref_logout",
                Email = "reflog@test.com",
                PhoneNumber = "+201088888888",
                Governorate = "Cairo",
                City = "Cairo"
            };
            setupDb.Users.Add(user);

            setupDb.RefreshTokens.Add(new RefreshToken
            {
                Token = tokenString,
                UserId = userId,
                Expires = DateTime.UtcNow.AddDays(90),
                Created = DateTime.UtcNow,
                IsRevoked = false
            });
            await setupDb.SaveChangesAsync();
        }

        var userServiceFake = A.Fake<IUserService>();
        var claims = new List<Claim>
        {
            new Claim("uid", userId),
            new Claim("token_type", TokensTypes.RefreshToken.ToString())
        };
        A.CallTo(() => userServiceFake.ValidateRefreshToken(tokenString))
            .Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        using var db1 = _fixture.GetDbContext();
        using var db2 = _fixture.GetDbContext();

        var controller1 = new AuthController(A.Fake<UserManager<User>>(), null!, userServiceFake, A.Fake<IOTPService>(), db1, new UserImageService(db1, A.Fake<ICDNService>()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var controller2 = new AuthController(A.Fake<UserManager<User>>(), null!, userServiceFake, A.Fake<IOTPService>(), db2, new UserImageService(db2, A.Fake<ICDNService>()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var logoutTask = controller1.Logout(new RefreshRequest { RefreshToken = tokenString });
        var refreshTask = controller2.Refresh(new RefreshRequest { RefreshToken = tokenString });

        await Task.WhenAll(logoutTask, refreshTask);

        using (var verifyDb = _fixture.GetDbContext())
        {
            var tokenInDb = await verifyDb.RefreshTokens.FirstAsync(t => t.Token == tokenString);
            Assert.True(tokenInDb.IsRevoked);
        }
    }
}
