namespace EgyptOnline.Dtos
{
    public class FilterSearchDto
    {
        public string? FullName { get; set; } = string.Empty;
        public string? Profession { get; set; } = string.Empty;
        public string? Location { get; set; } = string.Empty;

        public int PageNumber { get; set; } = 1;
    }
}