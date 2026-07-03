using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    public class Contract
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ContractorUsername { get; set; } = string.Empty;

        [Required]
        public string EngineerUsername { get; set; } = string.Empty;

        [Required]
        public string WorkerUsername { get; set; } = string.Empty;

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
        /// JSON object of approvals: {"username": true/false}
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

        // ─── 2-Party Simple Contract Properties ───────────────────────────
        public string? ClientUserId { get; set; }
        
        [ForeignKey(nameof(ClientUserId))]
        public User? ClientUser { get; set; }

        public string? WorkerUserId { get; set; }

        [ForeignKey(nameof(WorkerUserId))]
        public User? WorkerUser { get; set; }

        public bool IsSimpleContract { get; set; }
        public int DurationDays { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DailySalary { get; set; }

        [MaxLength(500)]
        public string WorkplaceAddress { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public bool ClientPenaltyPaid { get; set; }
        public bool WorkerPenaltyPaid { get; set; }

        [Column(TypeName = "timestamptz")]
        public DateTime? CheckInTime { get; set; }

        /// <summary>
        /// none, pending, approved, reported
        /// </summary>
        [MaxLength(50)]
        public string CheckInStatus { get; set; } = "none";

        [Column(TypeName = "date")]
        public DateTime? CheckInDate { get; set; }

        public int DaysWorked { get; set; }

        public bool ClientTerminationRequested { get; set; }
        public bool WorkerTerminationRequested { get; set; }
    }
}
