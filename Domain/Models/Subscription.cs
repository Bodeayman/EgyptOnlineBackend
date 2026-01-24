namespace EgyptOnline.Models
{
    public class Subscription
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public required User User { get; set; }

        public DateTime EndDate { get; set; }
        public DateTime StartDate { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Note: IsActive now uses DateTime (UTC)
        public bool IsActive => EndDate > DateTime.UtcNow;


    }
}