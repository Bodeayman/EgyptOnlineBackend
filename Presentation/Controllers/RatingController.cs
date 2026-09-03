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
        ///   "rating": 5,
        ///   "description": "Great service!"
        /// }
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SubmitRating([FromBody] CreateRatingDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var result = await _ratingService.SubmitRatingAsync(userId, dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while submitting the rating", error = ex.Message });
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
                return StatusCode(500, new { message = "An error occurred while retrieving ratings", error = ex.Message });
            }
        }
    }
}