namespace EgyptOnline.Dtos
{
    public class UpdateUserProfileDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }


        public string? Bio { get; set; }

        public decimal Pay { get; set; } = 0;





        public string? Governorate { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}