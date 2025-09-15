namespace EgyptOnline.Models
{
    public class Doctor : ServicesProvider
    {
        public int Id { get; set; }
        public string Specialization { get; set; } = "Unknown";

    }
}