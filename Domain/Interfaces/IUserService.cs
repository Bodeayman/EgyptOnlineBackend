using System.Security.Claims;
using EgyptOnline.Models;
using EgyptOnline.Utilities;

namespace EgyptOnline.Domain.Interfaces
{
    public interface IUserService
    {
        string GenerateJwtToken(User user, UsersTypes userRole);
        string GetUserID(ClaimsPrincipal user);

        string GenerateRefreshToken(User user);
    }
}