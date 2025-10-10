using EgyptOnline.Utilities;

namespace EgyptOnline.Dtos
{
    public class ShowProfileDto
    {
        // User Information
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? FirstName { get; set; }

        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Location { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ImageUrl { get; set; }
        public string UserType { get; set; } = string.Empty;
        public int Points { get; set; }

        // Subscription Information (if exists)
        public SubscriptionDto? Subscription { get; set; }

        // Service Provider Information (if user is a service provider)
        public ServiceProviderDto? ServiceProvider { get; set; }

    }

    public class SubscriptionDto
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class ServiceProviderDto
    {
        public int Id { get; set; }
        public bool IsAvailable { get; set; }
        public string? Location { get; set; }
        public string? Bio { get; set; }
        public string ProviderType { get; set; } = string.Empty;

        // Dynamic specialization field based on provider type
        public string? Specialization { get; set; }
        public string? SpecializationType { get; set; } // "Skill", "Expertise", "Services", etc.
    }
}