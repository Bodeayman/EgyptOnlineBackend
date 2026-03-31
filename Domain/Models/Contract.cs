using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    public class Contract
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ContractorId { get; set; } = string.Empty;

        [ForeignKey(nameof(ContractorId))]
        public User? Contractor { get; set; }

        [Required]
        public string EngineerId { get; set; } = string.Empty;

        [ForeignKey(nameof(EngineerId))]
        public User? Engineer { get; set; }

        [Required]
        public string WorkerId { get; set; } = string.Empty;

        [ForeignKey(nameof(WorkerId))]
        public User? Worker { get; set; }

        public string TermsAndConditions { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AgreedTotalAmount { get; set; }

        public bool SplitEnabled { get; set; }
        public int SplitDays { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyAmount { get; set; }

        /// <summary>
        /// JSON array of installment objects: [{dayIndex, amount, dueDate, status}]
        /// Stored as jsonb in PostgreSQL
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string InstallmentsJson { get; set; } = "[]";

        [Column(TypeName = "decimal(18,2)")]
        public decimal PenaltyClauseAmount { get; set; }

        public string PenaltyConditions { get; set; } = string.Empty;
        public double PenaltySplitContractorPercent { get; set; }
        public double PenaltySplitEngineerPercent { get; set; }

        /// <summary>
        /// pending_signatures, active, completed, cancelled
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "pending_signatures";

        /// <summary>
        /// JSON object of approvals: {"userId": true/false}
        /// Stored as jsonb in PostgreSQL
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string ApprovalsJson { get; set; } = "{}";

        [Column(TypeName = "decimal(18,2)")]
        public decimal EscrowAmount { get; set; }

        public bool ArrivalConfirmed { get; set; }
        public bool NoShowProcessed { get; set; }

        /// <summary>
        /// JSON array of history entries: [{id, type, message, createdAt}]
        /// Stored as jsonb in PostgreSQL
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string HistoryJson { get; set; } = "[]";

        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamptz")]
        public DateTime? FirstWorkingDay { get; set; }

        [MaxLength(500)]
        public string WorkLocation { get; set; } = string.Empty;

        [Column(TypeName = "timestamptz")]
        public DateTime? CancelledAt { get; set; }

        public string? CancelledBy { get; set; }
    }
}
