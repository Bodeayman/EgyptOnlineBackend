namespace EgyptOnline.Dtos
{
    /// <summary>
    /// DTO for individual rating response
    /// </summary>
    public class RatingResponseDto
    {
        public int Id { get; set; }
        public int Rating { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Information about the user who submitted the rating
        /// </summary>
        public RatingUserDto User { get; set; } = null!;

        /// <summary>
        /// Information about the user/provider being rated
        /// </summary>
        public RatingUserDto TargetUser { get; set; } = null!;
    }

    /// <summary>
    /// Simplified user information for rating display
    /// </summary>
    public class RatingUserDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ImageUrl { get; set; }
    }

    /// <summary>
    /// DTO for ratings summary statistics
    /// </summary>
    public class RatingsSummaryDto
    {
        /// <summary>
        /// Average rating across all ratings
        /// </summary>
        public double AverageRating { get; set; }

        /// <summary>
        /// Total number of ratings
        /// </summary>
        public int TotalRatings { get; set; }

        /// <summary>
        /// Distribution of ratings by star value
        /// </summary>
        public RatingDistributionDto Distribution { get; set; } = null!;
    }

    /// <summary>
    /// Distribution of ratings by star value
    /// </summary>
    public class RatingDistributionDto
    {
        public int FiveStars { get; set; }
        public int FourStars { get; set; }
        public int ThreeStars { get; set; }
        public int TwoStars { get; set; }
        public int OneStar { get; set; }
    }

    /// <summary>
    /// Complete response for GET ratings endpoint
    /// </summary>
    public class RatingsResponseDto
    {
        public RatingsSummaryDto Summary { get; set; } = null!;
        public List<RatingResponseDto> Ratings { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
    }
}