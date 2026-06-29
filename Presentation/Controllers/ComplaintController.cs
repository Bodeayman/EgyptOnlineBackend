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
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize(Roles = Roles.User)]
    public class ComplaintController : ControllerBase
    {
        private readonly ComplaintService _service;

        public ComplaintController(ComplaintService service)
        {
            _service = service;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        // ── USER ENDPOINTS ────────────────────────────────────────────────────

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
                return BadRequest(ModelState);

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

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
            catch (KeyNotFoundException ex)       { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
            catch (InvalidOperationException ex)   { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex)                   { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
        }

        /// <summary>
        /// Get all complaints I have filed.
        /// GET /api/v1/Complaint/my?pageNumber=1&pageSize=20
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyComplaints(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var complaints = await _service.GetMyComplaintsAsync(userId, pageNumber, pageSize);
                return Ok(new { data = complaints, pageNumber, pageSize });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
        }

        /// <summary>
        /// Get a single complaint by ID.
        /// GET /api/v1/Complaint/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var complaint = await _service.GetByIdAsync(id);
                if (complaint == null) return NotFound(new { message = "الشكوى غير موجودة" });

                // Only the reporter can view their complaint (admin has its own endpoint)
                if (complaint.ReporterUserId != userId)
                    return StatusCode(403, new { message = "ليس لديك صلاحية عرض هذه الشكوى" });

                return Ok(new { data = complaint });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal server error", error = ex.Message }); }
        }
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
