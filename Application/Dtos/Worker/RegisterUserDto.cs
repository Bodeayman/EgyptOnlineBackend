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

        public string? UserType { get; set; } = "SP";

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
        public decimal Pay { get; set; } = 0;
        //Contractor
        public string? Specialization { get; set; }

        public string? ReferralUserName { get; set; }


    }
}