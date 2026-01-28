using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    public class Subscription
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public required User User { get; set; }

        [Column(TypeName = "timestamptz")]
        public DateTime StartDate { get; set; }

        [Column(TypeName = "timestamptz")]
        public DateTime EndDate { get; set; }

        [Column(TypeName = "timestamptz")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Note: IsActive now uses DateTime (UTC)
        public bool IsActive => EndDate > DateTime.UtcNow;


    }
}