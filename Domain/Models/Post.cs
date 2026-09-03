using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    /// <summary>
    /// User post with description and photos
    /// </summary>
    public class Post
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The user who created the post
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        /// <summary>
        /// Post description (max 500 words)
        /// </summary>
        [Required]
        [MaxLength(5000)] // Enough for 500 words (avg 10 chars per word)
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// When the post was created
        /// </summary>
        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Collection of photos attached to this post
        /// </summary>
        public ICollection<PostPhoto> Photos { get; set; } = new List<PostPhoto>();
    }

    /// <summary>
    /// Individual photo attached to a post
    /// </summary>
    public class PostPhoto
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The post this photo belongs to
        /// </summary>
        [Required]
        public int PostId { get; set; }

        [ForeignKey(nameof(PostId))]
        public Post? Post { get; set; }

        /// <summary>
        /// Public URL of the photo (stored in MinIO public bucket)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string PhotoUrl { get; set; } = string.Empty;

        /// <summary>
        /// Order of the photo in the post (1-4)
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// When the photo was uploaded
        /// </summary>
        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
