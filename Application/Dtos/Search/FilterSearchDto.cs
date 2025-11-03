using System.Text.Json.Serialization;
using EgyptOnline.Models;
using EgyptOnline.Utilities;

namespace EgyptOnline.Dtos
{
    public class FilterSearchDto
    {
        public string? FullName { get; set; } = string.Empty;
        public string? Profession { get; set; } = string.Empty;

        public WorkerTypes? WorkerType { get; set; } = WorkerTypes.PerDay;
        public string? Location { get; set; } = string.Empty;


        public LocationCoords? LocationCoords { get; set; }

        public int PageNumber { get; set; } = 1;
    }
}