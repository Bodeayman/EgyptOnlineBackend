namespace EgyptOnline.Models
{
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public string Token { get; set; }

        public Guid UserId { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public User User { get; set; }
    }
}