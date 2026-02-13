using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EgyptOnline.Domain.Models
{
    public class Notification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public required string UserId { get; set; }
        public required string Title { get; set; }
        public required string Body { get; set; }
        public bool IsRead { get; set; } = false;
        
        [BsonRepresentation(BsonType.DateTime)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Optional fields for sender information
        public string? SenderId { get; set; }
        public string? SenderName { get; set; }
    }
}
