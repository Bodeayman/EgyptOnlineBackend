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
                return CreatedAtAction(nameof(GetPostById), new { id = result.Id }, result);
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
        /// Get all posts with author profile and ratings (Paginated)
        /// GET /api/v2/posts?pageNumber=1&pageSize=15
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllPosts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 15)
        {
            try
            {
                var result = await _postService.GetAllPostsAsync(pageNumber, pageSize);
                return Ok(new
                {
                    data = result,
                    pageNumber,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving posts", error = ex.Message });
            }
        }

        /// <summary>
        /// Get a single post by ID
        /// GET /api/v2/posts/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPostById(int id)
        {
            try
            {
                var result = await _postService.GetPostByIdAsync(id);
                if (result == null)
                {
                    return NotFound(new { message = $"Post with ID {id} not found", errorCode = "NotFound" });
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the post", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete a post by ID
        /// DELETE /api/v2/posts/{id}
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User ID not found in token", errorCode = "Unauthorized" });
            }

            try
            {
                var success = await _postService.DeletePostAsync(id, userId);
                if (!success)
                {
                    return NotFound(new { message = $"Post with ID {id} not found", errorCode = "NotFound" });
                }
                return Ok(new { message = "Post deleted successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message, errorCode = "Forbidden" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the post", error = ex.Message });
            }
        }

        /// <summary>
        /// Get posts created by the authenticated user
        /// GET /api/v2/users/me/posts
        /// </summary>
        [HttpGet("/api/v2/users/me/posts")]
        [HttpGet("me")]
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
                return StatusCode(500, new { message = "An error occurred while retrieving user posts", error = ex.Message });
            }
        }
    }
}
