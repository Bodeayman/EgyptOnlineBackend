using EgyptOnline.Application.Services.Contract;
using EgyptOnline.Domain.Models;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EgyptOnline.Data;
using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/contracts")]
    [ApiVersion("1.0")]
    [Authorize(Roles = Roles.User)]
    public class ContractController : ControllerBase
    {
        private readonly ContractService _contractService;
        private readonly ApplicationDbContext _context;

        public ContractController(ContractService contractService, ApplicationDbContext context)
        {
            _contractService = contractService;
            _context = context;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        /// <summary>
        /// Create a new 2-party contract (Client and ServiceProvider).
        /// POST /api/v1/contracts
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateContractDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var contract = new Contract
                {
                    ClientUserId = userId,
                    ServiceProviderPhoneNumber = dto.ServiceProviderPhoneNumber,
                    StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
                    ShiftStartTime = dto.ShiftStartTime,
                    ShiftEndTime = dto.ShiftEndTime,
                    TotalDays = dto.TotalDays,
                    TotalAmount = dto.TotalAmount,
                    PenaltyAmount = dto.PenaltyAmount,
                    Governorate = dto.Governorate,
                    City = dto.City,
                    District = dto.District,
                    DetailedAddress = dto.DetailedAddress,
                    Notes = dto.Notes,
                    RestrictedTerms = dto.RestrictedTerms
                };

                var result = await _contractService.CreateContractAsync(contract);
                return Ok(new { message = "تم إنشاء العقد بنجاح وبانتظار موافقة مقدم الخدمة", data = result });
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

        /// <summary>
        /// Provider accepts the contract.
        /// PUT /api/v1/contracts/{id}/accept
        /// </summary>
        [HttpPut("{id}/accept")]
        public async Task<IActionResult> Accept(int id)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var contract = await _contractService.ProviderAcceptContractAsync(id, userId);
                return Ok(new { message = "تم قبول العقد وتفعيله", data = contract });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Provider rejects the contract.
        /// PUT /api/v1/contracts/{id}/reject
        /// </summary>
        [HttpPut("{id}/reject")]
        public async Task<IActionResult> Reject(int id)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var contract = await _contractService.ProviderRejectContractAsync(id, userId);
                return Ok(new { message = "تم رفض العقد وإرجاع الرصيد للعميل", data = contract });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Provider registers arrival for a specific day.
        /// POST /api/v1/contracts/arrival
        /// </summary>
        [HttpPost("arrival")]
        public async Task<IActionResult> RegisterArrival([FromBody] RegisterArrivalDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var contract = await _contractService.RegisterArrivalAsync(dto.ContractId, dto.DayNumber, userId);
                return Ok(new { message = "تم تسجيل الوصول وإشعار العميل", data = contract });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Client reports dispute (provider absence).
        /// POST /api/v1/contracts/dispute
        /// </summary>
        [HttpPost("dispute")]
        public async Task<IActionResult> ReportDispute([FromBody] ReportDisputeDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var contract = await _contractService.ReportDisputeAsync(dto.ContractId, dto.DayNumber, dto.Reason);
                return Ok(new { message = "تم الإبلاغ عن الغياب وتجميد العقد وإحالته للأدمن", data = contract });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Mutual termination (both parties agree).
        /// PUT /api/v1/contracts/{id}/terminate/mutual
        /// </summary>
        [HttpPut("{id}/terminate/mutual")]
        public async Task<IActionResult> TerminateMutual(int id, [FromBody] TerminateContractDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var contract = await _contractService.MutualTerminationAsync(id, dto.Reason);
                return Ok(new { message = "تم تسجيل طلب الإنهاء الودي. الأرصدة مجمدة بانتظار مراجعة الأدمن", data = contract });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Client unilateral termination.
        /// PUT /api/v1/contracts/{id}/terminate/client
        /// </summary>
        [HttpPut("{id}/terminate/client")]
        public async Task<IActionResult> TerminateByClient(int id, [FromBody] TerminateContractDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var contract = await _contractService.ClientUnilateralTerminationAsync(id, dto.Reason);
                return Ok(new { message = "تم إنهاء العقد من قبل العميل. الأرصدة مجمدة بانتظار مراجعة الأدمن", data = contract });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Provider unilateral termination.
        /// PUT /api/v1/contracts/{id}/terminate/provider
        /// </summary>
        [HttpPut("{id}/terminate/provider")]
        public async Task<IActionResult> TerminateByProvider(int id, [FromBody] TerminateContractDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var contract = await _contractService.ProviderUnilateralTerminationAsync(id, userId, dto.Reason);
                return Ok(new { message = "تم إنهاء العقد من قبل مقدم الخدمة. الأرصدة مجمدة بانتظار مراجعة الأدمن", data = contract });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Get contracts for the logged-in user (as client or provider).
        /// GET /api/v1/contracts/my
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyContracts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var contracts = await _contractService.GetContractsByUserIdAsync(userId, null, pageNumber, pageSize);
                return Ok(new { data = contracts, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Get contract details by ID.
        /// GET /api/v1/contracts/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var contract = await _contractService.GetContractByIdAsync(id);
                if (contract == null) return NotFound(new { message = "العقد غير موجود" });

                // Only parties to the contract may view its details
                // Get current user's phone number to check against service provider phone
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                bool isParty = contract.ClientUserId == userId || (currentUser != null && currentUser.PhoneNumber == contract.ServiceProviderPhoneNumber);
                if (!isParty)
                    return StatusCode(403, new { message = "ليس لديك صلاحية لعرض هذا العقد" });

                return Ok(new { data = contract });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
    }

    // DTOs
    public class CreateContractDto
    {
        [Required]
        public string ServiceProviderPhoneNumber { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public TimeSpan ShiftStartTime { get; set; }

        [Required]
        public TimeSpan ShiftEndTime { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int TotalDays { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal PenaltyAmount { get; set; }

        [Required]
        public string Governorate { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        [Required]
        public string District { get; set; } = string.Empty;

        public string? DetailedAddress { get; set; }
        public string? Notes { get; set; }
        public string? RestrictedTerms { get; set; }
    }

    public class RegisterArrivalDto
    {
        [Required]
        public int ContractId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int DayNumber { get; set; }
    }

    public class ReportDisputeDto
    {
        [Required]
        public int ContractId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int DayNumber { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "السبب يجب أن يكون بين 5 و 500 حرف")]
        public string Reason { get; set; } = string.Empty;
    }

    public class TerminateContractDto
    {
        [Required]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "السبب يجب أن يكون بين 5 و 500 حرف")]
        public string Reason { get; set; } = string.Empty;
    }
}
