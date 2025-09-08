
namespace EgyptOnline.Models
{
    public class Skill
    {

        public int Id { get; set; }
        public string Name { get; set; } = "Unknown";
        public string WorkerId { get; set; } = string.Empty;

        public Worker? Worker { get; set; }
    }
}