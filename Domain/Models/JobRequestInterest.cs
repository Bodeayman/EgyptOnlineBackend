using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    /// <summary>
    /// Represents interest shown by a Service Provider in a Job Request.
    /// </summary>
    public class JobRequestInterest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int JobRequestId { get; set; }

        [ForeignKey(nameof(JobRequestId))]
        public JobRequest? JobRequest { get; set; }

        [Required]
        public string ServiceProviderUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(ServiceProviderUserId))]
        public User? ServiceProviderUser { get; set; }

        public bool IsInterested { get; set; }

        [Column(TypeName = "timestamptz")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
