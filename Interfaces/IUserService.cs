using System.Security.Claims;
using EgyptOnline.Models;

namespace EgyptOnline.Interfaces
{
    public interface IUserService
    {
        string GenerateJwtToken(User user);
        string GetUserID(ClaimsPrincipal user);
    }
}