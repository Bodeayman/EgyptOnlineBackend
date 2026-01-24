namespace EgyptOnline.Utilities
{
    /// <summary>
    /// Centralized configuration for provider pricing and subscription costs.
    /// This is the single source of truth for all payment amounts and subscription points.
    /// </summary>
    public static class ProviderPricingConfig
    {
        // ==================== SUBSCRIPTION COSTS (In EGP) ====================
        public const decimal WORKER_SUBSCRIPTION_COST = 50m;
        public const decimal ASSISTANT_SUBSCRIPTION_COST = 75m;
        public const decimal CONTRACTOR_SUBSCRIPTION_COST = 100m;
        public const decimal COMPANY_SUBSCRIPTION_COST = 200m;
        public const decimal MARKETPLACE_SUBSCRIPTION_COST = 150m;
        public const decimal ENGINEER_SUBSCRIPTION_COST = 125m;

        // ==================== SUBSCRIPTION POINTS ====================
        // Points awarded when user renews subscription
        public const int WORKER_SUBSCRIPTION_POINTS = 25;
        public const int ASSISTANT_SUBSCRIPTION_POINTS = 25;
        public const int CONTRACTOR_SUBSCRIPTION_POINTS = 50;
        public const int COMPANY_SUBSCRIPTION_POINTS = 100;
        public const int MARKETPLACE_SUBSCRIPTION_POINTS = 100;
        public const int ENGINEER_SUBSCRIPTION_POINTS = 50;

        // ==================== REGISTRATION POINTS ====================
        // Points awarded when user registers as a service provider
        public const int WORKER_REGISTRATION_POINTS = 25;
        public const int ASSISTANT_REGISTRATION_POINTS = 25;
        public const int CONTRACTOR_REGISTRATION_POINTS = 50;
        public const int COMPANY_REGISTRATION_POINTS = 100;
        public const int MARKETPLACE_REGISTRATION_POINTS = 100;
        public const int ENGINEER_REGISTRATION_POINTS = 50;

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Get subscription cost based on provider type
        /// </summary>
        public static decimal GetSubscriptionCost(string providerType)
        {
            return providerType?.ToLower() switch
            {
                "worker" => WORKER_SUBSCRIPTION_COST,
                "assistant" => ASSISTANT_SUBSCRIPTION_COST,
                "contractor" => CONTRACTOR_SUBSCRIPTION_COST,
                "company" => COMPANY_SUBSCRIPTION_COST,
                "marketplace" => MARKETPLACE_SUBSCRIPTION_COST,
                "engineer" => ENGINEER_SUBSCRIPTION_COST,
                _ => WORKER_SUBSCRIPTION_COST // Default to Worker pricing
            };
        }

        /// <summary>
        /// Get subscription points based on provider type
        /// </summary>
        public static int GetSubscriptionPoints(string providerType)
        {
            return providerType?.ToLower() switch
            {
                "worker" => WORKER_SUBSCRIPTION_POINTS,
                "assistant" => ASSISTANT_SUBSCRIPTION_POINTS,
                "contractor" => CONTRACTOR_SUBSCRIPTION_POINTS,
                "company" => COMPANY_SUBSCRIPTION_POINTS,
                "marketplace" => MARKETPLACE_SUBSCRIPTION_POINTS,
                "engineer" => ENGINEER_SUBSCRIPTION_POINTS,
                _ => WORKER_SUBSCRIPTION_POINTS // Default to Worker points
            };
        }

        /// <summary>
        /// Get registration points based on provider type
        /// </summary>
        public static int GetRegistrationPoints(string providerType)
        {
            return providerType?.ToLower() switch
            {
                "worker" => WORKER_REGISTRATION_POINTS,
                "assistant" => ASSISTANT_REGISTRATION_POINTS,
                "contractor" => CONTRACTOR_REGISTRATION_POINTS,
                "company" => COMPANY_REGISTRATION_POINTS,
                "marketplace" => MARKETPLACE_REGISTRATION_POINTS,
                "engineer" => ENGINEER_REGISTRATION_POINTS,
                _ => WORKER_REGISTRATION_POINTS // Default to Worker points
            };
        }

        /// <summary>
        /// Get all pricing information for a provider type as a dictionary
        /// </summary>
        public static Dictionary<string, object> GetPricingInfo(string providerType)
        {
            return new Dictionary<string, object>
            {
                ["subscriptionCost"] = GetSubscriptionCost(providerType),
                ["subscriptionPoints"] = GetSubscriptionPoints(providerType),
                ["registrationPoints"] = GetRegistrationPoints(providerType)
            };
        }
    }
}
