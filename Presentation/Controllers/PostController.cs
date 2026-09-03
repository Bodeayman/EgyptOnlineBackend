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
                    success = false,
                    message = "Validation failed",
                    errorCode = "InvalidInput",
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
                return Unauthorized(new { message = "User ID not found in token", errorCode = "Unauthorized" });
            }

            try
            {
                var result = await _postService.CreatePostAsync(userId, dto, photos);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message, errorCode = "InvalidInput" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the post", error = ex.Message });
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
                return Unauthorized(new { message = "User ID not found in token", errorCode = "Unauthorized" });
            }

            try
            {
                var result = await _postService.GetMyPostsAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving posts", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all posts from all users with author profile information
        /// GET /api/v1/Post/all
        /// Query params: pageNumber (default 1), pageSize (default 15)
        /// </summary>
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
                return StatusCode(500, new { message = "An error occurred while retrieving posts", error = ex.Message });
            }
        }
    }
}
