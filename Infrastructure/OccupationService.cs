using Microsoft.Extensions.Caching.Distributed;
using System.Text;

namespace EgyptOnline.Services
{
    public class OccupationService
    {
        private readonly IDistributedCache _cache;
        private const string OCCUPIED_KEY_PREFIX = "occupied:";

        public OccupationService(IDistributedCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Calculates TTL (Time To Live) until midnight Cairo time (UTC+2)
        /// </summary>
        private TimeSpan CalculateTTLUntilMidnight()
        {
            // Current time in Cairo (UTC+2)
            var cairoTime = DateTime.UtcNow.AddHours(2);
            
            // Next midnight in Cairo time
            var midnight = cairoTime.Date.AddDays(1);
            
            // Calculate time remaining until midnight
            var ttl = midnight - cairoTime;
            
            return ttl;
        }

        /// <summary>
        /// Marks a user as occupied in Redis with TTL until midnight
        /// </summary>
        public async Task<DateTime> SetUserOccupiedAsync(string userId)
        {
            var key = $"{OCCUPIED_KEY_PREFIX}{userId}";
            var ttl = CalculateTTLUntilMidnight();
            
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };

            // Store a simple value (we only care about key existence)
            await _cache.SetStringAsync(key, "1", options);

            // Return the expiration time for client information
            var cairoTime = DateTime.UtcNow.AddHours(2);
            return cairoTime.Date.AddDays(1); // Midnight
        }

        /// <summary>
        /// Removes occupation status for a user
        /// </summary>
        public async Task RemoveUserOccupiedAsync(string userId)
        {
            var key = $"{OCCUPIED_KEY_PREFIX}{userId}";
            await _cache.RemoveAsync(key);
        }

        /// <summary>
        /// Checks if a single user is occupied
        /// </summary>
        public async Task<bool> IsUserOccupiedAsync(string userId)
        {
            var key = $"{OCCUPIED_KEY_PREFIX}{userId}";
            var value = await _cache.GetStringAsync(key);
            return value != null;
        }

        /// <summary>
        /// Batch check multiple users for occupation status
        /// Returns a HashSet of occupied user IDs for efficient lookup
        /// </summary>
        public async Task<HashSet<string>> GetOccupiedUsersBatchAsync(List<string> userIds)
        {
            var occupiedUsers = new HashSet<string>();

            if (userIds == null || !userIds.Any())
                return occupiedUsers;

            // Check each user (IDistributedCache doesn't have native batch operations)
            // This is still efficient as Redis operations are very fast
            var tasks = userIds.Select(async userId =>
            {
                var key = $"{OCCUPIED_KEY_PREFIX}{userId}";
                var value = await _cache.GetStringAsync(key);
                return new { UserId = userId, IsOccupied = value != null };
            });

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                if (result.IsOccupied)
                {
                    occupiedUsers.Add(result.UserId);
                }
            }

            return occupiedUsers;
        }
    }
}
