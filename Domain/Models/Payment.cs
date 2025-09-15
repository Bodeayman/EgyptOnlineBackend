using EgyptOnline.Utilities;

namespace EgyptOnline.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public User? User { get; set; }
        public PaymentType PaymentType { get; set; }

        public string PaymentCode { get; set; } = string.Empty;
    }
}