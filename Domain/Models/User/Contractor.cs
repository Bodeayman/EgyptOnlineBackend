namespace EgyptOnline.Models
{
    public class Contractor : ServicesProvider
    {
        public required string Specialization { get; set; }
        public override string GetSpecialization()
        {
            return Specialization;
        }
        public override string GetDerivedSpecialization()
        {
            return Specialization;
        }
    }
}