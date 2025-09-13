using System.Security.Claims;
using EgyptOnline.Models;

namespace EgyptOnline.Domain.Interfaces
{
    public interface IUserService
    {
        string GenerateJwtToken(User user);
        string GetUserID(ClaimsPrincipal user);

        string GenerateRefreshToken(User user);
    }
}