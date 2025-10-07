using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;

namespace EgyptOnline.Models
{
    public class UserRegisterationResult
    {
        public required IdentityResult Result { get; set; }

        public User? User { get; set; }
    }
}