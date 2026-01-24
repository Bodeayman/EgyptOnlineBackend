using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EgyptOnline.Services;
using EgyptOnline.Data;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Presentation.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChatService _chatService;
        private readonly PresenceService _presenceService;
        private readonly ApplicationDbContext _context;

        public ChatHub(ChatService chatService, PresenceService presenceService, ApplicationDbContext context)
        {
            _chatService = chatService;
            _presenceService = presenceService;
            _context = context;
        }

        /// <summary>
        /// Checks if user has active subscription from database (fresh check for critical operations)
        /// </summary>
        private async Task<bool> CheckSubscriptionAsync(string userId)
        {
            var user = await _context.Users
                .Include(u => u.Subscription)
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Id == userId);
            return user?.Subscription != null 
                && user.ServiceProvider?.IsAvailable == true;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst("uid")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                var isFirstConnection = await _presenceService.UserConnected(userId, Context.ConnectionId);
                if (isFirstConnection)
                {
                    await Clients.Others.SendAsync("UserStatusChanged", userId, "Online");
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst("uid")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                var isLastConnection = await _presenceService.UserDisconnected(userId, Context.ConnectionId);
                if (isLastConnection)
                {
                    await Clients.Others.SendAsync("UserStatusChanged", userId, "Offline");
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string receiverId, string content)
        {
            var senderId = Context.User?.FindFirst("uid")?.Value;
            
            if (string.IsNullOrEmpty(senderId))
            {
                await Clients.Caller.SendAsync("SendMessageError", "Unauthorized");
                return;
            }

            // Critical operation: Check subscription from DB
            if (!await CheckSubscriptionAsync(senderId))
            {
                await Clients.Caller.SendAsync("SendMessageError", "Your subscription has expired. Please renew to send messages.");
                return;
            }

            // Save to MongoDB
            await _chatService.SaveMessageAsync(senderId, receiverId, content);

            // Send to receiver (assuming receiver is connected to their own user-id group)
            await Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, content);
            
            // Also send back to sender so their UI updates immediately (or they do it optimistically)
            // await Clients.Caller.SendAsync("ReceiveMessage", senderId, content);
        }

        public async Task DeleteMessage(string messageId, string receiverId)
        {
            var senderId = Context.User?.FindFirst("uid")?.Value;
            
            if (string.IsNullOrEmpty(senderId))
            {
                await Clients.Caller.SendAsync("DeleteMessageError", "Unauthorized");
                return;
            }

            // Critical operation: Check subscription from DB
            if (!await CheckSubscriptionAsync(senderId))
            {
                await Clients.Caller.SendAsync("DeleteMessageError", "Your subscription has expired. Please renew to delete messages.");
                return;
            }

            try
            {
                // Delete from MongoDB - only message owner can delete
                var deleted = await _chatService.DeleteMessageAsync(messageId, senderId);
                
                if (!deleted)
                {
                    await Clients.Caller.SendAsync("DeleteMessageError", "Message not found or you don't have permission");
                    return;
                }

                // Notify receiver that message was deleted
                await Clients.User(receiverId).SendAsync("MessageDeleted", messageId);
                
                // Confirm deletion to sender
                await Clients.Caller.SendAsync("MessageDeleted", messageId);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("DeleteMessageError", $"Error deleting message: {ex.Message}");
            }
        }

        public async Task EditMessage(string messageId, string newContent, string receiverId)
        {
            var senderId = Context.User?.FindFirst("uid")?.Value;
            
            if (string.IsNullOrEmpty(senderId))
            {
                await Clients.Caller.SendAsync("EditMessageError", "Unauthorized");
                return;
            }

            // Critical operation: Check subscription from DB
            if (!await CheckSubscriptionAsync(senderId))
            {
                await Clients.Caller.SendAsync("EditMessageError", "Your subscription has expired. Please renew to edit messages.");
                return;
            }

            try
            {
                // Update message in MongoDB - only message owner can edit
                var updated = await _chatService.EditMessageAsync(messageId, senderId, newContent);
                
                if (!updated)
                {
                    await Clients.Caller.SendAsync("EditMessageError", "Message not found or you don't have permission");
                    return;
                }

                // Notify receiver that message was edited
                await Clients.User(receiverId).SendAsync("MessageEdited", messageId, newContent);
                
                // Confirm edit to sender
                await Clients.Caller.SendAsync("MessageEdited", messageId, newContent);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("EditMessageError", $"Error editing message: {ex.Message}");
            }
        }
    }
}
