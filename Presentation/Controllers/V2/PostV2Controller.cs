using EgyptOnline.Application.Services.Post;
using EgyptOnline.Dtos;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Presentation.Controllers.V2
{
    /// <summary>
    /// RESTful V2 API Controller for Posts
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/posts")]
    [ApiVersion("2.0")]
    [Authorize(Roles = Roles.User)]
    public class PostV2Controller : ControllerBase
    {
        private readonly PostService _postService;

        public PostV2Controller(PostService postService)
        {
            _postService = postService;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        /// <summary>
        /// Create a new post
        /// POST /api/v2/posts
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
                return CreatedAtAction(nameof(GetPostById), new { id = result.Id }, result);
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
        /// Get all posts with author profile and ratings (Paginated)
        /// GET /api/v2/posts?pageNumber=1&pageSize=15
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllPosts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 15)
        {
            try
            {
                var result = await _postService.GetAllPostsAsync(pageNumber, pageSize);
                return Ok(new { data = result, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPostById(int id)
        {
            try
            {
                var result = await _postService.GetPostByIdAsync(id);
                if (result == null)
                {
                    return NotFound(new { message = $"المنشور بالمعرف {id} غير موجود", errorCode = "POST_NOT_FOUND" });
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });
            }

            try
            {
                var success = await _postService.DeletePostAsync(id, userId);
                if (!success)
                {
                    return NotFound(new { message = $"المنشور بالمعرف {id} غير موجود", errorCode = "POST_NOT_FOUND" });
                }
                return Ok(new { message = "تم حذف المنشور بنجاح" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message, errorCode = "FORBIDDEN" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpGet("/api/v2/users/me/posts")]
        [HttpGet("me")]
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
    }
}
