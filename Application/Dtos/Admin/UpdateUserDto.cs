namespace EgyptOnline.Dtos
{
    public class UpdateUserDto
    {
        public string? PhoneNumber { get; set; }
        public int? Points { get; set; }
        public bool? IsAvailable { get; set; }
        public string? ProviderType { get; set; }
        public DateOnly? SubscriptionStartDate { get; set; }
        public DateOnly? SubscriptionEndDate { get; set; }
    }
}