using EgyptOnline.Utilities;

namespace EgyptOnline.Models
{
    public class Assistant : ServicesProvider
    {
        public required string Skill { get; set; }

        public decimal ServicePricePerDay { get; set; } = 0;
        public string DerivedSpec { get; set; } = string.Empty;


        public override string GetSpecialization()
        {
            return Skill;
        }
        public override string GetDerivedSpecialization()
        {
            return DerivedSpec;
        }

    }
}
