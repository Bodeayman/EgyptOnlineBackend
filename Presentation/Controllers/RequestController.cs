using EgyptOnline.Application.Services.JobRequest;
using EgyptOnline.Dtos.JobRequest;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace EgyptOnline.Presentation.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/requests")]
    [ApiVersion("1.0")]
    [Authorize(Roles = Roles.User)]
    public class RequestController : ControllerBase
    {
        private readonly JobRequestService _service;

        public RequestController(JobRequestService service)
        {
            _service = service;
        }

        private string? GetUserId()
        {
            var claim = User.FindFirst("uid")
                        ?? User.FindFirst(ClaimTypes.NameIdentifier)
                        ?? User.FindFirst("sub")
                        ?? User.FindFirst("user_id")
                        ?? User.FindFirst("id");

            return claim?.Value;
        }

        /// <summary>
        /// Post a new job request.
        /// POST /api/v1/Request
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateJobRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Validation: if ProviderType is Worker, WorkerType must be specified
            if (dto.ProviderType.Equals("Worker", StringComparison.OrdinalIgnoreCase) && dto.WorkerType == null)
            {
                return BadRequest(new { message = "عند اختيار نوع مزود عامل (Worker)، يجب تحديد نوع الحساب اليومي أو بالمشروع (WorkerType)" });
            }

            try
            {
                var request = await _service.CreateRequestAsync(
                    userId,
                    dto.ProviderType,
                    dto.Skill,
                    dto.Governorate,
                    dto.City,
                    dto.WorkerType,
                    dto.PayRate,
                    dto.Days);

                return Ok(new { message = "تم إنشاء طلب العمل بنجاح ونشره في محافظتك", data = request });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all job requests created by the authenticated user.
        /// GET /api/v1/Request/my?pageNumber=1&pageSize=20
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyRequests(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = Constants.PAGE_SIZE)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var requests = await _service.GetMyRequestsAsync(userId, pageNumber, pageSize);
                return Ok(new { data = requests, pageNumber, pageSize, count = requests.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Get paginated interested service providers for a job request created by the current user.
        /// GET /api/v1/Request/{id}/interested?pageNumber=1&pageSize=20
        /// </summary>
        [HttpGet("{id:int}/interested")]
        public async Task<IActionResult> GetInterestedProviders(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = Constants.PAGE_SIZE)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var result = await _service.GetInterestedProvidersAsync(id, userId, pageNumber, pageSize);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all job requests created by other users (Other Requests tab),
        /// showing whether the current user is interested.
        /// GET /api/v1/Request/others?pageNumber=1&pageSize=20&governorate=...
        /// </summary>
        [HttpGet("others")]
        public async Task<IActionResult> GetOtherRequests(
            [FromQuery] string? governorate = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = Constants.PAGE_SIZE)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var requests = await _service.GetOtherRequestsAsync(userId, governorate, pageNumber, pageSize);
                return Ok(new { data = requests, pageNumber, pageSize, count = requests.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Set interest (interested / not interested) on another user's job request.
        /// PUT /api/v1/Request/{id}/interest
        /// </summary>
        [HttpPut("{id:int}/interest")]
        public async Task<IActionResult> SetInterest(int id, [FromBody] SetInterestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var interest = await _service.SetInterestAsync(id, userId, dto.IsInterested);
                var message = dto.IsInterested ? "تم تسجيل اهتمامك بطلب العمل بنجاح" : "تم إلغاء اهتمامك بطلب العمل";
                return Ok(new { message, data = interest });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Remove/Delete a job request.
        /// DELETE /api/v1/Request/{id}
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                await _service.DeleteRequestAsync(id, userId);
                return Ok(new { message = "تم حذف طلب العمل بنجاح" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Cancel a job request.
        /// PUT /api/v1/Request/{id}/cancel
        /// </summary>
        [HttpPut("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var request = await _service.CancelRequestAsync(id, userId);
                return Ok(new { message = "تم إلغاء طلب العمل بنجاح", data = request });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Complete a job request.
        /// PUT /api/v1/Request/{id}/complete
        /// </summary>
        [HttpPut("{id:int}/complete")]
        public async Task<IActionResult> Complete(int id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var request = await _service.CompleteRequestAsync(id, userId);
                return Ok(new { message = "تم اكتمال طلب العمل بنجاح", data = request });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }
    }
}
namespace EgyptOnline.Dtos.JobRequest
{
    public class CreateJobRequestDto
    {
        [Required(ErrorMessage = "نوع مقدم الخدمة مطلوب")]
        [MaxLength(50)]
        public string ProviderType { get; set; } = "Worker"; // Worker, Contractor, Engineer, Company, etc.

        [Required(ErrorMessage = "المهارة/التخصص مطلوب")]
        [MaxLength(100)]
        public string Skill { get; set; } = string.Empty;

        [Required(ErrorMessage = "المحافظة مطلوبة")]
        [MaxLength(100)]
        public string Governorate { get; set; } = string.Empty;

        [Required(ErrorMessage = "المدينة مطلوبة")]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        public WorkerTypes? WorkerType { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "الأجر اليومي أو أجر المشروع يجب أن يكون أكبر من صفر")]
        public decimal PayRate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "عدد الأيام يجب أن يكون أكبر من صفر")]
        public int? Days { get; set; }
    }

    public class SetInterestDto
    {
        [Required]
        public bool IsInterested { get; set; }
    }
}
