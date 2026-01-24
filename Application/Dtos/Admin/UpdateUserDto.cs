namespace EgyptOnline.Dtos
{
    public class UpdateUserDto
    {
        public string? PhoneNumber { get; set; }
        public int? Points { get; set; }
        public bool? IsAvailable { get; set; }
        public string? ProviderType { get; set; }
        public string? Email { get; set; }
        public DateTime? SubscriptionStartDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
    }
}