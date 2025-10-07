using EgyptOnline.Utilities;

namespace EgyptOnline.Dtos
{
    public class RegisterWorkerDto
    {
        public required string FirstName { get; set; } = string.Empty;

        public string? LastName { get; set; }
        public required string Email { get; set; } = string.Empty;
        public required string PhoneNumber { get; set; } = string.Empty;
        public required string Password { get; set; } = string.Empty;

        public required string UserType { get; set; } = "User";

        public required string Location { get; set; }
        //SP related
        public string? Bio { get; set; }

        public string? ProviderType { get; set; } = "Worker";

        //Worker
        public string? Skill { get; set; }
        public WorkerTypes WorkerType { get; set; } = WorkerTypes.PerDay;

        //Company
        public string? Business { get; set; }

        public string? Owner { get; set; }

        //Contractor
        public string? Specialization { get; set; }


    }
}