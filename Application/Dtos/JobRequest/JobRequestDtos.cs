namespace EgyptOnline.Dtos.JobRequest
{
    public class JobRequestSummaryDto
    {
        public int Id { get; set; }
        public string ClientUserId { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
        public string Skill { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? District { get; set; }
        public string? WorkDetails { get; set; }
        public string? WorkerPlace { get; set; }
        public string? PerpayDetails { get; set; }
        public int? WorkerType { get; set; }

        public decimal PayRate { get; set; }
        public int? Days { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? AcceptedProviderUserId { get; set; }
    }

    public class JobRequestInterestResultDto
    {
        public int Id { get; set; }
        public int JobRequestId { get; set; }
        public string ServiceProviderUserId { get; set; } = string.Empty;
        public bool IsInterested { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
