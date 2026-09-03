using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PostEntity = EgyptOnline.Models.Post;

namespace EgyptOnline.Application.Services.Post
{
    /// <summary>
    /// Service for handling post operations
    /// </summary>
    public class PostService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICDNService _cdnService;

        public PostService(ApplicationDbContext context, ICDNService cdnService)
        {
            _context = context;
            _cdnService = cdnService;
        }

        /// <summary>
        /// Create a new post with photos
        /// </summary>
        public async Task<PostResponseDto> CreatePostAsync(string userId, CreatePostDto dto, List<IFormFile>? photos)
        {
            // Validate photo count
            if (photos != null && photos.Count > 4)
            {
                throw new ArgumentException("Maximum 4 photos allowed per post");
            }

            // Upload photos and collect URLs
            var photoUrls = new List<string>();
            var uploadedObjectKeys = new List<string>();

            try
            {
                if (photos != null && photos.Count > 0)
                {
                    for (int i = 0; i < photos.Count; i++)
                    {
                        var photo = photos[i];
                        if (photo == null || photo.Length == 0)
                            continue;

                        // Validate and upload photo
                        var fileBytes = await ReadFileBytesAsync(photo);
                        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                        var fileName = $"post_{Guid.NewGuid()}{extension}";
                        var objectKey = $"posts/{fileName}";

                        var imageUrl = await _cdnService.UploadImageAsync(fileBytes, fileName, "posts");
                        photoUrls.Add(imageUrl);
                        uploadedObjectKeys.Add(objectKey);
                    }
                }

                // Create post entity
                var post = new PostEntity
                {
                    UserId = userId,
                    Description = dto.Description,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Posts.Add(post);
                await _context.SaveChangesAsync();

                // Create photo entities
                for (int i = 0; i < photoUrls.Count; i++)
                {
                    var postPhoto = new PostPhoto
                    {
                        PostId = post.Id,
                        PhotoUrl = photoUrls[i],
                        Order = i + 1,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.PostPhotos.Add(postPhoto);
                }

                await _context.SaveChangesAsync();

                // Reload with user data
                var savedPost = await _context.Posts
                    .Include(p => p.User)
                    .Include(p => p.Photos.OrderBy(ph => ph.Order))
                    .FirstAsync(p => p.Id == post.Id);

                return MapToResponseDto(savedPost);
            }
            catch
            {
                // Rollback: delete uploaded photos if database save fails
                foreach (var objectKey in uploadedObjectKeys)
                {
                    try
                    {
                        await _cdnService.DeleteImageAsync(objectKey);
                    }
                    catch
                    {
                        // Log but continue cleanup
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Read file bytes from IFormFile
        /// </summary>
        private static async Task<byte[]> ReadFileBytesAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Map Post entity to PostResponseDto
        /// </summary>
        private static PostResponseDto MapToResponseDto(PostEntity post)
        {
            return new PostResponseDto
            {
                Id = post.Id,
                Description = post.Description,
                CreatedAt = post.CreatedAt,
                PhotoUrls = post.Photos.OrderBy(p => p.Order).Select(p => p.PhotoUrl).ToList(),
                User = post.User != null ? new PostUserDto
                {
                    FirstName = post.User.FirstName,
                    LastName = post.User.LastName,
                    ImageUrl = post.User.ImageUrl
                } : null
            };
        }

        /// <summary>
        /// Get posts for a specific user
        /// </summary>
        public async Task<List<PostResponseDto>> GetMyPostsAsync(string userId)
        {
            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Photos.OrderBy(ph => ph.Order))
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return posts.Select(MapToResponseDto).ToList();
        }

        /// <summary>
        /// Get a single post by ID with author profile information
        /// </summary>
        public async Task<PostWithAuthorProfileDto?> GetPostByIdAsync(int id)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Photos.OrderBy(ph => ph.Order))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return null;

            var userIds = new List<string> { post.UserId };
            var ratings = await GetRatingsForUsers(userIds);
            var userProfiles = await GetUserProfiles(userIds);

            return MapToPostWithAuthorProfileDto(post, ratings, userProfiles);
        }

        /// <summary>
        /// Delete a post owned by the specified user
        /// </summary>
        public async Task<bool> DeletePostAsync(int id, string userId)
        {
            var post = await _context.Posts
                .Include(p => p.Photos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return false;

            if (post.UserId != userId)
            {
                throw new UnauthorizedAccessException("ليس لديك صلاحية لحذف هذا المنشور");
            }

            foreach (var photo in post.Photos)
            {
                try
                {
                    await _cdnService.DeleteImageAsync(photo.PhotoUrl);
                }
                catch
                {
                    // Continue cleanup
                }
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Get all posts with author profile information
        /// </summary>
        public async Task<List<PostWithAuthorProfileDto>> GetAllPostsAsync(int pageNumber = 1, int pageSize = 15)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Photos.OrderBy(ph => ph.Order))
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (posts.Count == 0)
                return new List<PostWithAuthorProfileDto>();

            var userIds = posts.Select(p => p.UserId).Distinct().ToList();

            var ratings = await GetRatingsForUsers(userIds);

            var userProfiles = await GetUserProfiles(userIds);

            return posts.Select(post => MapToPostWithAuthorProfileDto(post, ratings, userProfiles)).ToList();
        }

        /// <summary>
        /// Get ratings for a batch of users
        /// </summary>
        private async Task<Dictionary<string, (double average, int count)>> GetRatingsForUsers(List<string> userIds)
        {
            var ratings = await _context.Ratings
                .Where(r => userIds.Contains(r.UserId))
                .GroupBy(r => r.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Average = g.Average(r => r.RatingValue),
                    Count = g.Count()
                })
                .ToListAsync();

            return ratings.ToDictionary(r => r.UserId, r => (r.Average, r.Count));
        }

        /// <summary>
        /// Get provider profiles for a batch of users
        /// </summary>
        private async Task<Dictionary<string, (string skill, string? typeOfService, string? aboutMe, string governorate, string city, string? district)>> GetUserProfiles(List<string> userIds)
        {
            var profiles = new Dictionary<string, (string skill, string? typeOfService, string? aboutMe, string governorate, string city, string? district)>();

            var workers = await _context.Workers
                .Include(w => w.User)
                .Where(w => userIds.Contains(w.UserId))
                .ToListAsync();

            foreach (var worker in workers)
            {
                profiles[worker.UserId] = (
                    worker.Skill ?? string.Empty,
                    worker.ProviderType?.ToString(),
                    worker.Bio,
                    worker.User.Governorate ?? string.Empty,
                    worker.User.City ?? string.Empty,
                    worker.User.District
                );
            }

            var companies = await _context.Companies
                .Include(c => c.User)
                .Where(c => userIds.Contains(c.UserId))
                .ToListAsync();

            foreach (var company in companies)
            {
                profiles[company.UserId] = (
                    company.Business ?? string.Empty,
                    company.ProviderType?.ToString(),
                    company.Bio,
                    company.User.Governorate ?? string.Empty,
                    company.User.City ?? string.Empty,
                    company.User.District
                );
            }

            var contractors = await _context.Contractors
                .Include(c => c.User)
                .Where(c => userIds.Contains(c.UserId))
                .ToListAsync();

            foreach (var contractor in contractors)
            {
                profiles[contractor.UserId] = (
                    contractor.Specialization ?? string.Empty,
                    contractor.ProviderType?.ToString(),
                    contractor.Bio,
                    contractor.User.Governorate ?? string.Empty,
                    contractor.User.City ?? string.Empty,
                    contractor.User.District
                );
            }

            var marketplaces = await _context.MarketPlaces
                .Include(m => m.User)
                .Where(m => userIds.Contains(m.UserId))
                .ToListAsync();

            foreach (var marketplace in marketplaces)
            {
                profiles[marketplace.UserId] = (
                    marketplace.MarketPlace ?? string.Empty,
                    marketplace.ProviderType?.ToString(),
                    marketplace.Bio,
                    marketplace.User.Governorate ?? string.Empty,
                    marketplace.User.City ?? string.Empty,
                    marketplace.User.District
                );
            }

            var engineers = await _context.Engineers
                .Include(e => e.User)
                .Where(e => userIds.Contains(e.UserId))
                .ToListAsync();

            foreach (var engineer in engineers)
            {
                profiles[engineer.UserId] = (
                    engineer.Specialization ?? string.Empty,
                    engineer.ProviderType?.ToString(),
                    engineer.Bio,
                    engineer.User.Governorate ?? string.Empty,
                    engineer.User.City ?? string.Empty,
                    engineer.User.District
                );
            }

            var assistants = await _context.Assistants
                .Include(a => a.User)
                .Where(a => userIds.Contains(a.UserId))
                .ToListAsync();

            foreach (var assistant in assistants)
            {
                profiles[assistant.UserId] = (
                    assistant.Skill ?? string.Empty,
                    assistant.ProviderType?.ToString(),
                    assistant.Bio,
                    assistant.User.Governorate ?? string.Empty,
                    assistant.User.City ?? string.Empty,
                    assistant.User.District
                );
            }

            var sculptors = await _context.Sculptors
                .Include(s => s.User)
                .Where(s => userIds.Contains(s.UserId))
                .ToListAsync();

            foreach (var sculptor in sculptors)
            {
                profiles[sculptor.UserId] = (
                    sculptor.GetSpecialization(),
                    sculptor.ProviderType?.ToString(),
                    sculptor.Bio,
                    sculptor.User.Governorate ?? string.Empty,
                    sculptor.User.City ?? string.Empty,
                    sculptor.User.District
                );
            }

            return profiles;
        }

        /// <summary>
        /// Map Post entity to PostWithAuthorProfileDto
        /// </summary>
        private static PostWithAuthorProfileDto MapToPostWithAuthorProfileDto(PostEntity post, Dictionary<string, (double average, int count)> ratings, Dictionary<string, (string skill, string? typeOfService, string? aboutMe, string governorate, string city, string? district)> userProfiles)
        {
            var profile = userProfiles.GetValueOrDefault(post.UserId, (string.Empty, null, null, string.Empty, string.Empty, null));
            var rating = ratings.GetValueOrDefault(post.UserId, (0, 0));

            return new PostWithAuthorProfileDto
            {
                Id = post.Id,
                Description = post.Description,
                CreatedAt = post.CreatedAt,
                PhotoUrls = post.Photos.OrderBy(p => p.Order).Select(p => p.PhotoUrl).ToList(),
                Author = new PostAuthorProfileDto
                {
                    userId = post.UserId,
                    name = post.User != null ? $"{post.User.FirstName} {post.User.LastName}" : string.Empty,
                    imageUrl = post.User?.ImageUrl,
                    skill = profile.skill,
                    governorate = profile.governorate,
                    city = profile.city,
                    district = profile.district,
                    averageRating = rating.average,
                    totalRatingCount = rating.count,
                    typeOfService = profile.typeOfService,
                    aboutMe = profile.aboutMe
                }
            };
        }
    }
}
