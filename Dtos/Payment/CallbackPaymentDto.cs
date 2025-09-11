namespace EgyptOnline.Dtos
{
    public class PaymentCallbackDto
    {
        public required string OrderId { get; set; } = Guid.NewGuid().ToString(); // The normal user
        public required decimal AmountCents { get; set; } = 50; // The normal user

        public required string currency { get; set; } = "EGP"; // The normal user

    }
}