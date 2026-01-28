using EgyptOnline.Domain.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace EgyptOnline.Services
{
    public class ChatService
    {
        private readonly IMongoCollection<ChatMessage> _messages;

        public ChatService(IConfiguration config, IMongoClient mongoClient)
        {
            var database = mongoClient.GetDatabase("EgyptOnlineChat");
            _messages = database.GetCollection<ChatMessage>("Messages");

            // Optimize: Create compound indexes for bidirectional lookups and sorted retrieval
            // Index 1: (Sender, Receiver, Timestamp)
            // Index 2: (Receiver, Sender, Timestamp)
            var indexKeys1 = Builders<ChatMessage>.IndexKeys
                .Ascending(m => m.SenderId)
                .Ascending(m => m.ReceiverId)
                .Ascending(m => m.Timestamp);  // ✅ Changed to Ascending

            var indexKeys2 = Builders<ChatMessage>.IndexKeys
                .Ascending(m => m.ReceiverId)
                .Ascending(m => m.SenderId)
                .Ascending(m => m.Timestamp);  // ✅ Changed to Ascending

            _messages.Indexes.CreateMany(new[]
            {
                new CreateIndexModel<ChatMessage>(indexKeys1),
                new CreateIndexModel<ChatMessage>(indexKeys2)
            });
        }

        public async Task<string> SaveMessageAsync(string senderId, string receiverId, string content)
        {
            var message = new ChatMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            await _messages.InsertOneAsync(message);
            return message.Id; // Return the message ID
        }

        // ✅ FIXED: Changed to SortBy (ascending) for oldest-first ordering
        public async Task<List<ChatMessage>> GetConversationAsync(string user1Id, string user2Id, int pageNumber = 1, int pageSize = 50)
        {
            var filter = Builders<ChatMessage>.Filter.Or(
                Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.SenderId, user1Id),
                    Builders<ChatMessage>.Filter.Eq(m => m.ReceiverId, user2Id)
                ),
                Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.SenderId, user2Id),
                    Builders<ChatMessage>.Filter.Eq(m => m.ReceiverId, user1Id)
                )
            );

            return await _messages.Find(filter)
                                  .SortBy(m => m.Timestamp) // ✅ Changed from SortByDescending - oldest first
                                  .Skip((pageNumber - 1) * pageSize)
                                  .Limit(pageSize)
                                  .ToListAsync();
        }

        public async Task<bool> DeleteMessageAsync(string messageId, string senderId)
        {
            try
            {
                var objectId = MongoDB.Bson.ObjectId.Parse(messageId);
                var filter = Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.Id, objectId.ToString()),
                    Builders<ChatMessage>.Filter.Eq(m => m.SenderId, senderId)
                );

                var result = await _messages.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EditMessageAsync(string messageId, string senderId, string newContent)
        {
            try
            {
                var objectId = MongoDB.Bson.ObjectId.Parse(messageId);
                var filter = Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.Id, objectId.ToString()),
                    Builders<ChatMessage>.Filter.Eq(m => m.SenderId, senderId)
                );

                var update = Builders<ChatMessage>.Update
                    .Set(m => m.Content, newContent)
                    .Set(m => m.Timestamp, DateTime.UtcNow);

                var result = await _messages.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}