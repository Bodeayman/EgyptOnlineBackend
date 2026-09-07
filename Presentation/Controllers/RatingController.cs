using EgyptOnline.Application.Services.Rating;
using EgyptOnline.Dtos;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Presentation.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize(Roles = $"{Roles.User},{Roles.Customer}")]
    public class RatingController : ControllerBase
    {
        private readonly RatingService _ratingService;

        public RatingController(RatingService ratingService)
        {
            _ratingService = ratingService;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        /// <summary>
        /// Submit a new rating
        /// POST /api/v1/Rating
        /// {
        ///   "targetUserId": "string",
        ///   "rating": 5,
        ///   "description": "Great service!"
        /// }
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SubmitRating([FromBody] CreateRatingDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new { message = "فشل التحقق من صحة البيانات", errorCode = "INVALID_INPUT", errors });
            }

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });
            }

            try
            {
                var result = await _ratingService.SubmitRatingAsync(userId, dto);
                return Ok(result);
            }
            catch (ArgumentException ex) when (ex.ParamName == nameof(dto.TargetUserId))
            {
                return BadRequest(new { message = "المستخدم المستهدف غير موجود", errorCode = "USER_NOT_FOUND" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        /// <summary>
        /// Get ratings with optional ordering and pagination
        /// GET /api/v1/Rating?orderBy=latest or orderBy=best&pageNumber=1&pageSize=20
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRatings([FromQuery] string? orderBy = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _ratingService.GetRatingsAsync(orderBy, pageNumber, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }
    }
}