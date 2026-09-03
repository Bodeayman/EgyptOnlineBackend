namespace EgyptOnline.Dtos
{
    /// <summary>
    /// Enhanced search result for Search V2 with ratings and post previews
    /// </summary>
    public class SearchV2ResultDto
    {
        public string userId { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string skill { get; set; } = string.Empty;
        public string governorate { get; set; } = string.Empty;
        public string city { get; set; } = string.Empty;
        public string? district { get; set; }
        public decimal pay { get; set; }
        public string? owner { get; set; }
        public string? imageUrl { get; set; }
        public bool isCompany { get; set; }
        public int workerType { get; set; }
        public string mobileNumber { get; set; } = string.Empty;
        public string? typeOfService { get; set; }
        public string? aboutMe { get; set; }
        public bool isOccupied { get; set; }
        public string? marketPlace { get; set; }
        public string? derivedSpec { get; set; }

        // V2 additions
        public double averageRating { get; set; }
        public int totalRatingCount { get; set; }
        public List<string> postImagePreviews { get; set; } = new();
    }
}
