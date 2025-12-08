using System.Security.Claims;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

namespace EgyptOnline.Domain.Interfaces
{
    public interface IUserService
    {
        string GenerateJwtToken(User user, UsersTypes userRole, TokensTypes TokenType);
        public ClaimsPrincipal ValidateRefreshToken(RefreshRequest refreshToken);
        string GetUserID(ClaimsPrincipal user);
        Task<string> GetUserLocation(ClaimsPrincipal User);
    }
}