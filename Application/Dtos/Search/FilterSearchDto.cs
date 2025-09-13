namespace EgyptOnline.Dtos
{
    public class FilterSearchDto
    {
        public string? FullName { get; set; } = string.Empty;
        public List<string>? Skills { get; set; }
        public string? Location { get; set; }
    }
}