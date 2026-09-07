using EgyptOnline.Application.Services.Complaint;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Presentation.Controllers
{
    /// <summary>
    /// Complaints endpoint — no subscription required.
    /// Any authenticated user can file a complaint on a contract they are part of.
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/complaints")]
    [ApiVersion("1.0")]
    [Authorize(Roles = $"{Roles.User},{Roles.Customer}")]
    public class ComplaintController : ControllerBase
    {
        private readonly ComplaintService _service;

        public ComplaintController(ComplaintService service)
        {
            _service = service;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        // ── USER ENDPOINTS ────────────────────────────────────────────────────

        /*
        /// <summary>
        /// File a new complaint on an active contract you are a party of.
        ///
        /// POST /api/v1/Complaint
        /// {
        ///   "contractId": 42,
        ///   "reason": "no_show",
        ///   "description": "العامل لم يحضر منذ 3 أيام"
        /// }
        ///
        /// Reason values: no_show | contract_termination | payment_dispute | misconduct | other
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> FileComplaint([FromBody] FileComplaintDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new { message = "فشل التحقق من صحة البيانات", errorCode = "INVALID_INPUT", errors });
            }

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });

            try
            {
                var complaint = await _service.FileComplaintAsync(
                    userId,
                    dto.ContractId,
                    dto.Reason,
                    dto.Description);

                return Ok(new
                {
                    message = "تم تقديم الشكوى بنجاح وهي قيد المراجعة",
                    data = new
                    {
                        complaint.Id,
                        complaint.ContractId,
                        complaint.Reason,
                        complaint.Status,
                        complaint.CreatedAt
                    }
                });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message, errorCode = "NOT_FOUND" }); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message, errorCode = "FORBIDDEN" }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message, errorCode = "INVALID_OPERATION" }); }
            catch (Exception ex) { return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" }); }
        }
        */

        /*
        [HttpGet("my")]
        public async Task<IActionResult> GetMyComplaints(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });

            try
            {
                var complaints = await _service.GetMyComplaintsAsync(userId, pageNumber, pageSize);
                return Ok(new { data = complaints, pageNumber, pageSize });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" }); }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });

            try
            {
                var complaint = await _service.GetByIdAsync(id);
                if (complaint == null) return NotFound(new { message = "الشكوى غير موجودة", errorCode = "COMPLAINT_NOT_FOUND" });

                if (complaint.ReporterUserId != userId)
                    return StatusCode(403, new { message = "ليس لديك صلاحية عرض هذه الشكوى", errorCode = "FORBIDDEN" });

                return Ok(new { data = complaint });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" }); }
        }
        */
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class FileComplaintDto
    {
        [Required]
        public int ContractId { get; set; }

        /// <summary>no_show | contract_termination | payment_dispute | misconduct | other</summary>
        [Required]
        [MaxLength(100)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "الوصف يجب أن يكون بين 10 و 2000 حرف")]
        public string Description { get; set; } = string.Empty;
    }
}
