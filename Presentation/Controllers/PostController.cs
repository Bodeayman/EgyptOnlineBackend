using EgyptOnline.Application.Services.Post;
using EgyptOnline.Dtos;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Presentation.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize(Roles = Roles.User)]
    public class PostController : ControllerBase
    {
        private readonly PostService _postService;

        public PostController(PostService postService)
        {
            _postService = postService;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        /// <summary>
        /// Create a new post with optional photos
        /// POST /api/v1/Post
        /// Content-Type: multipart/form-data
        /// {
        ///   "description": "Post description text",
        ///   "photos": [IFormFile array, max 4]
        /// }
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreatePost([FromForm] CreatePostDto dto, [FromForm] List<IFormFile>? photos = null)
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
                var result = await _postService.CreatePostAsync(userId, dto, photos);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message, errorCode = "INVALID_INPUT" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        /// <summary>
        /// Get posts for the authenticated user
        /// GET /api/v1/Post/my-posts
        /// </summary>
        [HttpGet("my-posts")]
        public async Task<IActionResult> GetMyPosts()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });
            }

            try
            {
                var result = await _postService.GetMyPostsAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllPosts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 15)
        {
            try
            {
                var result = await _postService.GetAllPostsAsync(pageNumber, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }
    }
}
