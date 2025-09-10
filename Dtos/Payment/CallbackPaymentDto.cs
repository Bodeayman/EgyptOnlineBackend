namespace EgyptOnline.Dtos
{
    public class PaymentCallbackDto
    {
        public string? PaymentId { get; set; }
        public string? OrderId { get; set; }
        public decimal? AmountCents { get; set; } = 0;
        public string? Success { get; set; }
        public string? Currency { get; set; }
        public string? CreatedAt { get; set; }
        public string? AuthToken { get; set; }
    }
}