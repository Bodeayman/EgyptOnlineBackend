using System.Security.Claims;

namespace EgyptOnline.Utilities
{
    /// <summary>
    /// Helper methods for checking subscription status from JWT token claims.
    /// Use this for non-critical operations. For critical operations, use RequireSubscription attribute.
    /// </summary>
    public static class SubscriptionHelper
    {
        /// <summary>
        /// Checks if subscription is expired based on token claim (may be stale up to ACCESS_TOKEN_MINS).
        /// Returns true if subscription is active, false if expired or not found.
        /// </summary>
        public static bool IsSubscriptionActiveFromToken(ClaimsPrincipal user)
        {
            var expiryDateStr = user.FindFirst("subscription_expires")?.Value;
            if (string.IsNullOrEmpty(expiryDateStr))
                return false;

            if (DateOnly.TryParse(expiryDateStr, out var expiryDate))
            {
                return expiryDate > DateOnly.FromDateTime(DateTime.UtcNow);
            }

            return false;
        }

        /// <summary>
        /// Gets subscription expiry date from token claim.
        /// Returns null if not found or invalid.
        /// </summary>
        public static DateOnly? GetSubscriptionExpiryFromToken(ClaimsPrincipal user)
        {
            var expiryDateStr = user.FindFirst("subscription_expires")?.Value;
            if (string.IsNullOrEmpty(expiryDateStr))
                return null;

            if (DateOnly.TryParse(expiryDateStr, out var expiryDate))
            {
                return expiryDate;
            }

            return null;
        }
    }
}

