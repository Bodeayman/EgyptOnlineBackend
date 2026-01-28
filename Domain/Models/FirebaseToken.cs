namespace EgyptOnline.Models
{
    public class FirebaseToken
    {
        public int Id { get; set; }
        public string Token { get; set; }
        public User user { get; set; }

    }
}