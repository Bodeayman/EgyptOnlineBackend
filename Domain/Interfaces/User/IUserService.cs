using System.Security.Claims;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

namespace EgyptOnline.Domain.Interfaces
{
    public interface IUserService
    {
        public Task<string> GenerateJwtToken(User user, TokensTypes TokenType);
        public ClaimsPrincipal ValidateRefreshToken(string refreshToken);
        public string GetUserID(ClaimsPrincipal user);
        public Task<string> GetUserLocation(ClaimsPrincipal User);
    }
}