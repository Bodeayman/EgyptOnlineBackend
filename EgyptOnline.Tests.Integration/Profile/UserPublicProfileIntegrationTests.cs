using EgyptOnline.Application.Services.Rating;
using EgyptOnline.Application.Services.Search;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using RatingEntity = EgyptOnline.Models.Rating;
using PostEntity = EgyptOnline.Models.Post;
using PostPhotoEntity = EgyptOnline.Models.PostPhoto;

namespace EgyptOnline.Tests.Integration.Profile;

/// <summary>
/// Integration tests for the public user profile against a real PostgreSQL
/// database. Verifies received-rating aggregation, public post preview URLs,
/// and that the response is the safe public DTO across separate contexts.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class UserPublicProfileIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public UserPublicProfileIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => await _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<User> SeedUser(string id, string userName)
    {
        using var db = _fixture.GetDbContext();
        var user = new User
        {
            Id = id,
            UserName = userName,
            FirstName = "محمد",
            LastName = "علاء",
            PhoneNumber = "+201553430956",
            Governorate = "القاهرة",
            City = "القاهرة",
            District = "عين شمس",
            ImageUrl = "https://example.com/profile.png",
            NormalizedUserName = userName.ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task SeedWorker(User user, decimal pay)
    {
        using var db = _fixture.GetDbContext();
        db.Workers.Add(new Worker
        {
            Id = 1,
            UserId = user.Id,
            Skill = "كهرباء",
            ServicePricePerDay = pay,
            WorkerType = 0,
            DerivedSpec = "محاره",
            MarketPlace = "عين شمس"
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_PersistedData_ReturnsCorrectAggregates()
    {
        // Arrange
        var user = await SeedUser("profile-tgt-1", "profile_tgt_1");
        var rater = await SeedUser("profile-rater-1", "profile_rater_1");
        await SeedWorker(user, 700);

        using (var db = _fixture.GetDbContext())
        {
            // user RECEIVES [5, 3], user GIVES a 1 (must not count for own profile)
            db.Ratings.AddRange(
                new RatingEntity { UserId = rater.Id, TargetUserId = user.Id, RatingValue = 5 },
                new RatingEntity { UserId = rater.Id, TargetUserId = user.Id, RatingValue = 3 },
                new RatingEntity { UserId = user.Id, TargetUserId = rater.Id, RatingValue = 1 }
            );

            var post = new PostEntity { UserId = user.Id, Description = "Portfolio", CreatedAt = DateTime.UtcNow };
            post.Photos.Add(new PostPhotoEntity { PhotoUrl = "https://cdn.example.com/port.jpg", Order = 1 });
            db.Posts.Add(post);

            await db.SaveChangesAsync();
        }

        // Act — fresh context to simulate a new request
        var result = await new SearchService(_fixture.GetDbContext(), null).GetUserPublicProfileAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.userId);
        Assert.Equal(700, result.pay);
        Assert.Equal(4.0, result.averageRating);   // (5 + 3) / 2
        Assert.Equal(2, result.totalRatingCount);  // only received ratings
        Assert.Equal(new[] { "https://cdn.example.com/port.jpg" }, result.postImagePreviews);
        Assert.IsType<SearchV2ResultDto>(result);
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_AfterRatingDeleted_ExcludesIt()
    {
        // Arrange
        var user = await SeedUser("profile-del-1", "profile_del_1");
        var rater = await SeedUser("profile-del-rater-1", "profile_del_rater_1");
        await SeedWorker(user, 500);

        int ratingId;
        using (var db = _fixture.GetDbContext())
        {
            var rating = new RatingEntity { UserId = rater.Id, TargetUserId = user.Id, RatingValue = 5 };
            db.Ratings.Add(rating);
            await db.SaveChangesAsync();
            ratingId = rating.Id;
        }

        // Act
        await new RatingService(_fixture.GetDbContext()).DeleteRatingAsync(rater.Id, ratingId);
        var result = await new SearchService(_fixture.GetDbContext(), null).GetUserPublicProfileAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result!.averageRating);
        Assert.Equal(0, result.totalRatingCount);
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_NonExistentUser_ReturnsNull()
    {
        var result = await new SearchService(_fixture.GetDbContext(), null)
            .GetUserPublicProfileAsync("00000000-0000-0000-0000-000000000000");
        Assert.Null(result);
    }
}