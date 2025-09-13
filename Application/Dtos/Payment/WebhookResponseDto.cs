using System.Text.Json.Serialization;

namespace EgyptOnline.Dtos
{
    public class WebhookResponseDto
    {
        public string Message { get; set; }
        public string OrderId { get; set; }
    }

    public class PaymobWebhookDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("order")]
        public OrderData Order { get; set; }

        [JsonPropertyName("data")]
        public DataMessage Data { get; set; }

        // Add other fields if needed
    }

    public class OrderData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class DataMessage
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

}