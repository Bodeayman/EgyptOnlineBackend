namespace EgyptOnline.Models
{
    public class Subscription
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public required User User { get; set; }

        public DateOnly EndDate { get; set; }
        public DateOnly StartDate { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Note: IsActive now uses DateOnly
        public bool IsActive => EndDate > DateOnly.FromDateTime(DateTime.UtcNow);


    }
}