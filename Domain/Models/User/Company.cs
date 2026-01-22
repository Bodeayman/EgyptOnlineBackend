namespace EgyptOnline.Models
{
    public class Company : ServicesProvider
    {

        public required string Business { get; set; }

        public required string Owner { get; set; }
        public override string GetSpecialization()
        {
            return Business;
        }
    }

}