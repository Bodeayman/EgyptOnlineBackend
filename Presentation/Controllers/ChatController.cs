using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EgyptOnline.Services;
using EgyptOnline.Utilities;

namespace EgyptOnline.Presentation.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(Roles = Roles.User)]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly PresenceService _presenceService;

        public ChatController(ChatService chatService, PresenceService presenceService)
        {
            _chatService = chatService;
            _presenceService = presenceService;
        }

        [HttpGet("status/{userId}")]
        public async Task<IActionResult> GetStatus(string userId)
        {
            var isOnline = await _presenceService.IsUserOnline(userId);
            return Ok(new { userId, status = isOnline ? "Online" : "Offline" });
        }

        [HttpGet("online-users")]
        public async Task<IActionResult> GetOnlineUsers()
        {
            var users = await _presenceService.GetOnlineUsers();
            return Ok(users);
        }

        [HttpGet("history/{targetUserId}")]
        public async Task<IActionResult> GetHistory(string targetUserId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            var currentUserId = User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var messages = await _chatService.GetConversationAsync(currentUserId, targetUserId, pageNumber, pageSize);
            return Ok(messages);
        }
    }
}
