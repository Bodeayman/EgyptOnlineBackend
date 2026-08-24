using EgyptOnline.Controllers;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Identity.Data;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace EgyptOnline.Tests.Unit.Auth;

/// <summary>
/// Unit tests for AuthController Refresh Token rotation, multi-device isolation, and grace period logic.
/// </summary>
[Trait("Category", "Unit")]
public class AuthRefreshTests : UnitTestBase
{
    private readonly IUserService _userServiceFake = A.Fake<IUserService>();
    private readonly IOTPService _otpServiceFake = A.Fake<IOTPService>();
    private readonly ICDNService _cdnServiceFake = A.Fake<ICDNService>();
    private readonly UserImageService _userImageService;

    public AuthRefreshTests()
    {
        _userImageService = new UserImageService(Context, _cdnServiceFake);
    }

    private AuthController BuildController()
    {
        var controller = new AuthController(
            UserManagerFake,
            null!, // UserRegisterationService unused in Refresh
            _userServiceFake,
            _otpServiceFake,
            Context,
            _userImageService
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private async Task<User> SeedUser(string userId = "user-auth-1")
    {
        var user = new User
        {
            Id = userId,
            UserName = $"user_{userId}",
            Email = $"{userId}@test.com",
            PhoneNumber = "+201001234567",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    private void SetupJwtValidation(string tokenString, string userId)
    {
        var claims = new List<Claim>
        {
            new Claim("uid", userId),
            new Claim("token_type", TokensTypes.RefreshToken.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        A.CallTo(() => _userServiceFake.ValidateRefreshToken(tokenString))
            .Returns(principal);

        A.CallTo(() => _userServiceFake.GenerateJwtToken(A<User>._, TokensTypes.AccessToken))
            .Returns(Task.FromResult("new-access-token"));

        A.CallTo(() => _userServiceFake.GenerateJwtToken(A<User>._, TokensTypes.RefreshToken))
            .Returns(Task.FromResult($"new-refresh-token-{Guid.NewGuid()}"));
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesTokenAndRevokesOnlyPresentedToken()
    {
        var user = await SeedUser("u1");
        var tokenString = "valid-refresh-token-1";
        SetupJwtValidation(tokenString, user.Id);

        var dbToken = new RefreshToken
        {
            Token = tokenString,
            UserId = user.Id,
            Expires = DateTime.UtcNow.AddDays(90),
            Created = DateTime.UtcNow,
            IsRevoked = false
        };
        Context.RefreshTokens.Add(dbToken);
        await Context.SaveChangesAsync();

        var controller = BuildController();
        var result = await controller.Refresh(new RefreshRequest { RefreshToken = tokenString });

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify original token is marked revoked
        var updatedDbToken = await Context.RefreshTokens.FirstAsync(t => t.Token == tokenString);
        Assert.True(updatedDbToken.IsRevoked);
        Assert.NotNull(updatedDbToken.Revoked);

        // Verify a new token was created for user
        var activeTokens = await Context.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync();
        Assert.Single(activeTokens);
    }

    [Fact]
    public async Task Refresh_Device1_DoesNotRevokeDevice2Token()
    {
        var user = await SeedUser("u-multi");
        var dev1Token = "device-1-token";
        var dev2Token = "device-2-token";

        SetupJwtValidation(dev1Token, user.Id);

        Context.RefreshTokens.AddRange(
            new RefreshToken
            {
                Token = dev1Token,
                UserId = user.Id,
                Expires = DateTime.UtcNow.AddDays(90),
                Created = DateTime.UtcNow,
                IsRevoked = false
            },
            new RefreshToken
            {
                Token = dev2Token,
                UserId = user.Id,
                Expires = DateTime.UtcNow.AddDays(90),
                Created = DateTime.UtcNow,
                IsRevoked = false
            }
        );
        await Context.SaveChangesAsync();

        var controller = BuildController();
        await controller.Refresh(new RefreshRequest { RefreshToken = dev1Token });

        // Device 2's token MUST still be active and not revoked
        var dev2DbToken = await Context.RefreshTokens.FirstAsync(t => t.Token == dev2Token);
        Assert.False(dev2DbToken.IsRevoked);
        Assert.Null(dev2DbToken.Revoked);
    }

    [Fact]
    public async Task Refresh_TokenRevokedWithinGracePeriod_ReturnsActiveTokenSuccessfully()
    {
        var user = await SeedUser("u-grace");
        var oldToken = "old-revoked-token";
        var activeToken = "active-current-token";

        SetupJwtValidation(oldToken, user.Id);

        // Simulate old token revoked 10 seconds ago (concurrent race condition request)
        Context.RefreshTokens.AddRange(
            new RefreshToken
            {
                Token = oldToken,
                UserId = user.Id,
                Expires = DateTime.UtcNow.AddDays(90),
                Created = DateTime.UtcNow.AddMinutes(-5),
                IsRevoked = true,
                Revoked = DateTime.UtcNow.AddSeconds(-10) // 10 seconds ago (within 60s grace window)
            },
            new RefreshToken
            {
                Token = activeToken,
                UserId = user.Id,
                Expires = DateTime.UtcNow.AddDays(90),
                Created = DateTime.UtcNow.AddSeconds(-10),
                IsRevoked = false
            }
        );
        await Context.SaveChangesAsync();

        var controller = BuildController();
        var result = await controller.Refresh(new RefreshRequest { RefreshToken = oldToken });

        // Grace period accepts request and returns Ok
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Refresh_TokenRevokedOutsideGracePeriod_ReturnsUnauthorized()
    {
        var user = await SeedUser("u-expired-grace");
        var oldToken = "ancient-revoked-token";

        SetupJwtValidation(oldToken, user.Id);

        // Token revoked 5 minutes ago (outside 60s grace window)
        Context.RefreshTokens.Add(new RefreshToken
        {
            Token = oldToken,
            UserId = user.Id,
            Expires = DateTime.UtcNow.AddDays(90),
            Created = DateTime.UtcNow.AddDays(-1),
            IsRevoked = true,
            Revoked = DateTime.UtcNow.AddMinutes(-5)
        });
        await Context.SaveChangesAsync();

        var controller = BuildController();
        var result = await controller.Refresh(new RefreshRequest { RefreshToken = oldToken });

        // Returns Unauthorized 401
        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
