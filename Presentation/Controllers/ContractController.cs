using EgyptOnline.Application.Services.Contract;
using EgyptOnline.Dtos.Contract;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize(Roles = Roles.User)]
    public class ContractController : ControllerBase
    {
        private readonly ContractService _contractService;

        public ContractController(ContractService contractService)
        {
            _contractService = contractService;
        }

        private string? GetUsername() => User.Identity?.Name;

        /// <summary>
        /// Returns true when the logged-in username is one of the three contract parties.
        /// </summary>
        private static bool IsPartyToContract(Models.Contract contract, string username)
            => contract.ContractorUsername == username ||
               contract.EngineerUsername   == username ||
               contract.WorkerUsername     == username;


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateContractDto dto)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                // The logged-in user must be one of the named parties (contractor, engineer, or worker).
                // This prevents a user from creating a contract between entirely unrelated parties.
                if (dto.ContractorUsername != username &&
                    dto.EngineerUsername   != username &&
                    dto.WorkerUsername     != username)
                    return StatusCode(403, new { message = "يجب أن تكون أحد أطراف العقد لإنشائه" });

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var contract = await _contractService.CreateContractAsync(dto, username);
                return Ok(new { message = "تم انشاء العقد", data = contract });
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                var contract = await _contractService.GetByIdAsync(id);
                if (contract == null) return NotFound(new { message = "العقد غير موجود" });

                // Only parties to the contract may view its details
                if (!IsPartyToContract(contract, username))
                    return StatusCode(403, new { message = "ليس لديك صلاحية لعرض هذا العقد" });

                return Ok(new { data = contract });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/v1/contract/my
        /// Returns all contracts where the logged-in user is contractor, engineer, or worker.
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyContracts()
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                var contracts = await _contractService.GetMyContractsAsync(username);
                return Ok(new { data = contracts, count = contracts.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpPut("{id}/sign")]
        public async Task<IActionResult> Sign(int id, [FromBody] ContractSignDto dto)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                var contract = await _contractService.SignContractAsync(id, username, dto.Accepted);
                return Ok(new { message = "تم التوقيع", data = contract });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
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

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                var contract = await _contractService.CancelContractAsync(id, username);
                return Ok(new { message = "تم الغاء العقد", data = contract });
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

        [HttpPost("{id}/attendance")]
        public async Task<IActionResult> MarkAttendance(int id, [FromBody] MarkAttendanceDto dto)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var contract = await _contractService.MarkAttendanceAsync(id, username, dto.Status);
                var statusMsg = dto.Status == "attended" ? "تم تاكيد الحضور" : "تم تسجيل الغياب";
                return Ok(new { message = statusMsg, data = contract });
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

        [HttpPost("{id}/disburse")]
        public async Task<IActionResult> Disburse(int id, [FromBody] DisburseInstallmentDto dto)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                var contract = await _contractService.DisburseInstallmentAsync(id, username, dto.InstallmentIndex);
                return Ok(new { message = "تم صرف القسط", data = contract });
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

        [HttpGet("{id}/installments")]
        public async Task<IActionResult> GetInstallments(int id)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                // Verify caller is a party to the contract before exposing financial installment data
                var contract = await _contractService.GetByIdAsync(id);
                if (contract == null) return NotFound(new { message = "العقد غير موجود" });
                if (!IsPartyToContract(contract, username))
                    return StatusCode(403, new { message = "ليس لديك صلاحية لعرض أقساط هذا العقد" });

                var installments = await _contractService.GetInstallmentsAsync(id);
                return Ok(new { data = installments });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpPost("{id}/confirm-arrival")]
        public async Task<IActionResult> ConfirmArrival(int id, [FromBody] ConfirmArrivalDto dto)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                var contract = await _contractService.ConfirmArrivalAsync(id, username);
                return Ok(new { message = "تم تأكيد حضور العامل", data = contract });
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

        [HttpPost("{id}/apply-penalty")]
        public async Task<IActionResult> ApplyPenalty(int id, [FromBody] ApplyPenaltyDto dto)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                var contract = await _contractService.ApplyPenaltyAsync(id, username);
                return Ok(new { message = "تم تطبيق الشرط الجزائي", data = contract });
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
    }
}
