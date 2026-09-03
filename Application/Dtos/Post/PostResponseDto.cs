namespace EgyptOnline.Dtos
{
    /// <summary>
    /// DTO for post response
    /// </summary>
    public class PostResponseDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<string> PhotoUrls { get; set; } = new();
        public PostUserDto? User { get; set; }
    }

    /// <summary>
    /// Minimal user info for post (used by GetMyPosts)
    /// </summary>
    public class PostUserDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ImageUrl { get; set; }
    }

    /// <summary>
    /// Extended author profile for posts (used by GetAllPosts)
    /// Contains essential profile information from SearchV2ResultDto relevant for post feed
    /// </summary>
    public class PostAuthorProfileDto
    {
        public string userId { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string? imageUrl { get; set; }
        public string skill { get; set; } = string.Empty;
        public string governorate { get; set; } = string.Empty;
        public string city { get; set; } = string.Empty;
        public string? district { get; set; }
        public double averageRating { get; set; }
        public int totalRatingCount { get; set; }
        public string? typeOfService { get; set; }
        public string? aboutMe { get; set; }
    }

    /// <summary>
    /// DTO for GetAllPosts response with extended author profile
    /// </summary>
    public class PostWithAuthorProfileDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<string> PhotoUrls { get; set; } = new();
        public PostAuthorProfileDto Author { get; set; } = new();
    }
}
