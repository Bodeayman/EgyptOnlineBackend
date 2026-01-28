using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using EgyptOnline.Data;
using EgyptOnline.Utilities;
using System.Security.Claims;

namespace EgyptOnline.Domain.Attributes
{
    /// <summary>
    /// Authorization attribute that checks subscription status from database.
    /// Use this only for critical operations that require active subscription.
    /// For non-critical operations, check the subscription_expires claim in the token.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequireSubscriptionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Skip if already unauthorized
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
                return;

            var userId = context.HttpContext.User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    message = "User ID not found in token",
                    errorCode = "Unauthorized"
                });
                return;
            }

            // Get DB context from service provider
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

            // Check subscription from database (fresh check)
            var user = await dbContext.Users
                .Include(u => u.Subscription)
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                context.Result = new NotFoundObjectResult(new
                {
                    message = "User not found",
                    errorCode = "UserNotFound"
                });
                return;
            }

            // Check if subscription is active
            if (user.Subscription == null ||
                (user.ServiceProvider != null &&
                (!user.ServiceProvider.IsAvailable)))
            {
                // user.ServiceProvider.IsAvailable = false;
                await dbContext.SaveChangesAsync();
                // Match exact format from old CheckSubscription() for backward compatibility
                context.Result = new ObjectResult(new
                {
                    message = "Your Subscription period Expired",
                    errorCode = UserErrors.SubscriptionInvalid.ToString(),
                    LastDate = user.Subscription?.EndDate.ToString()
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }
    }
}

