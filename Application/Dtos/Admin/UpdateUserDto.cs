using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EgyptOnline.Utilities;

namespace EgyptOnline.Dtos
{
    public class UpdateUserDto
    {
        public string? PhoneNumber { get; set; }
        public int? Points { get; set; }
        public int? SubscriptionPoints { get; set; }

        public bool? IsAvailable { get; set; }
        public string? ProviderType { get; set; }
        public string? Email { get; set; }

        /// <summary>
        /// Expected format: "yyyy-MM-ddTHH:mm:ssZ" (ISO 8601), e.g. "2026-03-11T00:00:00Z"
        /// </summary>
        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? SubscriptionStartDate { get; set; }

        /// <summary>
        /// Expected format: "yyyy-MM-ddTHH:mm:ssZ" (ISO 8601), e.g. "2026-12-31T00:00:00Z"
        /// </summary>
        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? SubscriptionEndDate { get; set; }
    }
}