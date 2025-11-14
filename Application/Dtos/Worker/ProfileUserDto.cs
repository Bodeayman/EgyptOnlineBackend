using EgyptOnline.Utilities;

namespace EgyptOnline.Dtos
{
    public class ShowProfileDto
    {
        // User Information
        // public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? FirstName { get; set; }

        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ImageUrl { get; set; }
        public int Points { get; set; }



        public SubscriptionDto? Subscription { get; set; }

        public ServiceProviderDto? ServiceProvider { get; set; }
        public required string Governorate { get; set; }
        public required string City { get; set; }
        public required string District { get; set; }
    }

    public class SubscriptionDto
    {
        public int Id { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class ServiceProviderDto
    {
        public int Id { get; set; }
        public bool IsAvailable { get; set; }
        public string? Bio { get; set; }
        public string ProviderType { get; set; } = string.Empty;

        public string? Specialization { get; set; } = string.Empty; // skill here please 

        public decimal Pay { get; set; }
        public string? Business { get; set; } = string.Empty;
        public string? Owner { get; set; } = string.Empty;
        public WorkerTypes WorkerTypes { get; set; } = WorkerTypes.PerPay;


        // public string? SpecializationType { get; set; } // "Skill", "Expertise", "Services", etc.
    }
}