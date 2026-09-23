using System.Security.Claims;
using EgyptOnline.Controllers;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Models;
using EgyptOnline.Services;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgyptOnline.Tests.Unit.Auth;

/// <summary>
/// Verifies that a failed profile-image upload is reported as an error,
/// not silently reported as success (HTTP 200).
/// </summary>
[Trait("Category", "Unit")]
public class AuthUploadProfileImageTests : UnitTestBase
{
    private readonly IUserService _userServiceFake = A.Fake<IUserService>();
    private readonly IOTPService _otpServiceFake = A.Fake<IOTPService>();
    private readonly ICDNService _cdnServiceFake = A.Fake<ICDNService>();
    private readonly UserImageService _userImageService;

    public AuthUploadProfileImageTests()
    {
        _userImageService = new UserImageService(Context, _cdnServiceFake);
    }

    private AuthController BuildController(string userId)
    {
        var controller = new AuthController(
            UserManagerFake,
            null!, // UserRegisterationService unused in upload-profile-image
            _userServiceFake,
            _otpServiceFake,
            Context,
            _userImageService
        );

        var identity = new ClaimsIdentity(new[] { new Claim("uid", userId) }, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    private static IFormFile BuildFormFile(string content = "fake-image-bytes")
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", "avatar.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private async Task<User> SeedUser(string userId)
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

    [Fact]
    public async Task UploadProfileImage_WhenCdnFails_ReturnsBadRequest_NotOk()
    {
        var userId = "upload-fail-user";
        await SeedUser(userId);

        // Simulate R2 PutObject failing; UploadUserImageAsync swallows it and returns null.
        A.CallTo(() => _cdnServiceFake.UploadImageAsync(A<byte[]>._, A<string>._, "profiles"))
            .Throws(new Exception("R2 PutObject failed: STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER not implemented"));

        var controller = BuildController(userId);

        var result = await controller.UploadProfileImage(BuildFormFile());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        Assert.Equal(400, badRequest.StatusCode);

        // The user must not have an image URL persisted.
        var user = await Context.Users.FirstAsync(u => u.Id == userId);
        Assert.Null(user.ImageUrl);
    }

    [Fact]
    public async Task UploadProfileImage_WhenCdnSucceeds_ReturnsOkWithUrl_AndPersistsImageUrl()
    {
        var userId = "upload-ok-user";
        await SeedUser(userId);

        A.CallTo(() => _cdnServiceFake.UploadImageAsync(A<byte[]>._, A<string>._, "profiles"))
            .Returns("https://cdn.egyptonlinema3ak.com/profiles/avatar.jpg");

        var controller = BuildController(userId);

        var result = await controller.UploadProfileImage(BuildFormFile());

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var user = await Context.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal("https://cdn.egyptonlinema3ak.com/profiles/avatar.jpg", user.ImageUrl);
    }

    [Fact]
    public async Task UploadProfileImage_WhenNewUploadFails_KeepsOldImage_AndDoesNotDeleteIt()
    {
        var userId = "upload-keep-old-user";
        var user = await SeedUser(userId);
        user.ImageUrl = "https://cdn.egyptonlinema3ak.com/profiles/old-avatar.jpg";
        await Context.SaveChangesAsync();

        // New upload fails; failure is swallowed by the service (returns null).
        A.CallTo(() => _cdnServiceFake.UploadImageAsync(A<byte[]>._, A<string>._, "profiles"))
            .Throws(new Exception("R2 PutObject failed"));

        var controller = BuildController(userId);

        var result = await controller.UploadProfileImage(BuildFormFile());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);

        // The old image must survive an upload failure (delete happens only after a successful upload).
        A.CallTo(() => _cdnServiceFake.DeleteImageAsync(A<string>._)).MustNotHaveHappened();
        var persisted = await Context.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal("https://cdn.egyptonlinema3ak.com/profiles/old-avatar.jpg", persisted.ImageUrl);
    }

    [Fact]
    public async Task UploadProfileImage_WhenNewUploadSucceeds_DeletesOldImage()
    {
        var userId = "upload-delete-old-user";
        var user = await SeedUser(userId);
        user.ImageUrl = "https://egyptonlinema3ak.com/files/egypt-online-public/profiles/old-avatar.jpg";
        await Context.SaveChangesAsync();

        A.CallTo(() => _cdnServiceFake.UploadImageAsync(A<byte[]>._, A<string>._, "profiles"))
            .Returns("https://cdn.egyptonlinema3ak.com/profiles/new-avatar.jpg");

        var controller = BuildController(userId);

        var result = await controller.UploadProfileImage(BuildFormFile());

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        // The legacy old URL (containing /files/egypt-online-public/...) is passed for deletion.
        A.CallTo(() => _cdnServiceFake.DeleteImageAsync("https://egyptonlinema3ak.com/files/egypt-online-public/profiles/old-avatar.jpg"))
            .MustHaveHappenedOnceExactly();
        var persisted = await Context.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal("https://cdn.egyptonlinema3ak.com/profiles/new-avatar.jpg", persisted.ImageUrl);
    }
}