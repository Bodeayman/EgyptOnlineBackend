using EgyptOnline.Domain.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace EgyptOnline.Services
{
    public class NotificationMongoService
    {
        private readonly IMongoCollection<Notification> _notifications;

        public NotificationMongoService(IConfiguration config, IMongoClient mongoClient)
        {
            var database = mongoClient.GetDatabase("EgyptOnlineNotifications");
            _notifications = database.GetCollection<Notification>("Notifications");

            // Create compound indexes for efficient querying
            // Index 1: (UserId, Timestamp) for pagination
            var indexKeys1 = Builders<Notification>.IndexKeys
                .Ascending(n => n.UserId)
                .Descending(n => n.Timestamp);  // Descending for newest-first ordering

            // Index 2: (UserId, IsRead) for filtering unread notifications
            var indexKeys2 = Builders<Notification>.IndexKeys
                .Ascending(n => n.UserId)
                .Ascending(n => n.IsRead);

            _notifications.Indexes.CreateMany(new[]
            {
                new CreateIndexModel<Notification>(indexKeys1),
                new CreateIndexModel<Notification>(indexKeys2)
            });
        }

        public async Task<string> SaveNotificationAsync(string userId, string title, string body, string? senderId = null, string? senderName = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Body = body,
                IsRead = false,
                Timestamp = DateTime.UtcNow,
                SenderId = senderId,
                SenderName = senderName
            };

            await _notifications.InsertOneAsync(notification);
            return notification.Id; // Return the notification ID
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int pageNumber = 1, int pageSize = 20)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.UserId, userId);

            return await _notifications.Find(filter)
                                      .SortByDescending(n => n.Timestamp) // Newest first
                                      .Skip((pageNumber - 1) * pageSize)
                                      .Limit(pageSize)
                                      .ToListAsync();
        }

        public async Task<bool> MarkAsReadAsync(string notificationId, string userId)
        {
            try
            {
                var objectId = MongoDB.Bson.ObjectId.Parse(notificationId);
                var filter = Builders<Notification>.Filter.And(
                    Builders<Notification>.Filter.Eq(n => n.Id, objectId.ToString()),
                    Builders<Notification>.Filter.Eq(n => n.UserId, userId)
                );

                var update = Builders<Notification>.Update
                    .Set(n => n.IsRead, true);

                var result = await _notifications.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteNotificationAsync(string notificationId, string userId)
        {
            try
            {
                var objectId = MongoDB.Bson.ObjectId.Parse(notificationId);
                var filter = Builders<Notification>.Filter.And(
                    Builders<Notification>.Filter.Eq(n => n.Id, objectId.ToString()),
                    Builders<Notification>.Filter.Eq(n => n.UserId, userId)
                );

                var result = await _notifications.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<long> DeleteAllUserNotificationsAsync(string userId)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.UserId, userId);
            var result = await _notifications.DeleteManyAsync(filter);
            return result.DeletedCount;
        }

        public async Task<Notification?> GetNotificationByIdAsync(string notificationId, string userId)
        {
            try
            {
                var objectId = MongoDB.Bson.ObjectId.Parse(notificationId);
                var filter = Builders<Notification>.Filter.And(
                    Builders<Notification>.Filter.Eq(n => n.Id, objectId.ToString()),
                    Builders<Notification>.Filter.Eq(n => n.UserId, userId)
                );

                return await _notifications.Find(filter).FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }
    }
}
