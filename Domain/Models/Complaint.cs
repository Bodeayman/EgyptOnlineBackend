using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EgyptOnline.Models
{
    /// <summary>
    /// A complaint filed by any party in a contract (contractor, engineer, or worker).
    /// When a complaint is filed the related contract is paused until the admin resolves it.
    ///
    /// Status flow:
    ///   open  →  under_review  →  resolved
    ///                          →  rejected
    /// </summary>
    public class Complaint
    {
        [Key]
        public int Id { get; set; }

        // ── Who filed it ─────────────────────────────────────────────────────
        [Required]
        public string ReporterUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(ReporterUserId))]
        public User? Reporter { get; set; }

        // ── Related contract (mandatory — complaints are always contract-scoped) ─
        [Required]
        public int ContractId { get; set; }

        [ForeignKey(nameof(ContractId))]
        public Contract? Contract { get; set; }

        // ── Complaint details ────────────────────────────────────────────────
        /// <summary>
        /// Reason category.
        /// Examples: "no_show", "contract_termination", "payment_dispute", "misconduct", "other"
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>Free-text description provided by the reporter.</summary>
        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        // ── Status ──────────────────────────────────────────────────────────
        /// <summary>open, under_review, resolved, rejected</summary>
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "open";

        // ── Admin resolution ────────────────────────────────────────────────
        public string? ResolvedByAdminId { get; set; }

        [MaxLength(2000)]
        public string? AdminNote { get; set; }

        // ── Timestamps ──────────────────────────────────────────────────────
        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamptz")]
        public DateTime? ResolvedAt { get; set; }
    }
}
