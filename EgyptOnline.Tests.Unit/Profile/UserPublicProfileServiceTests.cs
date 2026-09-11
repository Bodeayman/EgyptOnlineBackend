using EgyptOnline.Application.Services.Rating;
using EgyptOnline.Application.Services.Search;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Tests.Unit;
using RatingEntity = EgyptOnline.Models.Rating;
using PostEntity = EgyptOnline.Models.Post;
using PostPhotoEntity = EgyptOnline.Models.PostPhoto;

namespace EgyptOnline.Tests.Unit.Profile;

/// <summary>
/// Tests for the public user profile method (GetUserPublicProfileAsync).
/// Confirms only public fields are exposed, ratings are received-rating
/// aggregates, deleted ratings are excluded, post previews use public URLs,
/// and unknown/missing users produce null (404 upstream).
/// </summary>
public class UserPublicProfileServiceTests : UnitTestBase
{
    private readonly SearchService _searchService;
    private readonly RatingService _ratingService;

    public UserPublicProfileServiceTests()
    {
        _searchService = new SearchService(Context, null);
        _ratingService = new RatingService(Context);
    }

    private async Task<(User user, Worker worker)> SeedWorker(string idSuffix, decimal pay)
    {
        var user = new User
        {
            Id = $"{idSuffix}-user-id",
            FirstName = "محمد",
            LastName = "علاء",
            UserName = $"{idSuffix}username",
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
            ServicePricePerDay = pay,
            WorkerType = 0,
            DerivedSpec = "محاره",
            MarketPlace = "عين شمس",
            Bio = "نبذة"
        };
        Context.Users.Add(user);
        Context.Workers.Add(worker);
        await Context.SaveChangesAsync();
        return (user, worker);
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_ReturnsExpectedProfileFields_ForWorker()
    {
        // Arrange
        var (user, _) = await SeedWorker("worker", 700);

        // Act
        var result = await _searchService.GetUserPublicProfileAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.userId);
        Assert.Equal("محمد علاء", result.name);
        Assert.Equal("كهرباء", result.skill);
        Assert.Equal("القاهرة", result.governorate);
        Assert.Equal("القاهرة", result.city);
        Assert.Equal("عين شمس", result.district);
        Assert.Equal(700, result.pay);
        Assert.Equal("https://example.com/profile.png", result.imageUrl);
        Assert.False(result.isCompany);
        Assert.Equal("+201553430956", result.mobileNumber);
        Assert.Equal("Worker", result.typeOfService);
        Assert.Equal("نبذة", result.aboutMe);
        Assert.Equal("عين شمس", result.marketPlace);
        Assert.Equal("محاره", result.derivedSpec);
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_ReturnsNull_WhenUserNotFound()
    {
        var result = await _searchService.GetUserPublicProfileAsync("no-such-user");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_ReturnsNull_ForMalformedId()
    {
        var result = await _searchService.GetUserPublicProfileAsync("not-a-guid");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_DoesNotExposeSensitiveFields()
    {
        // Arrange
        var (user, _) = await SeedWorker("sensitive", 100);

        // Act
        var result = await _searchService.GetUserPublicProfileAsync(user.Id);

        // Assert — the DTO must be the safe public projection, not a User entity
        Assert.IsType<SearchV2ResultDto>(result);

        var sensitive = new[]
        {
            nameof(User.PasswordHash), nameof(User.SecurityStamp),
            nameof(User.ConcurrencyStamp), nameof(User.EmailConfirmed),
            nameof(User.LockoutEnd), "RefreshToken",
            "Wallet", "SubscriptionPoints", "Points"
        };
        var properties = typeof(SearchV2ResultDto).GetProperties().Select(p => p.Name);
        foreach (var name in sensitive)
        {
            Assert.DoesNotContain(name, properties);
        }
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_AverageRatingAndCount_UseReceivedRatings()
    {
        // Arrange — user receives [5, 3] and also GIVES a 1 (must NOT count for their own profile)
        var (user, _) = await SeedWorker("ratings", 100);
        var rater = new User
        {
            Id = "rater-id",
            FirstName = "Rater",
            LastName = "One",
            UserName = "raterone",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(rater);

        Context.Ratings.AddRange(
            new RatingEntity { UserId = rater.Id, TargetUserId = user.Id, RatingValue = 5 },
            new RatingEntity { UserId = rater.Id, TargetUserId = user.Id, RatingValue = 3 },
            new RatingEntity { UserId = user.Id, TargetUserId = rater.Id, RatingValue = 1 }
        );
        await Context.SaveChangesAsync();

        // Act
        var result = await _searchService.GetUserPublicProfileAsync(user.Id);

        // Assert — only received ratings counted: (5+3)/2 = 4.0
        Assert.NotNull(result);
        Assert.Equal(4.0, result!.averageRating);
        Assert.Equal(2, result.totalRatingCount);
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_ReturnsZero_WhenNoRatings()
    {
        var (user, _) = await SeedWorker("noratings", 100);

        var result = await _searchService.GetUserPublicProfileAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(0, result!.averageRating);
        Assert.Equal(0, result.totalRatingCount);
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_ExcludesDeletedRatings()
    {
        // Arrange
        var (user, _) = await SeedWorker("deleted", 100);
        var rater = new User
        {
            Id = "rater-deleted-id",
            FirstName = "Rater",
            LastName = "Two",
            UserName = "ratertwo",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(rater);
        var toDelete = new RatingEntity { UserId = rater.Id, TargetUserId = user.Id, RatingValue = 5 };
        var toKeep = new RatingEntity { UserId = rater.Id, TargetUserId = user.Id, RatingValue = 3 };
        Context.Ratings.AddRange(toDelete, toKeep);
        await Context.SaveChangesAsync();

        // Act — delete one rating, then re-read the profile
        await _ratingService.DeleteRatingAsync(rater.Id, toDelete.Id);
        var result = await _searchService.GetUserPublicProfileAsync(user.Id);

        // Assert — only the surviving 3-star rating counts
        Assert.NotNull(result);
        Assert.Equal(3.0, result!.averageRating);
        Assert.Equal(1, result.totalRatingCount);
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_ReturnsPostImagePreviews_UsingPublicUrls()
    {
        // Arrange
        var (user, _) = await SeedWorker("posts", 100);
        var postA = new PostEntity { UserId = user.Id, Description = "Post A", CreatedAt = DateTime.UtcNow };
        postA.Photos.Add(new PostPhotoEntity { PhotoUrl = "https://cdn.example.com/a1.jpg", Order = 1 });
        postA.Photos.Add(new PostPhotoEntity { PhotoUrl = "https://cdn.example.com/a2.jpg", Order = 2 });
        var postB = new PostEntity { UserId = user.Id, Description = "Post B", CreatedAt = DateTime.UtcNow };
        postB.Photos.Add(new PostPhotoEntity { PhotoUrl = "https://cdn.example.com/b1.jpg", Order = 1 });
        Context.Posts.AddRange(postA, postB);
        await Context.SaveChangesAsync();

        // Act
        var result = await _searchService.GetUserPublicProfileAsync(user.Id);

        // Assert — first photo per post, public CDN URLs only
        Assert.NotNull(result);
        Assert.Equal(2, result!.postImagePreviews.Count);
        Assert.All(result.postImagePreviews, u => u.StartsWith("https://cdn.example.com/"));
    }

    [Fact]
    public async Task GetUserPublicProfileAsync_ReturnsEmptyPostPreviews_WhenUserHasNoPosts()
    {
        var (user, _) = await SeedWorker("noposts", 100);

        var result = await _searchService.GetUserPublicProfileAsync(user.Id);

        Assert.NotNull(result);
        Assert.Empty(result!.postImagePreviews);
    }
}