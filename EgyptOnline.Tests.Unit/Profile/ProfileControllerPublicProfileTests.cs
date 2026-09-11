using EgyptOnline.Application.Services.Rating;
using EgyptOnline.Application.Services.Search;
using EgyptOnline.Controllers;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Services;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EgyptOnline.Tests.Unit.Profile;

/// <summary>
/// Controller-level tests for GET /api/v1/Profile/{userId}.
/// Verifies status-code behaviour (200 / 404), that the response is the safe
/// public DTO, and that authorization is not weakened (User role required).
/// </summary>
public class ProfileControllerPublicProfileTests : UnitTestBase
{
    private ProfileController BuildController()
    {
        var occupationService = new OccupationService(Cache);
        var searchService = new SearchService(Context, occupationService);
        var controller = new ProfileController(
            UserManagerFake,
            A.Fake<IUserService>(),
            Context,
            occupationService,
            searchService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var identity = new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim("uid", "viewer-id")
        }, "TestAuth");
        controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);

        return controller;
    }

    private async Task<(User user, Worker worker)> SeedWorker()
    {
        var user = new User
        {
            Id = "91bc3ab5-6950-4181-8402-358506289603",
            FirstName = "محمد",
            LastName = "علاء",
            UserName = "mohamed",
            PhoneNumber = "+201553430956",
            Governorate = "القاهرة",
            City = "القاهرة",
            District = "عين شمس",
            ImageUrl = "https://example.com/profile.png"
        };
        var worker = new Worker
        {
            Id = 1,
            UserId = user.Id,
            Skill = "كهرباء",
            ServicePricePerDay = 700,
            WorkerType = 0,
            DerivedSpec = "محاره",
            MarketPlace = "عين شمس"
        };
        Context.Users.Add(user);
        Context.Workers.Add(worker);
        await Context.SaveChangesAsync();
        return (user, worker);
    }

    [Fact]
    public async Task GetUserProfile_ValidUser_ReturnsOkWithPublicDto()
    {
        // Arrange
        var (user, _) = await SeedWorker();
        var controller = BuildController();

        // Act
        var result = await controller.GetUserProfile(Guid.Parse(user.Id));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.IsType<SearchV2ResultDto>(ok.Value);
        var dto = (SearchV2ResultDto)ok.Value!;
        Assert.Equal(user.Id, dto.userId);
        Assert.Equal(700, dto.pay);
        Assert.Equal("كهرباء", dto.skill);
    }

    [Fact]
    public async Task GetUserProfile_NonExistentUser_ReturnsNotFound()
    {
        var controller = BuildController();

        var result = await controller.GetUserProfile(Guid.NewGuid());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task GetUserProfile_ReturnsUserNotFoundErrorCode()
    {
        var controller = BuildController();

        var result = await controller.GetUserProfile(Guid.NewGuid()) as NotFoundObjectResult;

        Assert.NotNull(result);
        var valueType = result!.Value!.GetType();
        var errorCode = valueType.GetProperty("errorCode")?.GetValue(result.Value)?.ToString();
        Assert.Equal("USER_NOT_FOUND", errorCode);
    }

    [Fact]
    public void GetUserProfileRoute_UsesGuidConstraint_AndRequiresAuthorization()
    {
        // Verify route constraint so literal action names never collide and
        // invalid GUIDs are rejected at the routing layer.
        var action = typeof(ProfileController).GetMethod(nameof(ProfileController.GetUserProfile));
        Assert.NotNull(action);

        var route = action!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>()
            .SingleOrDefault();
        Assert.NotNull(route);
        Assert.Equal("{userId:guid}", route!.Template);

        // Authorization is inherited from the class-level [Authorize(Roles = User)]
        var classAuth = typeof(ProfileController).GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();
        Assert.NotNull(classAuth);
        Assert.Contains("User", classAuth!.Roles);
    }
}