using System.Security.Claims;
using EgyptOnline.Models;
using EgyptOnline.Utilities;

namespace EgyptOnline.Domain.Interfaces
{
    public interface IUserService
    {
        string GenerateJwtToken(User user, UsersTypes userRole, TokensTypes TokenType);
        public ClaimsPrincipal ValidateRefreshToken(string refreshToken);
        string GetUserID(ClaimsPrincipal user);
        Task<string> GetUserLocation(ClaimsPrincipal User);
    }
}