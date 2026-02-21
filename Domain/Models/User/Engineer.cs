namespace EgyptOnline.Models
{
    public class Engineer : ServicesProvider
    {

        public decimal Salary { get; set; } = 0;
        public required string Specialization { get; set; }
        public string DerivedSpec { get; set; } = string.Empty;

        public override string GetSpecialization()
        {
            return Specialization;
        }
        public override string GetDerivedSpecialization()
        {
            return DerivedSpec;
        }
    }
}