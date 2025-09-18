namespace EgyptOnline.Dtos
{
    public class PaymentCallbackDto
    {

        public decimal? AmountCents { get; set; } = 50;
        public string PaymentMethod { get; set; } = "CreditCard";


    }
}