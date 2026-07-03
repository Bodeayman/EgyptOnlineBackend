using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Domain.Models;

namespace EgyptOnline.Models
{
    public class Contract
    {
        [Key]
        public int Id { get; set; }

        // Client (the user creating the contract)
        public string? ClientUserId { get; set; }

        [ForeignKey(nameof(ClientUserId))]
        public User? ClientUser { get; set; }

        // Service Provider identified by phone number
        public string? ServiceProviderPhoneNumber { get; set; }

        // Mandatory Fields (User Input)
        public DateTime? StartDate { get; set; }
        public TimeSpan? ShiftStartTime { get; set; }
        public TimeSpan? ShiftEndTime { get; set; }
        public int? TotalDays { get; set; }
        public decimal? TotalAmount { get; set; }

        // Mandatory Penalty Field (defaults to 0 if not set)
        [Column(TypeName = "decimal(18,2)")]
        public decimal PenaltyAmount { get; set; } = 0;

        // Location Context (Mandatory)
        public string? Governorate { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }

        // Optional Fields (User Input)
        public string? DetailedAddress { get; set; }
        public string? Notes { get; set; }
        public string? RestrictedTerms { get; set; }

        // System Fields
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "pending";

        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamptz")]
        public DateTime? CancelledAt { get; set; }

        public string? CancelledBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? TerminatedAt { get; set; }
        public string? TerminatedBy { get; set; }
        public string? TerminationReason { get; set; }

        // Navigation Property
        public ICollection<Domain.Models.ContractDay> ContractDays { get; set; } = new List<Domain.Models.ContractDay>();
    }
}
