namespace EgyptOnline.Dtos
{
    public class PaymentCallbackDto
    {
        public string? OrderId { get; set; } = Guid.NewGuid().ToString(); // The normal user
        public decimal? AmountCents { get; set; } = 50; // The normal user

        public string? currency { get; set; } = "EGP"; // The normal user

    }
}