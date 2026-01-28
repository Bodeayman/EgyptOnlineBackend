using EgyptOnline.Data;
using EgyptOnline.Models;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ UPDATED: Added senderId and senderName parameters
        public async Task SendNotificationToUser(
            string userId,
            string title,
            string body,
            string senderId = null,
            string senderName = null)
        {
            var user = await _context.Users.Include(u => u.FirebaseTokens)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || !user.FirebaseTokens.Any())
                return;

            foreach (var token in user.FirebaseTokens)
            {
                // ✅ UPDATED: Build data payload
                var data = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(senderId))
                {
                    data["senderId"] = senderId;
                }

                if (!string.IsNullOrEmpty(senderName))
                {
                    data["senderName"] = senderName;
                }

                var message = new Message()
                {
                    Token = token.Token,
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = data  // ✅ ADDED: Include data payload
                };

                try
                {
                    string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                    Console.WriteLine($"Sent to {token.Token}: {response}");
                }
                catch (FirebaseMessagingException ex)
                {
                    Console.WriteLine($"Failed to send to {token.Token}: {ex.Message}");
                }
            }
        }
    }
}