using System.ComponentModel.DataAnnotations;
using EgyptOnline.Domain.Attributes;

namespace EgyptOnline.Dtos
{
    /// <summary>
    /// DTO for creating a new post
    /// </summary>
    public class CreatePostDto
    {
        /// <summary>
        /// Post description (max 500 words)
        /// </summary>
        [Required(ErrorMessage = "Description is required")]
        [MaxWords(500, ErrorMessage = "Description cannot exceed 500 words")]
        public string Description { get; set; } = string.Empty;
    }
}
