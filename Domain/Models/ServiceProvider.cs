using System.Text.Json.Serialization;

namespace EgyptOnline.Models
{
    public abstract class ServicesProvider
    {

        public int Id { get; set; }
        public required string UserId { get; set; }
        [JsonIgnore]
        public User? User { get; set; } = default!;

        public bool IsAvailable { get; set; } = true;


        public string? Bio { get; set; }

        public string ProviderType { get; set; } = "Worker";

        public int ServicePricePerDay { get; set; } = 0;
    }
}