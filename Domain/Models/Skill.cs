
namespace EgyptOnline.Models
{
    public class Skill
    {

        public int Id { get; set; }
        public string Name { get; set; } = "Unknown";
        public required int WorkerId { get; set; }

        public required Worker Worker { get; set; }
    }
}