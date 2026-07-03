using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    public class UserWallet
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        // Free Balance: Can be used for cash-out withdrawals and creating new contracts
        [Column(TypeName = "decimal(18,2)")]
        public decimal FreeBalance { get; set; } = 0;

        // Frozen Balance: Locked for active contracts/disputes, cannot be used for withdrawals or new contracts
        [Column(TypeName = "decimal(18,2)")]
        public decimal FrozenBalance { get; set; } = 0;

        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamptz")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
