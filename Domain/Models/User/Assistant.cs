using EgyptOnline.Utilities;

namespace EgyptOnline.Models
{
    public class Assistant : ServicesProvider
    {
        public required string Skill { get; set; }

        public decimal ServicePricePerDay { get; set; } = 0;


        public override string GetSpecialization()
        {
            return Skill;
        }

    }
}