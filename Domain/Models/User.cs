using Microsoft.AspNetCore.Identity;

namespace EgyptOnline.Models
{
    public class User : IdentityUser
    {
        public string? FirstName { get; set; }

        public string? LastName { get; set; }
        public string? ImageUrl { get; set; }
        public required string UserType { get; set; } = "User";

        public string? Location { get; set; } = "Unknown";

        public ServicesProvider? ServiceProvider { get; set; }

        public Subscription? Subscription { get; set; }



    }
}