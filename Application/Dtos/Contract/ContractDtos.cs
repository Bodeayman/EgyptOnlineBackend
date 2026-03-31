using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Dtos.Contract
{
    public class CreateContractDto
    {
        [Required(ErrorMessage = "ContractorId is required")]
        public string ContractorId { get; set; } = string.Empty;

        [Required(ErrorMessage = "EngineerId is required")]
        public string EngineerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "WorkerId is required")]
        public string WorkerId { get; set; } = string.Empty;

        public string TermsAndConditions { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "AgreedTotalAmount must be positive")]
        public decimal AgreedTotalAmount { get; set; }

        public bool SplitEnabled { get; set; }

        [Range(0, int.MaxValue)]
        public int SplitDays { get; set; }

        [Range(0, double.MaxValue)]
        public decimal DailyAmount { get; set; }

        public object? Installments { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PenaltyClauseAmount { get; set; }

        public string PenaltyConditions { get; set; } = string.Empty;

        [Range(0, 100)]
        public double PenaltySplitContractorPercent { get; set; }

        [Range(0, 100)]
        public double PenaltySplitEngineerPercent { get; set; }

        public DateTime? FirstWorkingDay { get; set; }

        [Required(ErrorMessage = "WorkLocation is required")]
        public string WorkLocation { get; set; } = string.Empty;
    }

    public class ContractSignDto
    {
        [Required]
        public bool Accepted { get; set; }
    }

    public class ConfirmArrivalDto
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class ApplyPenaltyDto
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class MarkAttendanceDto
    {
        [Required]
        [RegularExpression("^(attended|absent)$", ErrorMessage = "Status must be 'attended' or 'absent'")]
        public string Status { get; set; } = string.Empty;
    }

    public class DisburseInstallmentDto
    {
        [Range(0, int.MaxValue, ErrorMessage = "InstallmentIndex must be non-negative")]
        public int InstallmentIndex { get; set; }
    }
}
