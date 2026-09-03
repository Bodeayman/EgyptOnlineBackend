using EgyptOnline.Application.Services.Search;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Tests.Unit;
using EgyptOnline.Utilities;
using Microsoft.EntityFrameworkCore;
using RatingEntity = EgyptOnline.Models.Rating;
using PostEntity = EgyptOnline.Models.Post;
using PostPhotoEntity = EgyptOnline.Models.PostPhoto;

namespace EgyptOnline.Tests.Unit.Search
{
    public class SearchServiceTests : UnitTestBase
    {
        private readonly SearchService _searchService;

        public SearchServiceTests()
        {
            _searchService = new SearchService(Context, null);
        }

        [Fact]
        public async Task SearchWorkersV2Async_ShouldReturnWorkersWithRatingsAndPostPreviews()
        {
            // Arrange
            var user1 = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };
            var user2 = new User
            {
                Id = "user2",
                FirstName = "Jane",
                LastName = "Smith",
                UserName = "janesmith",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 90
            };

            var worker1 = new Worker
            {
                Id = 1,
                UserId = "user1",
                WorkerType = 0,
                ServicePricePerDay = 200,
                Skill = "Plumber"
            };
            var worker2 = new Worker
            {
                Id = 2,
                UserId = "user2",
                WorkerType = 0,
                ServicePricePerDay = 150,
                Skill = "Electrician"
            };

            Context.Users.AddRange(user1, user2);
            Context.Workers.AddRange(worker1, worker2);
            Context.Ratings.AddRange(
                new RatingEntity { UserId = "user1", RatingValue = 5, CreatedAt = DateTime.UtcNow },
                new RatingEntity { UserId = "user1", RatingValue = 4, CreatedAt = DateTime.UtcNow },
                new RatingEntity { UserId = "user2", RatingValue = 3, CreatedAt = DateTime.UtcNow }
            );

            var post1 = new PostEntity { UserId = "user1", Description = "Post 1", CreatedAt = DateTime.UtcNow };
            post1.Photos.Add(new PostPhoto { PostId = 1, PhotoUrl = "https://cdn.example.com/post1.jpg", Order = 1 });
            Context.Posts.Add(post1);

            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchWorkersV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Equal(2, results.Count);
            Assert.True(results[0].averageRating > 0);
            Assert.Equal(2, results[0].totalRatingCount);
            Assert.Single(results[0].postImagePreviews);
            Assert.Equal("https://cdn.example.com/post1.jpg", results[0].postImagePreviews[0]);
        }

        [Fact]
        public async Task SearchWorkersV2Async_ShouldReturnEmptyList_WhenNoWorkersMatch()
        {
            // Arrange
            var filter = new FilterSearchDto { FirstName = "NonExistent" };

            // Act
            var results = await _searchService.SearchWorkersV2Async(filter);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchWorkersV2Async_ShouldLimitPostPreviewsTo4()
        {
            // Arrange
            var user = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };

            var worker = new Worker
            {
                Id = 1,
                UserId = "user1",
                WorkerType = 0,
                ServicePricePerDay = 200,
                Skill = "Plumber"
            };

            Context.Users.Add(user);
            Context.Workers.Add(worker);

            for (int i = 1; i <= 6; i++)
            {
                var post = new PostEntity { UserId = "user1", Description = $"Post {i}", CreatedAt = DateTime.UtcNow };
                post.Photos.Add(new PostPhotoEntity { PostId = i, PhotoUrl = $"https://cdn.example.com/post{i}.jpg", Order = 1 });
                Context.Posts.Add(post);
            }

            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchWorkersV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Single(results);
            Assert.Equal(4, results[0].postImagePreviews.Count);
        }

        [Fact]
        public async Task SearchWorkersV2Async_ShouldHandleWorkersWithNoPosts()
        {
            // Arrange
            var user = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };

            var worker = new Worker
            {
                Id = 1,
                UserId = "user1",
                WorkerType = 0,
                ServicePricePerDay = 200,
                Skill = "Plumber"
            };

