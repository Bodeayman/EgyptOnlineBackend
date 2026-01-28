using Microsoft.AspNetCore.Identity;

namespace EgyptOnline.Models
{
    public class User : IdentityUser
    {
        public string? FirstName { get; set; }

        public string? LastName { get; set; }
        public string? ImageUrl { get; set; }


        //Not needed for now

        public required string Governorate { get; set; } = "Unknown";
        public required string City { get; set; } = "Unknown";
        public string? District { get; set; } = "Unknown";



        // public required LocationCoords LocationCoords { get; set; }
        public List<RefreshToken>? RefreshTokens { get; set; }
        public ServicesProvider? ServiceProvider { get; set; }

        public Subscription? Subscription { get; set; }
        public List<FirebaseToken>? FirebaseTokens { get; set; }

        public int Points { get; set; } = 0;
        public int SubscriptionPoints { get; set; } = 0;

        public string? ReferrerUserName { get; set; }
        public int ReferralRewardCount { get; set; } = 0;

        //For Soft Delete
        // public bool IsDeleted { get; set; }

    }
}