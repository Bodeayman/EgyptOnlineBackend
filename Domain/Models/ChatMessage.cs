using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EgyptOnline.Domain.Models
{
    public class ChatMessage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public required string SenderId { get; set; }
        public required string ReceiverId { get; set; }
        public required string Content { get; set; } = string.Empty;
        
        [BsonRepresentation(BsonType.DateTime)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
