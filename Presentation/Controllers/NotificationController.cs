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
    public class NotificationController : ControllerBase
    {
        private readonly NotificationMongoService _notificationService;

        public NotificationController(NotificationMongoService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get paginated notifications for the authenticated user
        /// </summary>
        [HttpGet("my-notifications")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var currentUserId = User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var notifications = await _notificationService.GetUserNotificationsAsync(currentUserId, pageNumber, pageSize);
            return Ok(notifications);
        }

        /// <summary>
        /// Mark a notification as read
        /// </summary>
        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkAsRead(string id)
        {
            var currentUserId = User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var success = await _notificationService.MarkAsReadAsync(id, currentUserId);
            if (!success)
            {
                return NotFound(new { message = "Notification not found or you don't have permission" });
            }

            var notification = await _notificationService.GetNotificationByIdAsync(id, currentUserId);
            return Ok(notification);
        }

        /// <summary>
        /// Delete a single notification
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(string id)
        {
            var currentUserId = User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var success = await _notificationService.DeleteNotificationAsync(id, currentUserId);
            if (!success)
            {
                return NotFound(new { message = "Notification not found or you don't have permission" });
            }

            return NoContent();
        }

        /// <summary>
        /// Delete all notifications for the authenticated user
        /// </summary>
        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAllNotifications()
        {
            var currentUserId = User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var deletedCount = await _notificationService.DeleteAllUserNotificationsAsync(currentUserId);
            return Ok(new { message = $"Deleted {deletedCount} notification(s)", deletedCount });
        }
    }
}
