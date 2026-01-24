namespace EgyptOnline.Dtos
{
    /// <summary>
    /// DEPRECATED: This DTO is no longer used. 
    /// Payment method is now passed as a query parameter via FromQuery.
    /// Payment amount is automatically determined from ProviderPricingConfig based on provider type.
    /// This enforces single source of truth for pricing.
    /// </summary>
    [Obsolete("Use query parameter 'paymentMethod' in /subscribe endpoint instead. Amount is determined from ProviderPricingConfig.")]
    public class PaymentCallbackDto
    {
        [Obsolete("Amount is now determined from ProviderPricingConfig")]
        public decimal? AmountCents { get; set; } = 50;

        [Obsolete("Use query parameter instead")]
        public string PaymentMethod { get; set; } = "CreditCard";
    }
}