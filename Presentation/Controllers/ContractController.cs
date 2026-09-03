using EgyptOnline.Application.Services.Contract;
using EgyptOnline.Domain.Models;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EgyptOnline.Data;
using System.ComponentModel.DataAnnotations;
using ContractModel = EgyptOnline.Models.Contract;
using ContractDayModel = EgyptOnline.Domain.Models.ContractDay;

namespace EgyptOnline.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/contracts")]
    [ApiVersion("1.0")]
    [Authorize(Roles = $"{Roles.User},{Roles.Customer}")]
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
                    return BadRequest(new { message = "فشل التحقق من صحة البيانات", errors = ModelState });

                // Set default values for Egypt time (UTC+2)
                var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
                var nowInEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptTimeZone);
                var tomorrowInEgypt = nowInEgypt.Date.AddDays(1);

                // Use provided values or defaults
                DateTime startDate;
                if (dto.StartDate.HasValue && dto.StartDate.Value != default(DateTime))
                {
                    // Handle the DateTime based on its Kind property
                    if (dto.StartDate.Value.Kind == DateTimeKind.Utc)
                    {
                        startDate = dto.StartDate.Value;
                    }
                    else if (dto.StartDate.Value.Kind == DateTimeKind.Local)
                    {
                        startDate = TimeZoneInfo.ConvertTimeToUtc(dto.StartDate.Value);
                    }
                    else
                    {
                        // Unspecified - assume it's Egypt local time
                        startDate = TimeZoneInfo.ConvertTimeToUtc(dto.StartDate.Value, egyptTimeZone);
                    }
                }
                else
                {
                    startDate = TimeZoneInfo.ConvertTimeToUtc(tomorrowInEgypt, egyptTimeZone);
                }

                // Support explicit SelectedDates list for PerDay and Batch contract types
                List<DateTime>? selectedDates = null;
                if (dto.SelectedDates != null && dto.SelectedDates.Count > 0)
                {
                    selectedDates = dto.SelectedDates
                        .Select(d => d.Kind == DateTimeKind.Utc ? d.Date : TimeZoneInfo.ConvertTimeToUtc(d, egyptTimeZone).Date)
                        .OrderBy(d => d)
                        .Distinct()
                        .ToList();

                    startDate = selectedDates.First();
                }

                var totalDays = selectedDates != null ? selectedDates.Count : dto.TotalDays;

                var shiftStartTime = dto.ShiftStartTime.HasValue && dto.ShiftStartTime.Value != TimeSpan.Zero
                    ? dto.ShiftStartTime.Value
                    : TimeSpan.FromHours(5); // 5 AM default

                var shiftEndTime = dto.ShiftEndTime.HasValue && dto.ShiftEndTime.Value != TimeSpan.Zero
                    ? dto.ShiftEndTime.Value
                    : TimeSpan.FromHours(22); // 10 PM default

                var contract = new Contract
                {
                    ClientUserId = userId,
                    ServiceProviderPhoneNumber = dto.ServiceProviderPhoneNumber,
                    StartDate = startDate,
                    ShiftStartTime = shiftStartTime,
                    ShiftEndTime = shiftEndTime,
                    TotalDays = totalDays,
                    DailySalary = (int)dto.DailySalary,
                    TotalAmount = (int)(dto.DailySalary * totalDays),
                    PenaltyAmount = (int)dto.PenaltyAmount,
                    ContractType = dto.ContractType,
                    Governorate = dto.Governorate,
                    City = dto.City,
                    District = dto.District,
                    DetailedAddress = dto.DetailedAddress,
                    Notes = dto.Notes,
                    RestrictedTerms = dto.RestrictedTerms
                };

                // Convert DTO contract days to domain entities for Batch contracts
                List<ContractDayModel>? contractDays = null;
                if (dto.ContractDays != null && dto.ContractDays.Count > 0)
                {
                    contractDays = dto.ContractDays.Select(d => new ContractDayModel
                    {
                        DayNumber = d.DayNumber,
                        Date = d.Date.Kind == DateTimeKind.Utc ? d.Date : TimeZoneInfo.ConvertTimeToUtc(d.Date, egyptTimeZone),
                        BatchAmount = d.BatchAmount
                    }).ToList();
                }

                var result = await _contractService.CreateContractAsync(contract, selectedDates, contractDays);
                return Ok(new { message = "تم إنشاء العقد بنجاح وبانتظار موافقة مقدم الخدمة", data = result });
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
        /// Provider accepts the contract.
        /// PUT /api/v1/contracts/{id}/accept
        /// </summary>
        [HttpPut("{id}/accept")]
        [Authorize(Roles = Roles.User)]
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
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Provider rejects the contract.
        /// PUT /api/v1/contracts/{id}/reject
        /// </summary>
        [HttpPut("{id}/reject")]
        [Authorize(Roles = Roles.User)]
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
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Provider registers arrival for a specific day.
        /// POST /api/v1/contracts/arrival
        /// </summary>
        [HttpPost("arrival")]
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> RegisterArrival([FromBody] RegisterArrivalDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "فشل التحقق من صحة البيانات", errors = ModelState });

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
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Client confirms provider's attendance/arrival for a specific day.
        /// POST /api/v1/contracts/confirm-attendance
        /// </summary>
        [HttpPost("confirm-attendance")]
        public async Task<IActionResult> ConfirmAttendance([FromBody] ConfirmAttendanceDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "فشل التحقق من صحة البيانات", errors = ModelState });

                var contractDay = await _contractService.ClientConfirmAttendanceAsync(dto.ContractId, dto.DayNumber, userId);
                return Ok(new { message = "تم تأكيد الحضور لليوم بنجاح وسيتم الصرف عند نهاية الشيفت اليومي", data = contractDay });
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
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Client or provider reports a dispute.
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
                    return BadRequest(new { message = "فشل التحقق من صحة البيانات", errors = ModelState });

                var contract = await _contractService.ReportDisputeAsync(dto.ContractId, dto.DayNumber, dto.Reason, userId);
                return Ok(new { message = "تم الإبلاغ عن المشكلة وتجميد العقد وإحالته للأدمن", data = contract });
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
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Get contracts for the logged-in user (as client or provider).
        /// GET /api/v1/contracts/my?include=Days
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyContracts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? include = null)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var includeDays = include?.Contains("Days", StringComparison.OrdinalIgnoreCase) == true;
                var contracts = await _contractService.GetContractsByUserIdAsync(userId, null, pageNumber, pageSize, includeDays);
                return Ok(new { data = contracts, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }

        /// <summary>
        /// Get contract details by ID.
        /// GET /api/v1/contracts/{id}?include=Days
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id, [FromQuery] string? include = null)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var includeDays = include?.Contains("Days", StringComparison.OrdinalIgnoreCase) == true;
                var contract = await _contractService.GetContractByIdAsync(id, userId, includeDays);
                if (contract == null) return NotFound(new { message = "العقد غير موجود" });

                return Ok(new { data = contract });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في الخادم الداخلي", error = ex.Message });
            }
        }
    }

    // DTOs
    public class CreateContractDto
    {
        [Required(ErrorMessage = "رقم موبايل مقدم الخدمة مطلوب")]
        public string ServiceProviderPhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "عدد الأيام مطلوب")]
        [Range(1, int.MaxValue, ErrorMessage = "عدد الأيام يجب أن يكون 1 على الأقل")]
        public int TotalDays { get; set; }

        [Required(ErrorMessage = "الأجر اليومي مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "الأجر اليومي يجب أن يكون أكبر من صفر")]
        public decimal DailySalary { get; set; }

        [Required(ErrorMessage = "مبلغ الشرط الجزائي مطلوب")]
        [Range(0.0, double.MaxValue, ErrorMessage = "الشرط الجزائي يجب أن يكون 0 أو أكبر")]
        public decimal PenaltyAmount { get; set; }

        [Required(ErrorMessage = "المحافظة مطلوبة")]
        public string Governorate { get; set; } = string.Empty;

        [Required(ErrorMessage = "المدينة مطلوبة")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "الحي مطلوب")]
        public string District { get; set; } = string.Empty;

        public string? DetailedAddress { get; set; }
        public string? Notes { get; set; }
        public string? RestrictedTerms { get; set; }

        public ContractType ContractType { get; set; } = ContractType.PerDay;
        public List<DateTime>? SelectedDates { get; set; }
        public List<ContractDayDto>? ContractDays { get; set; }

        public DateTime? StartDate { get; set; }
        public TimeSpan? ShiftStartTime { get; set; }
        public TimeSpan? ShiftEndTime { get; set; }
    }

    public class ContractDayDto
    {
        [Required(ErrorMessage = "رقم اليوم مطلوب")]
        [Range(1, int.MaxValue, ErrorMessage = "رقم اليوم يجب أن يكون 1 على الأقل")]
        public int DayNumber { get; set; }

        [Required(ErrorMessage = "تاريخ اليوم مطلوب")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "مبلغ الدفعة مطلوب")]
        [Range(1, int.MaxValue, ErrorMessage = "مبلغ الدفعة يجب أن يكون أكبر من صفر")]
        public int BatchAmount { get; set; }
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

    public class ConfirmAttendanceDto
    {
        [Required]
        public int ContractId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int DayNumber { get; set; }
    }
}
