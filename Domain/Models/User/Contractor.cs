namespace EgyptOnline.Models
{
    public class Contractor : ServicesProvider
    {
        public decimal Salary { get; set; } = 0;
        public required string Specialization { get; set; }
        public override string GetSpecialization()
        {
            return Specialization;
        }
    }
}