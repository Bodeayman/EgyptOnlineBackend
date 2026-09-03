using EgyptOnline.Application.Services.Rating;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Tests.Unit;
using FakeItEasy;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using RatingEntity = EgyptOnline.Models.Rating;

namespace EgyptOnline.Tests.Unit.Rating;

public class RatingServiceTests : UnitTestBase
{
    private readonly RatingService _ratingService;

    public RatingServiceTests()
    {
        _ratingService = new RatingService(Context);
    }

    [Fact]
    public async Task SubmitRatingAsync_ShouldReturnSuccess_WhenValidInput()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var dto = new CreateRatingDto
        {
            Rating = 5,
            Description = "Great service!"
        };

        // Act
        var result = await _ratingService.SubmitRatingAsync(userId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Rating);
        Assert.Equal("Great service!", result.Description);
        Assert.Equal("John", result.User.FirstName);
        Assert.Equal("Doe", result.User.LastName);
    }

    [Fact]
    public async Task SubmitRatingAsync_ShouldAccept_EmptyDescription()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var dto = new CreateRatingDto
        {
            Rating = 4,
            Description = null
        };

        // Act
        var result = await _ratingService.SubmitRatingAsync(userId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Rating);
        Assert.Null(result.Description);
    }

    [Fact]
    public async Task GetRatingsAsync_ShouldReturnEmptySummary_WhenNoRatingsExist()
    {
        // Act
        var result = await _ratingService.GetRatingsAsync(orderBy: "latest", pageNumber: 1, pageSize: 20);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Summary.TotalRatings);
        Assert.Equal(0, result.Summary.AverageRating);
        Assert.Equal(0, result.Summary.Distribution.FiveStars);
        Assert.Equal(0, result.Summary.Distribution.FourStars);
        Assert.Equal(0, result.Summary.Distribution.ThreeStars);
        Assert.Equal(0, result.Summary.Distribution.TwoStars);
        Assert.Equal(0, result.Summary.Distribution.OneStar);
        Assert.Empty(result.Ratings);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetRatingsAsync_ShouldReturnCorrectAverageRating()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        Context.Ratings.AddRange(
            new RatingEntity { UserId = userId, RatingValue = 5, CreatedAt = DateTime.UtcNow },
            new RatingEntity { UserId = userId, RatingValue = 3, CreatedAt = DateTime.UtcNow },
            new RatingEntity { UserId = userId, RatingValue = 4, CreatedAt = DateTime.UtcNow }
        );
        await Context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetRatingsAsync(orderBy: "latest", pageNumber: 1, pageSize: 20);

        // Assert
        Assert.Equal(4.0, result.Summary.AverageRating);
        Assert.Equal(3, result.Summary.TotalRatings);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task GetRatingsAsync_ShouldReturnCorrectStarDistribution()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        Context.Ratings.AddRange(
            new RatingEntity { UserId = userId, RatingValue = 5, CreatedAt = DateTime.UtcNow },
            new RatingEntity { UserId = userId, RatingValue = 5, CreatedAt = DateTime.UtcNow },
            new RatingEntity { UserId = userId, RatingValue = 4, CreatedAt = DateTime.UtcNow },
            new RatingEntity { UserId = userId, RatingValue = 3, CreatedAt = DateTime.UtcNow },
            new RatingEntity { UserId = userId, RatingValue = 2, CreatedAt = DateTime.UtcNow },
            new RatingEntity { UserId = userId, RatingValue = 1, CreatedAt = DateTime.UtcNow }
        );
        await Context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetRatingsAsync(orderBy: "latest", pageNumber: 1, pageSize: 20);

        // Assert
        Assert.Equal(2, result.Summary.Distribution.FiveStars);
        Assert.Equal(1, result.Summary.Distribution.FourStars);
        Assert.Equal(1, result.Summary.Distribution.ThreeStars);
        Assert.Equal(1, result.Summary.Distribution.TwoStars);
        Assert.Equal(1, result.Summary.Distribution.OneStar);
    }

    [Fact]
    public async Task GetRatingsAsync_OrderByLatest_ShouldOrderByCreatedAtDescending()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        var baseTime = DateTime.UtcNow;
        Context.Ratings.AddRange(
            new RatingEntity { UserId = userId, RatingValue = 3, CreatedAt = baseTime.AddHours(-2) },
            new RatingEntity { UserId = userId, RatingValue = 5, CreatedAt = baseTime.AddHours(-1) },
            new RatingEntity { UserId = userId, RatingValue = 4, CreatedAt = baseTime }
        );
        await Context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetRatingsAsync(orderBy: "latest");

        // Assert
        Assert.Equal(3, result.Ratings.Count);
        Assert.Equal(4, result.Ratings[0].Rating); // Most recent
        Assert.Equal(5, result.Ratings[1].Rating);
        Assert.Equal(3, result.Ratings[2].Rating); // Oldest
    }

    [Fact]
    public async Task GetRatingsAsync_OrderByBest_ShouldOrderByRatingValueDescending()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        var baseTime = DateTime.UtcNow;
        Context.Ratings.AddRange(
            new RatingEntity { UserId = userId, RatingValue = 3, CreatedAt = baseTime.AddHours(-2) },
            new RatingEntity { UserId = userId, RatingValue = 5, CreatedAt = baseTime.AddHours(-1) },
            new RatingEntity { UserId = userId, RatingValue = 4, CreatedAt = baseTime }
        );
        await Context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetRatingsAsync(orderBy: "best");

        // Assert
        Assert.Equal(3, result.Ratings.Count);
        Assert.Equal(5, result.Ratings[0].Rating); // Highest rated
        Assert.Equal(4, result.Ratings[1].Rating);
        Assert.Equal(3, result.Ratings[2].Rating); // Lowest rated
    }

    [Fact]
    public async Task GetRatingsAsync_InvalidOrderBy_ShouldDefaultToLatest()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        var baseTime = DateTime.UtcNow;
        Context.Ratings.AddRange(
            new RatingEntity { UserId = userId, RatingValue = 3, CreatedAt = baseTime.AddHours(-2) },
            new RatingEntity { UserId = userId, RatingValue = 5, CreatedAt = baseTime.AddHours(-1) },
            new RatingEntity { UserId = userId, RatingValue = 4, CreatedAt = baseTime }
        );
        await Context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetRatingsAsync(orderBy: "invalid");

        // Assert
        Assert.Equal(3, result.Ratings.Count);
        Assert.Equal(4, result.Ratings[0].Rating); // Should default to latest (most recent)
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetRatingsAsync_NullOrderBy_ShouldDefaultToLatest()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        var baseTime = DateTime.UtcNow;
        Context.Ratings.AddRange(
            new RatingEntity { UserId = userId, RatingValue = 3, CreatedAt = baseTime.AddHours(-2) },
            new RatingEntity { UserId = userId, RatingValue = 5, CreatedAt = baseTime.AddHours(-1) },
            new RatingEntity { UserId = userId, RatingValue = 4, CreatedAt = baseTime }
        );
        await Context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetRatingsAsync(orderBy: null);

        // Assert
        Assert.Equal(3, result.Ratings.Count);
        Assert.Equal(4, result.Ratings[0].Rating); // Should default to latest (most recent)
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetRatingsAsync_ShouldIncludeUserInformation()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "Jane",
            LastName = "Smith",
            ImageUrl = "https://example.com/image.jpg",
            UserName = "janesmith",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        Context.Ratings.Add(new RatingEntity
        {
            UserId = userId,
            RatingValue = 5,
            Description = "Excellent!",
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetRatingsAsync(orderBy: "latest", pageNumber: 1, pageSize: 20);

        // Assert
        Assert.Single(result.Ratings);
        Assert.Equal("Jane", result.Ratings[0].User.FirstName);
        Assert.Equal("Smith", result.Ratings[0].User.LastName);
        Assert.Equal("https://example.com/image.jpg", result.Ratings[0].User.ImageUrl);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetRatingsAsync_ShouldReturnCorrectPaginationInfo()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        // Create 25 ratings
        for (int i = 0; i < 25; i++)
        {
            Context.Ratings.Add(new RatingEntity
            {
                UserId = userId,
                RatingValue = (i % 5) + 1,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        await Context.SaveChangesAsync();

        // Act - Get first page with page size 10
        var result = await _ratingService.GetRatingsAsync(pageSize: 10, pageNumber: 1);

        // Assert
        Assert.Equal(10, result.Ratings.Count);
        Assert.Equal(25, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task GetRatingsAsync_ShouldReturnSecondPageCorrectly()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        // Create 25 ratings
        for (int i = 0; i < 25; i++)
        {
            Context.Ratings.Add(new RatingEntity
            {
                UserId = userId,
                RatingValue = (i % 5) + 1,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        await Context.SaveChangesAsync();

        // Act - Get second page with page size 10
        var result = await _ratingService.GetRatingsAsync(pageSize: 10, pageNumber: 2);

        // Assert
        Assert.Equal(10, result.Ratings.Count);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(25, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetRatingsAsync_ShouldLimitPageSizeToMax100()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        // Create 5 ratings
        for (int i = 0; i < 5; i++)
        {
            Context.Ratings.Add(new RatingEntity
            {
                UserId = userId,
                RatingValue = 5,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        await Context.SaveChangesAsync();

        // Act - Try to get page size 200 (should be limited to 100)
        var result = await _ratingService.GetRatingsAsync(pageSize: 200, pageNumber: 1);

        // Assert
        Assert.Equal(5, result.Ratings.Count); // All 5 ratings should be returned
        Assert.Equal(100, result.PageSize); // Page size should be limited to 100
    }

    [Fact]
    public async Task GetRatingsAsync_ShouldHandlePageNumberBelow1()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        Context.Ratings.Add(new RatingEntity
        {
            UserId = userId,
            RatingValue = 5,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        // Act - Try to get page 0 (should default to page 1)
        var result = await _ratingService.GetRatingsAsync(pageNumber: 0, pageSize: 20);

        // Assert
        Assert.Equal(1, result.PageNumber);
        Assert.Single(result.Ratings);
    }

    [Fact]
    public async Task GetRatingsAsync_ShouldHandlePageNumberBeyondTotalPages()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);

        Context.Ratings.Add(new RatingEntity
        {
            UserId = userId,
            RatingValue = 5,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        // Act - Try to get page 10 when only 1 page exists
        var result = await _ratingService.GetRatingsAsync(pageNumber: 10, pageSize: 10);

        // Assert
        Assert.Equal(10, result.PageNumber);
        Assert.Empty(result.Ratings); // Should return empty results
    }

    // ── V2 RatingService method tests ────────────────────────────────────────

    [Fact]
    public async Task GetRatingByIdAsync_ReturnsRating_WhenExists()
    {
        var userId = "rating-v2-user";
        var user = new User
        {
            Id = userId, UserName = "rating_v2_user",
            FirstName = "Amr", LastName = "Ali",
            PhoneNumber = "01234567899",
            Governorate = "Cairo", City = "Cairo",
            NormalizedUserName = "RATING_V2_USER",
            SecurityStamp = Guid.NewGuid().ToString()
        };
        Context.Users.Add(user);
        var rating = new RatingEntity { UserId = userId, RatingValue = 4, Description = "Very good service" };
        Context.Ratings.Add(rating);
        await Context.SaveChangesAsync();

        var result = await _ratingService.GetRatingByIdAsync(rating.Id);

        Assert.NotNull(result);
        Assert.Equal(4, result!.Rating);
        Assert.Equal("Very good service", result.Description);
    }

    [Fact]
    public async Task GetRatingByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _ratingService.GetRatingByIdAsync(999999);
        Assert.Null(result);
    }
}

public class CreateRatingDtoValidationTests
{
    [Fact]
    public void Rating_Below1_ShouldBeInvalid()
    {
        // Arrange
        var dto = new CreateRatingDto { Rating = 0 };
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage?.Contains("between 1 and 5") == true);
    }

    [Fact]
    public void Rating_Above5_ShouldBeInvalid()
    {
        // Arrange
        var dto = new CreateRatingDto { Rating = 6 };
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage?.Contains("between 1 and 5") == true);
    }

    [Fact]
    public void Rating_WithinRange_ShouldBeValid()
    {
        // Arrange
        var dto = new CreateRatingDto { Rating = 3 };
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Description_LongerThan500Chars_ShouldBeInvalid()
    {
        // Arrange
        var dto = new CreateRatingDto
        {
            Rating = 5,
            Description = new string('a', 501)
        };
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage?.Contains("500 characters") == true);
    }

    [Fact]
    public void Description_Exactly500Chars_ShouldBeValid()
    {
        // Arrange
        var dto = new CreateRatingDto
        {
            Rating = 5,
            Description = new string('a', 500)
        };
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Description_Null_ShouldBeValid()
    {
        // Arrange
        var dto = new CreateRatingDto
        {
            Rating = 5,
            Description = null
        };
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Description_EmptyString_ShouldBeValid()
    {
        // Arrange
        var dto = new CreateRatingDto
        {
            Rating = 5,
            Description = ""
        };
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        // Assert
        Assert.True(isValid);
    }
}