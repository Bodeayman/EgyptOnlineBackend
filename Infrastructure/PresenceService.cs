using System.Collections.Concurrent;

namespace EgyptOnline.Services
{
    public class PresenceService
    {
        // Using a ConcurrentDictionary to track online users and their multiple connections (devices)
        private static readonly ConcurrentDictionary<string, HashSet<string>> OnlineUsers = new();

        public Task<bool> UserConnected(string userId, string connectionId)
        {
            var isFirstConnection = false;

            OnlineUsers.AddOrUpdate(userId, 
                // If user is not in dictionary, add them with their first connection
                _ => {
                    isFirstConnection = true;
                    return new HashSet<string> { connectionId };
                },
                // If user exists, add the new connectionId to their set
                (_, connections) => {
                    lock (connections)
                    {
                        connections.Add(connectionId);
                    }
                    return connections;
                });

            return Task.FromResult(isFirstConnection);
        }

        public Task<bool> UserDisconnected(string userId, string connectionId)
        {
            var isLastConnection = false;

            if (OnlineUsers.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    connections.Remove(connectionId);
                    if (connections.Count == 0)
                    {
                        isLastConnection = true;
                        OnlineUsers.TryRemove(userId, out _);
                    }
                }
            }

            return Task.FromResult(isLastConnection);
        }

        public Task<bool> IsUserOnline(string userId)
        {
            return Task.FromResult(OnlineUsers.ContainsKey(userId));
        }

        public Task<List<string>> GetOnlineUsers()
        {
            return Task.FromResult(OnlineUsers.Keys.ToList());
        }
    }
}
