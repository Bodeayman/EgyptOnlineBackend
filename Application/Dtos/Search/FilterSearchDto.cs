using System.Text.Json.Serialization;
using EgyptOnline.Models;
using EgyptOnline.Utilities;

namespace EgyptOnline.Dtos
{
    public class FilterSearchDto
    {
        public string? FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; } = string.Empty;

        public string? Profession { get; set; } = string.Empty;

        public WorkerTypes? WorkerType { get; set; } = WorkerTypes.PerDay;
        public string? Governorate { get; set; } = string.Empty;
        public string? City { get; set; } = string.Empty;
        public string? District { get; set; } = string.Empty;



        public int PageNumber { get; set; } = 1;
    }
}