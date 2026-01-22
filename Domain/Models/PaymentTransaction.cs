namespace EgyptOnline.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public User User { get; set; }
        
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EGP";
        
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public string PaymentMethod { get; set; } // CreditCard, MobileWallet, Fawry
        
        // Paymob integration
        public int? PaymobOrderId { get; set; }
        public string? PaymobTransactionId { get; set; }
        public string? PaymobMerchantOrderId { get; set; }
        
        // Webhook data
        public string? PaymentGatewayResponse { get; set; }
        public string? ErrorMessage { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // For idempotency
        public string? IdempotencyKey { get; set; }
    }

    public enum PaymentStatus
    {
        Pending,      // Awaiting payment
        Processing,   // Webhook received, processing
        Success,      // Subscription renewed
        Failed,       // Payment declined
        Refunded      // Money returned
    }
}
