using EgyptOnline.Application.Configuration;
using EgyptOnline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Presentation.Controllers
{
    /// <summary>
    /// AI chat endpoint — no subscription required.
    /// Users only need to be authenticated (valid JWT).
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize] // JWT required but no role / subscription check
    public class AiChatController : ControllerBase
    {
        private readonly GeminiService _gemini;

        public AiChatController(GeminiService gemini)
        {
            _gemini = gemini;
        }

        /// <summary>
        /// Send a message to the Ma3ak AI assistant.
        /// The assistant operates under the company guardrails configured in AiConfig.
        /// </summary>
        /// <remarks>
        /// POST /api/v1/AiChat/message
        ///
        /// Request body:
        /// {
        ///   "message": "What services does Ma3ak offer?"
        /// }
        ///
        /// Response:
        /// {
        ///   "reply": "Ma3ak connects you with skilled professionals..."
        /// }
        /// </remarks>
        [HttpPost("message")]
        public async Task<IActionResult> SendMessage(
            [FromBody] AiChatRequest request,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new { message = "فشل التحقق من صحة البيانات", errorCode = "INVALID_INPUT", errors });
            }

            try
            {
                var reply = await _gemini.ChatAsync(request.Message, ct);
                return Ok(new AiChatResponse { Reply = reply });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new
                {
                    message = "خدمة الذكاء الاصطناعي غير متاحة مؤقتاً. يرجى المحاولة لاحقاً.",
                    errorCode = "AI_SERVICE_UNAVAILABLE"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "حدث خطأ داخلي في الخادم",
                    errorCode = "INTERNAL_ERROR"
                });
            }
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class AiChatRequest
    {
        [Required]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 2000 characters.")]
        public string Message { get; set; } = string.Empty;
    }

    public class AiChatResponse
    {
        public string Reply { get; set; } = string.Empty;
    }
}
