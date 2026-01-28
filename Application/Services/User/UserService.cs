using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.IdentityModel.Tokens;

/*
This is not related to the repository pattern that deals with the user database
But rather it will provide the functionalies that needed mostly for authentication
like generating JWT token and getting user ID from the token claims
*/

namespace EgyptOnline.Services
{
    public class UserService : IUserService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public UserService(IConfiguration config, ApplicationDbContext context, UserManager<User> userManager)
        {
            _config = config;
            _context = context;
            _userManager = userManager;
        }


        public async Task<string> GenerateJwtToken(User user, TokensTypes TokenType)
        {
            try
            {
                var roles = await _userManager.GetRolesAsync(user); // REAL DB roles

                SymmetricSecurityKey securityKey =
                    TokenType == TokensTypes.AccessToken
                    ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]))
                    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:RefreshKey"]));

                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                var expiry = TokenType == TokensTypes.RefreshToken
                    ? DateTime.UtcNow.AddDays(TokenPeriod.REFRESH_TOKEN_DAYS)
                    : DateTime.UtcNow.AddMinutes(TokenPeriod.ACCESS_TOKEN_MINS);

                var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("uid", user.Id),
            new Claim("token_type", TokenType.ToString()),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64
            )
        };

                // Optional business claim
                if (user.Subscription != null)
                {
                    claims.Add(new Claim(
                        "subscription_expires",
                        user.Subscription.EndDate.ToString("yyyy-MM-dd")
                    ));
                }

                Console.WriteLine(roles.Count);
                if (roles.Any())
                {
                    foreach (var role in roles)
                    {
                        Console.WriteLine(role);

                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }
                }
                else
                {
                    claims.Add(new Claim(ClaimTypes.Role, Roles.User)); // default
                }

                var token = new JwtSecurityToken(
                    issuer: _config["Jwt:Issuer"],
                    audience: _config["Jwt:Audience"],
                    claims: claims,
                    expires: expiry,
                    signingCredentials: credentials
                );

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch
            {
                throw; // preserve stack trace
            }
        }


        public string GetUserID(ClaimsPrincipal User)
        {

            var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
            if (userId == null)
            {
                return null;
            }

            return userId;
        }
        public ClaimsPrincipal ValidateRefreshToken(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            var handler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["Jwt:RefreshKey"]);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = handler.ValidateToken(refreshToken, validationParameters, out SecurityToken validatedToken);

                // Optional: Check if token type claim is actually "RefreshToken"
                var tokenType = principal.Claims.FirstOrDefault(c => c.Type == "token_type")?.Value;
                if (tokenType != TokensTypes.RefreshToken.ToString())
                    return null;

                return principal;
            }
            catch
            {
                return null; // invalid or expired token
            }
        }

        public async Task<string> GetUserLocation(ClaimsPrincipal User)
        {
            /*
            var UserId = GetUserID(User);
            if (UserId == null)
            {
                return null;
            }
            //Take care from this part
            User user = await _context.Users.FirstOrDefaultAsync(u => u.Id == UserId);
            Console.WriteLine($"User locaitons is {user.Location}");
            return user.Location ?? "";
            */

            return "Nothing to return really";
        }


    }

}