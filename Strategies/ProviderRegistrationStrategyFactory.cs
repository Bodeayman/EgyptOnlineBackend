using EgyptOnline.Dtos;

namespace EgyptOnline.Strategies
{
    /// <summary>
    /// Factory for creating appropriate provider registration strategies.
    /// Uses a dictionary for O(1) lookup instead of if-else chains.
    /// </summary>
    public class ProviderRegistrationStrategyFactory
    {
        private readonly Dictionary<string, IProviderRegistrationStrategy> _strategies;

        public ProviderRegistrationStrategyFactory()
        {
            _strategies = new Dictionary<string, IProviderRegistrationStrategy>(StringComparer.CurrentCultureIgnoreCase)
            {
                { "worker", new WorkerRegistrationStrategy() },
                { "assistant", new AssistantRegistrationStrategy() },
                { "contractor", new ContractorRegistrationStrategy() },
                { "company", new CompanyRegistrationStrategy() },
                { "marketplace", new MarketPlaceRegistrationStrategy() },
                { "engineer", new EngineerRegistrationStrategy() }
            };
        }

        /// <summary>
        /// Gets the appropriate strategy for a given provider type.
        /// </summary>
        /// <param name="providerType">The type of service provider</param>
        /// <returns>The strategy instance or null if provider type is not found</returns>
        public IProviderRegistrationStrategy? GetStrategy(string? providerType)
        {
            if (string.IsNullOrEmpty(providerType))
                return null;

            _strategies.TryGetValue(providerType, out var strategy);
            return strategy;
        }
    }
}
