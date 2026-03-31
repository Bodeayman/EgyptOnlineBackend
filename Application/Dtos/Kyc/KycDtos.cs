using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Dtos.Kyc
{
    public class SubmitKycDto
    {
        [Required(ErrorMessage = "Front image path is required")]
        public string FrontImagePath { get; set; } = string.Empty;

        [Required(ErrorMessage = "Back image path is required")]
        public string BackImagePath { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selfie image path is required")]
        public string SelfieImagePath { get; set; } = string.Empty;
    }

    public class ReviewKycDto
    {
        [Required]
        [RegularExpression("^(approved|rejected)$", ErrorMessage = "Status must be 'approved' or 'rejected'")]
        public string Status { get; set; } = string.Empty;

        public string? RejectionReason { get; set; }
    }
}
