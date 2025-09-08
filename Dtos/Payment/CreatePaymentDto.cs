using EgyptOnline.Utilities;

namespace EgyptOnline.Dtos
{
    public class CreatePaymentDto
    {
        public PaymentType PaymentType { get; set; }
        public string PaymentCode { get; set; }
    }
}