namespace EgyptOnline.Dtos
{
    public class UpdateUserProfileDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        public string? Location { get; set; }
        public string? Bio { get; set; }

        public string? Skill { get; set; }

        public string? Specialization { get; set; }

        public decimal Pay { get; set; } = 0;


        public string? Owner { get; set; }
        public string? Business { get; set; }
        public string? ProviderType { get; set; } = "Worker";
    }
}