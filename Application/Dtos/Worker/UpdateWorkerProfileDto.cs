namespace EgyptOnline.Dtos
{
    public class UpdateUserProfileDto
    {
        public string? FullName { get; set; }

        public string? Email { get; set; }
        public string? Location { get; set; }
        public bool? IsAvailable { get; set; }

        public string? Bio { get; set; }
        public string? Skill { get; set; }

        public string? Specialization { get; set; }
        public string? Business { get; set; }
        public string? ProviderType { get; set; } = "Worker";
    }
}