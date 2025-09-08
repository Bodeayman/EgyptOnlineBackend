namespace EgyptOnline.Models
{
    public class Worker : User
    {
        public bool IsAvailable { get; set; }
        public ICollection<Skill> Skills { get; set; } = new List<Skill>();
        public string? Location { get; set; }
    }
}