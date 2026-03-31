using EgyptOnline.Application.Services.Kyc;
using EgyptOnline.Dtos.Kyc;
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

        public KycController(KycService kycService)
        {
            _kycService = kycService;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] SubmitKycDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var submission = await _kycService.SubmitKycAsync(userId, dto.FrontImagePath, dto.BackImagePath, dto.SelfieImagePath);

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