            Context.Users.Add(user);
            Context.Workers.Add(worker);
            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchWorkersV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Single(results);
            Assert.Empty(results[0].postImagePreviews);
        }

        [Fact]
        public async Task SearchWorkersV2Async_ShouldHandleWorkersWithNoRatings()
        {
            // Arrange
            var user = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };

            var worker = new Worker
            {
                Id = 1,
                UserId = "user1",
                WorkerType = 0,
                ServicePricePerDay = 200,
                Skill = "Plumber"
            };

            Context.Users.Add(user);
            Context.Workers.Add(worker);
            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchWorkersV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Single(results);
            Assert.Equal(0, results[0].averageRating);
            Assert.Equal(0, results[0].totalRatingCount);
        }

        [Fact]
        public async Task SearchCompaniesV2Async_ShouldReturnCompaniesWithRatingsAndPostPreviews()
        {
            // Arrange
            var user = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };

            var company = new Company
            {
                Id = 1,
                UserId = "user1",
                Business = "Construction",
                Owner = "John Smith"
            };

            Context.Users.Add(user);
            Context.Companies.Add(company);
            Context.Ratings.Add(new RatingEntity { UserId = "user1", RatingValue = 5, CreatedAt = DateTime.UtcNow });

            var post = new PostEntity { UserId = "user1", Description = "Post 1", CreatedAt = DateTime.UtcNow };
            post.Photos.Add(new PostPhotoEntity { PostId = 1, PhotoUrl = "https://cdn.example.com/post1.jpg", Order = 1 });
            Context.Posts.Add(post);
            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchCompaniesV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Single(results);
            Assert.Equal(5.0, results[0].averageRating);
            Assert.Equal(1, results[0].totalRatingCount);
            Assert.Single(results[0].postImagePreviews);
        }

        [Fact]
        public async Task SearchContractorsV2Async_ShouldReturnContractorsWithRatingsAndPostPreviews()
        {
            // Arrange
            var user = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };

            var contractor = new Contractor
            {
                Id = 1,
                UserId = "user1",
                Specialization = "Electrical"
            };

            Context.Users.Add(user);
            Context.Contractors.Add(contractor);
            Context.Ratings.Add(new RatingEntity { UserId = "user1", RatingValue = 4, CreatedAt = DateTime.UtcNow });

            var post = new PostEntity { UserId = "user1", Description = "Post 1", CreatedAt = DateTime.UtcNow };
            post.Photos.Add(new PostPhotoEntity { PostId = 1, PhotoUrl = "https://cdn.example.com/post1.jpg", Order = 1 });
            Context.Posts.Add(post);
            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchContractorsV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Single(results);
            Assert.Equal(4.0, results[0].averageRating);
            Assert.Equal(1, results[0].totalRatingCount);
            Assert.Single(results[0].postImagePreviews);
        }

        [Fact]
        public async Task SearchMarketPlacesV2Async_ShouldReturnMarketPlacesWithRatingsAndPostPreviews()
        {
            // Arrange
            var user = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };

            var marketplace = new MarketPlace
            {
                Id = 1,
                UserId = "user1",
                MarketPlace = "Electronics",
                Business = "Electronics Store",
                Owner = "John Smith"
            };

            Context.Users.Add(user);
            Context.MarketPlaces.Add(marketplace);
            Context.Ratings.Add(new RatingEntity { UserId = "user1", RatingValue = 5, CreatedAt = DateTime.UtcNow });

            var post = new PostEntity { UserId = "user1", Description = "Post 1", CreatedAt = DateTime.UtcNow };
            post.Photos.Add(new PostPhotoEntity { PostId = 1, PhotoUrl = "https://cdn.example.com/post1.jpg", Order = 1 });
            Context.Posts.Add(post);
            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchMarketPlacesV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Single(results);
            Assert.Equal(5.0, results[0].averageRating);
            Assert.Equal(1, results[0].totalRatingCount);
            Assert.Single(results[0].postImagePreviews);
        }

        [Fact]
        public async Task SearchEngineersV2Async_ShouldReturnEngineersWithRatingsAndPostPreviews()
        {
            // Arrange
            var user = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };

            var engineer = new Engineer
            {
                Id = 1,
                UserId = "user1",
                Specialization = "Civil"
            };

            Context.Users.Add(user);
            Context.Engineers.Add(engineer);
            Context.Ratings.Add(new RatingEntity { UserId = "user1", RatingValue = 4, CreatedAt = DateTime.UtcNow });

            var post = new PostEntity { UserId = "user1", Description = "Post 1", CreatedAt = DateTime.UtcNow };
            post.Photos.Add(new PostPhotoEntity { PostId = 1, PhotoUrl = "https://cdn.example.com/post1.jpg", Order = 1 });
            Context.Posts.Add(post);
            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchEngineersV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Single(results);
            Assert.Equal(4.0, results[0].averageRating);
            Assert.Equal(1, results[0].totalRatingCount);
            Assert.Single(results[0].postImagePreviews);
        }

        [Fact]
        public async Task SearchAssistantsV2Async_ShouldReturnAssistantsWithRatingsAndPostPreviews()
        {
            // Arrange
            var user = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };

            var assistant = new Assistant
            {
                Id = 1,
                UserId = "user1",
                Skill = "Helper"
            };

            Context.Users.Add(user);
            Context.Assistants.Add(assistant);
            Context.Ratings.Add(new RatingEntity { UserId = "user1", RatingValue = 5, CreatedAt = DateTime.UtcNow });

            var post = new PostEntity { UserId = "user1", Description = "Post 1", CreatedAt = DateTime.UtcNow };
            post.Photos.Add(new PostPhotoEntity { PostId = 1, PhotoUrl = "https://cdn.example.com/post1.jpg", Order = 1 });
            Context.Posts.Add(post);
            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchAssistantsV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Single(results);
            Assert.Equal(5.0, results[0].averageRating);
            Assert.Equal(1, results[0].totalRatingCount);
            Assert.Single(results[0].postImagePreviews);
        }

        [Fact]
        public async Task SearchSculptorsV2Async_ShouldReturnSculptorsWithRatingsAndPostPreviews()
        {
            // Arrange
            var user = new User
            {
                Id = "user1",
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100
            };

            var sculptor = new Sculptor
            {
                Id = 1,
                UserId = "user1",
                WorkerType = WorkerTypes.PerDay
            };

            Context.Users.Add(user);
            Context.Sculptors.Add(sculptor);
            Context.Ratings.Add(new RatingEntity { UserId = "user1", RatingValue = 4, CreatedAt = DateTime.UtcNow });

            var post = new PostEntity { UserId = "user1", Description = "Post 1", CreatedAt = DateTime.UtcNow };
            post.Photos.Add(new PostPhotoEntity { PostId = 1, PhotoUrl = "https://cdn.example.com/post1.jpg", Order = 1 });
            Context.Posts.Add(post);
            await Context.SaveChangesAsync();

            // Act
            var results = await _searchService.SearchSculptorsV2Async(new FilterSearchDto { PageNumber = 1 });

            // Assert
            Assert.Single(results);
            Assert.Equal(4.0, results[0].averageRating);
            Assert.Equal(1, results[0].totalRatingCount);
            Assert.Single(results[0].postImagePreviews);
            Assert.Equal("Sculptor", results[0].skill);
        }
    }
}
