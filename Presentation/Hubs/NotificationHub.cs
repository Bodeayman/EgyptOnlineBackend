using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EgyptOnline.Services;

namespace EgyptOnline.Presentation.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly NotificationMongoService _notificationService;

        public NotificationHub(NotificationMongoService notificationService)
        {
            _notificationService = notificationService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst("uid")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                // User connected - could add presence tracking here if needed
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst("uid")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Send a notification to a specific user (can be called from server-side code)
        /// </summary>
        public async Task SendNotificationToUser(string userId, string title, string body, string? senderId = null, string? senderName = null)
        {
            try
            {
                // Save to MongoDB
                var notificationId = await _notificationService.SaveNotificationAsync(userId, title, body, senderId, senderName);

                // Send real-time notification to the user
                await Clients.User(userId).SendAsync("ReceiveNotification", notificationId, title, body, senderId, senderName, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                // Log error but don't throw to avoid breaking the caller
                Console.WriteLine($"Error sending notification: {ex.Message}");
            }
        }
    }
}
