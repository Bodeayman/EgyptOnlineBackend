namespace EgyptOnline.Models
{
    public class Engineer : ServicesProvider
    {

        public decimal Salary { get; set; } = 0;
        public required string Specialization { get; set; }

    }
}