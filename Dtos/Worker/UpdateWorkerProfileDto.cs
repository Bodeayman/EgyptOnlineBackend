namespace EgyptOnline.Dtos
{
    public class UpdateWorkerProfileDto
    {
        public string? FullName { get; set; }

        public string? Email { get; set; }
        public string? Location { get; set; }
        public bool? IsAvailable { get; set; }
        public List<string>? Skills { get; set; }
    }
}