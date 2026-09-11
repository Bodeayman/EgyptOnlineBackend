using EgyptOnline.Application.Services.Rating;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Presentation.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EgyptOnline.Tests.Unit.Rating;

/// <summary>
/// Controller-level tests for RatingController. Verifies that ownership is
/// taken from the authentication context (never client-supplied data),
/// unauthenticated requests are rejected, and the self-comment restriction
/// holds through direct API calls.
/// </summary>
public class RatingControllerTests : UnitTestBase
{
    private RatingController BuildController(string? uid = null)
    {
        var controller = new RatingController(new RatingService(Context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (!string.IsNullOrEmpty(uid))
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("uid", uid)
            }, "TestAuth");
            controller.HttpContext.User = new ClaimsPrincipal(identity);
        }

        return controller;
    }

    private async Task<(User owner, User other, User target)> SeedUsers()
    {
        var owner = new User
        {
            Id = "del-owner",
            FirstName = "Owner",
            LastName = "One",
            UserName = "ownerone",
            Governorate = "Cairo",
            City = "Cairo"
        };
        var other = new User
        {
            Id = "del-other",
            FirstName = "Other",
            LastName = "Two",
            UserName = "othertwo",
            Governorate = "Cairo",
            City = "Cairo"
        };
        var target = new User
        {
            Id = "del-target",
            FirstName = "Target",
            LastName = "Three",
            UserName = "targetthree",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.AddRange(owner, other, target);
        await Context.SaveChangesAsync();
        return (owner, other, target);
    }

    // ── Delete own rating ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRating_OwnRating_ReturnsOk()
    {
        // Arrange
        var (owner, _, target) = await SeedUsers();
        var service = new RatingService(Context);
        var created = await service.SubmitRatingAsync(owner.Id, new CreateRatingDto
        {
            TargetUserId = target.Id,
            Rating = 5,
            Description = "Great!"
        });
        var controller = BuildController(owner.Id);

        // Act
        var result = await controller.DeleteRating(created.Id);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.False(Context.Ratings.Any(r => r.Id == created.Id));
    }

    [Fact]
    public async Task DeleteRating_AnotherUsersRating_ReturnsForbid()
    {
        // Arrange
        var (owner, other, target) = await SeedUsers();
        var service = new RatingService(Context);
        var created = await service.SubmitRatingAsync(owner.Id, new CreateRatingDto
        {
            TargetUserId = target.Id,
            Rating = 4,
            Description = "Nice"
        });
        var controller = BuildController(other.Id);

        // Act
        var result = await controller.DeleteRating(created.Id);

        // Assert - 403 Forbidden
        Assert.IsAssignableFrom<ForbidResult>(result);
        Assert.True(Context.Ratings.Any(r => r.Id == created.Id));
    }

    [Fact]
    public async Task DeleteRating_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var controller = BuildController("some-user");

        // Act
        var result = await controller.DeleteRating(999999);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task DeleteRating_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var controller = BuildController(); // no uid claim

        // Act
        var result = await controller.DeleteRating(1);

        // Assert
        Assert.IsAssignableFrom<UnauthorizedObjectResult>(result);
    }

    // ── Self-comment prevention via direct API ────────────────────────────

    [Fact]
    public async Task SubmitRating_SelfTarget_ReturnsBadRequestWithCannotRateSelf()
    {
        // Arrange
        var user = new User
        {
            Id = "self-commenter",
            FirstName = "Self",
            LastName = "Commenter",
            UserName = "selfcommenter",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var controller = BuildController(user.Id);
        var dto = new CreateRatingDto
        {
            TargetUserId = user.Id,
            Rating = 5,
            Description = "Trying to comment on myself via direct API"
        };

        // Act
        var result = await controller.SubmitRating(dto);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task SubmitRating_OtherUser_ReturnsOk()
    {
        // Arrange
        var userA = new User
        {
            Id = "commenter-a",
            FirstName = "Commenter",
            LastName = "A",
            UserName = "commentera",
            Governorate = "Cairo",
            City = "Cairo"
        };
        var userB = new User
        {
            Id = "commenter-b",
            FirstName = "Target",
            LastName = "B",
            UserName = "targetb",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.AddRange(userA, userB);
        await Context.SaveChangesAsync();

        var controller = BuildController(userA.Id);
        var dto = new CreateRatingDto
        {
            TargetUserId = userB.Id,
            Rating = 5,
            Description = "Legit comment on another user"
        };

        // Act
        var result = await controller.SubmitRating(dto);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}