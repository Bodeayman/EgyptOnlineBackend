using EgyptOnline.Utilities;

namespace EgyptOnline.Models
{
    public class Worker : ServicesProvider
    {
        public required string Skill { get; set; }

        public decimal ServicePricePerDay { get; set; } = 0;

        public required WorkerTypes WorkerType { get; set; }

    }
}