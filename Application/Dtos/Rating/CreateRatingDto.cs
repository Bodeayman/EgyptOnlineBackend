using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Dtos
{
    /// <summary>
    /// DTO for creating a new rating
    /// </summary>
    public class CreateRatingDto
    {
        /// <summary>
        /// ID of the user/provider being rated
        /// </summary>
        [Required(ErrorMessage = "Target user ID is required")]
        public string TargetUserId { get; set; } = string.Empty;

        /// <summary>
        /// Rating value from 1 to 5, inclusive
        /// </summary>
        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        /// <summary>
        /// Optional description (0-500 characters)
        /// </summary>
        [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }
    }
}