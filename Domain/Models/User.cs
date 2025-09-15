using Microsoft.AspNetCore.Identity;

namespace EgyptOnline.Models
{
    public class User : IdentityUser
    {
        public ServicesProvider? ServiceProvider { get; set; }

        public Subscription? Subscription { get; set; }



    }
}