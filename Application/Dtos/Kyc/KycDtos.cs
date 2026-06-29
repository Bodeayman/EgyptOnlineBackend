using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

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

    public class SubmitKycUploadDto
    {
        [Required(ErrorMessage = "صورة وجه البطاقة مطلوبة")]
        public IFormFile FrontImage { get; set; } = null!;

        [Required(ErrorMessage = "صورة ظهر البطاقة مطلوبة")]
        public IFormFile BackImage { get; set; } = null!;

        [Required(ErrorMessage = "الصورة الشخصية (سيلفي) مطلوبة")]
        public IFormFile SelfieImage { get; set; } = null!;
    }

    public class ReviewKycDto
    {
        [Required]
        [RegularExpression("^(approved|rejected|edit_required)$", ErrorMessage = "Status must be 'approved', 'rejected', or 'edit_required'")]
        public string Status { get; set; } = string.Empty;

        public string? RejectionReason { get; set; }
    }
}
