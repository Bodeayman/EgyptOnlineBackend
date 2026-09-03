using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;
using RatingEntity = EgyptOnline.Models.Rating;

namespace EgyptOnline.Application.Services.Rating
{
    /// <summary>
    /// Service for handling rating operations
    /// </summary>
    public class RatingService
    {
        private readonly ApplicationDbContext _context;

        public RatingService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Submit a new rating for a user
        /// </summary>
        public async Task<RatingResponseDto> SubmitRatingAsync(string userId, CreateRatingDto dto)
        {
            var rating = new RatingEntity
            {
                UserId = userId,
                RatingValue = dto.Rating,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow
            };

            _context.Ratings.Add(rating);
            await _context.SaveChangesAsync();

            // Reload with user data
            var savedRating = await _context.Ratings
                .Include(r => r.User)
                .FirstAsync(r => r.Id == rating.Id);

            return MapToResponseDto(savedRating);
        }

        /// <summary>
        /// Get rating details by ID
        /// </summary>
        public async Task<RatingResponseDto?> GetRatingByIdAsync(int id)
        {
            var rating = await _context.Ratings
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            return rating != null ? MapToResponseDto(rating) : null;
        }

        /// <summary>
        /// Get ratings with optional ordering (latest or best) and pagination
        /// </summary>
        public async Task<RatingsResponseDto> GetRatingsAsync(string? orderBy = null, int pageNumber = 1, int pageSize = 20)
        {
            // Default to 'latest' if orderBy is null or invalid
            var validOrderBy = orderBy?.ToLower() == "best" ? "best" : "latest";

            // Get total count for pagination
            var totalItems = await _context.Ratings.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Ensure pageNumber is valid
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, Math.Min(100, pageSize)); // Limit page size to max 100

            // Get base query
            var query = _context.Ratings.Include(r => r.User).AsQueryable();

            // Apply ordering
            query = validOrderBy == "best" 
                ? query.OrderByDescending(r => r.RatingValue).ThenByDescending(r => r.CreatedAt)
                : query.OrderByDescending(r => r.CreatedAt);

            // Apply pagination
            var ratings = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Calculate statistics
            var summary = await CalculateSummaryAsync();

            var response = new RatingsResponseDto
            {
                Summary = summary,
                Ratings = ratings.Select(MapToResponseDto).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalItems = totalItems
            };

            return response;
        }

        /// <summary>
        /// Calculate rating summary statistics
        /// </summary>
        private async Task<RatingsSummaryDto> CalculateSummaryAsync()
        {
            var ratings = await _context.Ratings.ToListAsync();

            if (!ratings.Any())
            {
                return new RatingsSummaryDto
                {
                    AverageRating = 0,
                    TotalRatings = 0,
                    Distribution = new RatingDistributionDto
                    {
                        FiveStars = 0,
                        FourStars = 0,
                        ThreeStars = 0,
                        TwoStars = 0,
                        OneStar = 0
                    }
                };
            }

            var totalRatings = ratings.Count;
            var averageRating = ratings.Average(r => r.RatingValue);

            var distribution = new RatingDistributionDto
            {
                FiveStars = ratings.Count(r => r.RatingValue == 5),
                FourStars = ratings.Count(r => r.RatingValue == 4),
                ThreeStars = ratings.Count(r => r.RatingValue == 3),
                TwoStars = ratings.Count(r => r.RatingValue == 2),
                OneStar = ratings.Count(r => r.RatingValue == 1)
            };

            return new RatingsSummaryDto
            {
                AverageRating = Math.Round(averageRating, 1),
                TotalRatings = totalRatings,
                Distribution = distribution
            };
        }

        /// <summary>
        /// Map Rating entity to RatingResponseDto
        /// </summary>
        private static RatingResponseDto MapToResponseDto(RatingEntity rating)
        {
            return new RatingResponseDto
            {
                Id = rating.Id,
                Rating = rating.RatingValue,
                Description = rating.Description,
                CreatedAt = rating.CreatedAt,
                User = new RatingUserDto
                {
                    FirstName = rating.User?.FirstName,
                    LastName = rating.User?.LastName,
                    ImageUrl = rating.User?.ImageUrl
                }
            };
        }
    }
}