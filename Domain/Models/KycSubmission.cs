using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    public class KycSubmission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        /// <summary>
        /// not_submitted, pending, approved, rejected
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "pending";

        [MaxLength(500)]
        public string? FrontImagePath { get; set; }

        [MaxLength(500)]
        public string? BackImagePath { get; set; }

        [MaxLength(500)]
        public string? SelfieImagePath { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        public string? ReviewedByAdminId { get; set; }

        [Column(TypeName = "timestamptz")]
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamptz")]
        public DateTime? ReviewedAt { get; set; }
    }
}
