using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EgyptOnline.Services;
using EgyptOnline.Utilities;

namespace EgyptOnline.Presentation.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(Roles = $"{Roles.User},{Roles.Customer}")]
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
                return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });
            }

            var notifications = await _notificationService.GetUserNotificationsAsync(currentUserId, pageNumber, pageSize);
            return Ok(notifications);
        }

        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkAsRead(string id)
        {
            var currentUserId = User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });
            }

            var success = await _notificationService.MarkAsReadAsync(id, currentUserId);
            if (!success)
            {
                return NotFound(new { message = "الإشعار غير موجود أو ليس لديك صلاحية الوصول إليه", errorCode = "NOTIFICATION_NOT_FOUND" });
            }

            var notification = await _notificationService.GetNotificationByIdAsync(id, currentUserId);
            return Ok(notification);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(string id)
        {
            var currentUserId = User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });
            }

            var success = await _notificationService.DeleteNotificationAsync(id, currentUserId);
            if (!success)
            {
                return NotFound(new { message = "الإشعار غير موجود أو ليس لديك صلاحية الوصول إليه", errorCode = "NOTIFICATION_NOT_FOUND" });
            }

            return NoContent();
        }

        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAllNotifications()
        {
            var currentUserId = User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });
            }

            var deletedCount = await _notificationService.DeleteAllUserNotificationsAsync(currentUserId);
            return Ok(new { message = $"تم حذف {deletedCount} إشعار/إشعارات", deletedCount });
        }
    }
}
