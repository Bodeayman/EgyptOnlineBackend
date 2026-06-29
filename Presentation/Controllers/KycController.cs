using EgyptOnline.Application.Services.Kyc;
using EgyptOnline.Dtos.Kyc;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize(Roles = Roles.User)]
    public class KycController : ControllerBase
    {
        private readonly KycService _kycService;
        private readonly ICDNService _cdnService;

        public KycController(KycService kycService, ICDNService cdnService)
        {
            _kycService = kycService;
            _cdnService = cdnService;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        [HttpPost("submit")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Submit([FromForm] SubmitKycUploadDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                // Validate Front Image
                var frontPath = await ValidateAndUploadImageAsync(dto.FrontImage, $"kyc_front_{userId}");
                if (string.IsNullOrEmpty(frontPath))
                    return BadRequest(new { message = "فشل رفع صورة وجه البطاقة" });

                // Validate Back Image
                var backPath = await ValidateAndUploadImageAsync(dto.BackImage, $"kyc_back_{userId}");
                if (string.IsNullOrEmpty(backPath))
                    return BadRequest(new { message = "فشل رفع صورة ظهر البطاقة" });

                // Validate Selfie Image
                var selfiePath = await ValidateAndUploadImageAsync(dto.SelfieImage, $"kyc_selfie_{userId}");
                if (string.IsNullOrEmpty(selfiePath))
                    return BadRequest(new { message = "فشل رفع الصورة الشخصية (سيلفي)" });

                var submission = await _kycService.SubmitKycAsync(userId, frontPath, backPath, selfiePath);

                return Ok(new
                {
                    message = "تم تقديم طلب التحقق بنجاح وهو قيد المراجعة",
                    data = new
                    {
                        submission.Id,
                        submission.Status,
                        submission.SubmittedAt
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        private async Task<string?> ValidateAndUploadImageAsync(IFormFile file, string filePrefix)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("الملف المرفوع فارغ أو غير موجود.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                throw new ArgumentException("نوع الملف غير صالح. يرجى رفع صور بصيغة JPG, PNG أو WEBP.");

            const int maxFileSize = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxFileSize)
                throw new ArgumentException("حجم الملف كبير جداً. الحد الأقصى هو 5 ميجابايت.");

            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var uniqueFileName = $"{filePrefix}_{Guid.NewGuid()}{extension}";
            return await _cdnService.UploadImageAsync(fileBytes, uniqueFileName, "kyc");
        }

        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var kyc = await _kycService.GetLatestKycAsync(userId);
                if (kyc == null)
                {
                    return Ok(new
                    {
                        data = new
                        {
                            kycStatus = "not_submitted",
                            kycSubmittedAt = (DateTime?)null,
                            kycReviewedAt = (DateTime?)null
                        }
                    });
                }

                return Ok(new
                {
                    data = new
                    {
                        kycId = kyc.Id,
                        kycStatus = kyc.Status,
                        kycSubmittedAt = kyc.SubmittedAt,
                        kycReviewedAt = kyc.ReviewedAt,
                        rejectionReason = kyc.RejectionReason
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
    }
}
