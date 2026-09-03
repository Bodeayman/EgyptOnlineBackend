using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    /// <summary>
    /// User rating for the platform or services
    /// </summary>
    public class Rating
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The user who submitted the rating
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        /// <summary>
        /// Rating value from 1 to 5
        /// </summary>
        [Required]
        [Range(1, 5)]
        public int RatingValue { get; set; }

        /// <summary>
        /// Optional description (0-500 characters)
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// When the rating was created
        /// </summary>
        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}