namespace EgyptOnline.Dtos
{
    public class RegisterWorkerDto
    {
        public required string FullName { get; set; } = string.Empty;
        public required string Email { get; set; } = string.Empty;
        public required string PhoneNumber { get; set; } = string.Empty;
        public required string Password { get; set; } = string.Empty;

        public required string UserType { get; set; } = "User";

        public string? Location { get; set; }

        public string? Bio { get; set; }
        public string? Skill { get; set; }

        public string? Specialization { get; set; }
        public string? Business { get; set; }
        public string? ProviderType { get; set; } = "Worker";
    }
}