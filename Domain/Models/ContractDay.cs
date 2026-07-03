using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EgyptOnline.Domain.Models.Enums;

namespace EgyptOnline.Domain.Models
{
    [Table("ContractDays")]
    public class ContractDay
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ContractId { get; set; }

        [ForeignKey(nameof(ContractId))]
        public Contract Contract { get; set; } = null!;

        [Required]
        public int DayNumber { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public bool ProviderArrived { get; set; }

        [Required]
        public ContractDayStatus Status { get; set; } = ContractDayStatus.Pending;

        public bool IsProcessed { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public DateTime? ArrivalTime { get; set; }

        public DateTime? DisputeReportedAt { get; set; }

        public string? DisputeReason { get; set; }
    }
}
