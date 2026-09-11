using EgyptOnline.Application.Services.Rating;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgyptOnline.Tests.Integration.Rating;

/// <summary>
/// Integration tests for RatingService against a real PostgreSQL database.
/// Verifies deletion ownership enforcement and aggregate correctness
/// across separate DbContext instances.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class RatingServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public RatingServiceIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => await _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<User> SeedUser(string userId, string userName)
    {
        using var db = _fixture.GetDbContext();
        var user = new User
        {
            Id = userId,
            UserName = userName,
            PhoneNumber = "+20" + userId.Replace("user", "").TrimStart('0'),
            Governorate = "Cairo",
            City = "Cairo",
            NormalizedUserName = userName.ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task DeleteRatingAsync_DeletesOwnRating_PersistedInPostgres()
    {
        // Arrange
        var owner = await SeedUser("rating-owner-1", "rating_owner_1");
        var target = await SeedUser("rating-target-1", "rating_target_1");
        var service = new RatingService(_fixture.GetDbContext());

        var created = await service.SubmitRatingAsync(owner.Id, new CreateRatingDto
        {
            TargetUserId = target.Id,
            Rating = 5,
            Description = "Great service"
        });

        // Act - use a fresh context to simulate a separate request
        var result = await new RatingService(_fixture.GetDbContext()).DeleteRatingAsync(owner.Id, created.Id);

        // Assert - verify persistence with yet another fresh context
        Assert.Equal(created.Id, result.Id);
        using (var verify = _fixture.GetDbContext())
        {
            Assert.Null(await verify.Ratings.FirstOrDefaultAsync(r => r.Id == created.Id));
        }
    }

    [Fact]
    public async Task DeleteRatingAsync_RejectsOtherUsersRating_InPostgres()
    {
        // Arrange
        var owner = await SeedUser("rating-owner-2", "rating_owner_2");
        var other = await SeedUser("rating-other-2", "rating_other_2");
        var target = await SeedUser("rating-target-2", "rating_target_2");
        var service = new RatingService(_fixture.GetDbContext());

        var created = await service.SubmitRatingAsync(owner.Id, new CreateRatingDto
        {
            TargetUserId = target.Id,
            Rating = 4,
            Description = "Nice"
        });

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new RatingService(_fixture.GetDbContext()).DeleteRatingAsync(other.Id, created.Id));

        // The rating must still exist
        using (var verify = _fixture.GetDbContext())
        {
            Assert.NotNull(await verify.Ratings.FirstOrDefaultAsync(r => r.Id == created.Id));
        }
    }

    [Fact]
    public async Task DeleteRatingAsync_NotFound_WhenRatingMissing()
    {
        var service = new RatingService(_fixture.GetDbContext());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteRatingAsync("any-user", 999999));
    }

    [Fact]
    public async Task SubmitRatingAsync_RejectsSelfRating_InPostgres()
    {
        // Arrange
        var user = await SeedUser("rating-self-3", "rating_self_3");
        var service = new RatingService(_fixture.GetDbContext());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitRatingAsync(user.Id, new CreateRatingDto
            {
                TargetUserId = user.Id,
                Rating = 5,
                Description = "Self comment"
            }));
    }

    [Fact]
    public async Task DeleteRatingAsync_RecomputesAggregates_InPostgres()
    {
        // Arrange
        var owner = await SeedUser("rating-agg-4", "rating_agg_4");
        var target = await SeedUser("rating-agg-target-4", "rating_agg_target_4");
        var otherOwner = await SeedUser("rating-agg-other-4", "rating_agg_other_4");
        var service = new RatingService(_fixture.GetDbContext());

        var toDelete = await service.SubmitRatingAsync(owner.Id, new CreateRatingDto
        {
            TargetUserId = target.Id,
            Rating = 5,
            Description = "Will be deleted"
        });

        // Remaining rating from another user (3 stars)
        await service.SubmitRatingAsync(otherOwner.Id, new CreateRatingDto
        {
            TargetUserId = target.Id,
            Rating = 3,
            Description = "Stays"
        });

        // Sanity check: two ratings before deletion
        var before = await service.GetRatingsAsync();
        Assert.Equal(2, before.Summary.TotalRatings);

        // Act
        await new RatingService(_fixture.GetDbContext()).DeleteRatingAsync(owner.Id, toDelete.Id);

        // Assert aggregates correct after deletion using fresh context
        var after = await new RatingService(_fixture.GetDbContext()).GetRatingsAsync();
        Assert.Equal(1, after.Summary.TotalRatings);
        Assert.Equal(3.0, after.Summary.AverageRating);
        Assert.Equal(1, after.Summary.Distribution.ThreeStars);
        Assert.DoesNotContain(after.Ratings, r => r.Id == toDelete.Id);
    }
}