using EgyptOnline.Utilities;

namespace EgyptOnline.Models
{
    public class Sculptor : ServicesProvider
    {

        public decimal ServicePricePerDay { get; set; } = 0;

        public required WorkerTypes WorkerType { get; set; }


        public override string GetSpecialization()
        {
            return "Sculptor";
        }
        public override string GetDerivedSpecialization()
        {
            return "Sculptor";
        }

    }
}