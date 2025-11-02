namespace EgyptOnline.Models
{
    public class LocationCoords
    {
        public int Id;
        public double Latitude;
        public double Longitude;
        public string UserId { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}