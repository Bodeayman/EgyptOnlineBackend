using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EgyptOnline.Utilities;

namespace EgyptOnline.Models
{
    /// <summary>
    /// Represents a job request posted by a client looking for a service provider.
    /// </summary>
    public class JobRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ClientUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(ClientUserId))]
        public User? ClientUser { get; set; }

        /// <summary>
        /// e.g. Worker, Contractor, Engineer, Company, Assistant, Sculptor
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ProviderType { get; set; } = "Worker";

        [Required]
        [MaxLength(100)]
        public string Skill { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Governorate { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// Specific to Workers (PerDay, PerPay). Null for other provider types.
        /// </summary>
        public WorkerTypes? WorkerType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PayRate { get; set; }

        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Cancelled

        public string? AcceptedProviderUserId { get; set; }

        [ForeignKey(nameof(AcceptedProviderUserId))]
        public User? AcceptedProviderUser { get; set; }

        // List of interests from service providers
        public List<JobRequestInterest> Interests { get; set; } = new();
    }
}
