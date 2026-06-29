using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    /// <summary>
    /// Represents a user request to withdraw funds from their wallet balance.
    /// Deducts or locks balance upon creation, and is reviewed by Admin.
    /// Status: pending, approved, rejected
    /// </summary>
    public class WithdrawRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(100)]
        public string DestinationWalletNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string WalletOwnerName { get; set; } = string.Empty;

        /// <summary>pending, approved, rejected</summary>
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "pending";

        public string? ReviewedByAdminId { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamptz")]
        public DateTime? ReviewedAt { get; set; }
    }
}
