using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using EgyptOnline.Domain.Models.Enums;

namespace EgyptOnline.Models
{
    /// <summary>
    /// Represents a user request to deposit funds by submitting a bank/wallet transfer receipt.
    /// Admin reviews this from the dashboard and either approves or rejects.
    /// Status: pending, approved, rejected
    /// </summary>
    public class DepositRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        [JsonIgnore]
        public User? User { get; set; }

        public int Amount { get; set; }

        [Required]
        [MaxLength(100)]
        public string SourceWalletNumber { get; set; } = string.Empty;

        public PaymentType PaymentType { get; set; } = PaymentType.MobileWallet;

        [Required]
        [MaxLength(200)]
        public string WalletOwnerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string RecipientPhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string ReceiptImagePath { get; set; } = string.Empty;

        /// <summary>pending, approved, rejected</summary>
        [Required]
        [MaxLength(30)]
        [ConcurrencyCheck]
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
