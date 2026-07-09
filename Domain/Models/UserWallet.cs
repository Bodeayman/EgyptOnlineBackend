using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EgyptOnline.Models
{
    public class UserWallet
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        [JsonIgnore]
        public User? User { get; set; }

        // Free Balance: Can be used for cash-out withdrawals and creating new contracts
        [ConcurrencyCheck]
        public int FreeBalance { get; set; } = 0;

        // Frozen Balance: Locked for active contracts/disputes, cannot be used for withdrawals or new contracts
        [ConcurrencyCheck]
        public int FrozenBalance { get; set; } = 0;


        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamptz")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
