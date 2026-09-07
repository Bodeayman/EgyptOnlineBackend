using EgyptOnline.Application.Services.Rating;
using EgyptOnline.Dtos;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Presentation.Controllers.V2
{
    /// <summary>
    /// RESTful V2 API Controller for Ratings
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/ratings")]
    [ApiVersion("2.0")]
    [Authorize(Roles = $"{Roles.User},{Roles.Customer}")]
    public class RatingV2Controller : ControllerBase
    {
        private readonly RatingService _ratingService;

        public RatingV2Controller(RatingService ratingService)
        {
            _ratingService = ratingService;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        /// <summary>
        /// Submit a new rating
        /// POST /api/v2/ratings
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SubmitRating([FromBody] CreateRatingDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    message = "فشل التحقق من صحة البيانات",
                    errorCode = "INVALID_INPUT",
                    errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        )
                });
            }

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });
            }

            try
            {
                var result = await _ratingService.SubmitRatingAsync(userId, dto);
                return CreatedAtAction(nameof(GetRatingById), new { id = result.Id }, result);
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
        /// Get ratings with summary statistics and pagination
        /// GET /api/v2/ratings?orderBy=latest&pageNumber=1&pageSize=20
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

        /// <summary>
        /// Get details of a single rating by ID
        /// GET /api/v2/ratings/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetRatingById(int id)
        {
            try
            {
                var result = await _ratingService.GetRatingByIdAsync(id);
                if (result == null)
                {
                    return NotFound(new { message = $"التقييم بالمعرف {id} غير موجود", errorCode = "RATING_NOT_FOUND" });
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }
    }
}
