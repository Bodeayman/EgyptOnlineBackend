using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EgyptOnline.Presentation.Hubs
{
    // SignalR uses a user identifier to route messages when calling Clients.User(id).
    // By default it uses ClaimTypes.NameIdentifier. This implementation makes
    // SignalR use the custom "uid" claim present in our JWT so Clients.User(uid)
    // will correctly target the intended user.
    public class UidUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            if (connection?.User == null) return null;
            // Look for the `uid` claim used across the app
            var uid = connection.User.FindFirst("uid")?.Value;
            if (!string.IsNullOrEmpty(uid)) return uid;

            // Fallback to NameIdentifier if `uid` not present
            return connection.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
