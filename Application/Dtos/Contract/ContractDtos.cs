using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Dtos.Contract
{
    public class CreateContractDto
    {
        [Required(ErrorMessage = "ContractorUsername is required")]
        public string ContractorUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "EngineerUsername is required")]
        public string EngineerUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "WorkerUsername is required")]
        public string WorkerUsername { get; set; } = string.Empty;

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

    public class CreateSimpleContractDto
    {
        [Required(ErrorMessage = "رقم موبايل مقدم الخدمة مطلوب")]
        public string ServiceProviderPhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاريخ بدء العمل مطلوب")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "ساعة الحضور مطلوبة")]
        public TimeSpan ShiftStartTime { get; set; }

        [Required(ErrorMessage = "ساعة الانصراف مطلوبة")]
        public TimeSpan ShiftEndTime { get; set; }

        [Required(ErrorMessage = "عدد الأيام مطلوب")]
        [Range(1, int.MaxValue, ErrorMessage = "عدد الأيام يجب أن يكون 1 على الأقل")]
        public int TotalDays { get; set; }

        [Required(ErrorMessage = "الأجر اليومي مطلوب")]
        [Range(1, int.MaxValue, ErrorMessage = "الأجر اليومي يجب أن يكون أكبر من صفر")]
        public int DailySalary { get; set; }

        [Required(ErrorMessage = "مبلغ الشرط الجزائي مطلوب")]
        [Range(0, int.MaxValue, ErrorMessage = "الشرط الجزائي يجب أن يكون 0 أو أكبر")]
        public int PenaltyAmount { get; set; }

        [Required(ErrorMessage = "المحافظة مطلوبة")]
        public string Governorate { get; set; } = string.Empty;

        [Required(ErrorMessage = "المدينة مطلوبة")]
        public string City { get; set; } = string.Empty;

        public string District { get; set; } = string.Empty;

        // Optional Fields
        public string? DetailedAddress { get; set; }
        public string? Notes { get; set; }
        public string? RestrictedTerms { get; set; }
    }

    public class RespondSimpleContractDto
    {
        [Required]
        public bool Accept { get; set; }
    }
}
