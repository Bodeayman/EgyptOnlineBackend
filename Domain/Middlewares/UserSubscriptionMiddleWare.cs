using EgyptOnline.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

public class SubscriptionCheckMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionCheckMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<User> userManager)
    {
        //Exclude the Register and Login from This middleware
        var path = context.Request.Path.Value?.ToLower();
        if (path != null && (path.Contains("/register") || path.Contains("/login")))
        {
            await _next(context);
            return;
        }


        //Otherwise check the subscription, so i need to check the result

        var userId = context.User?.FindFirst("uid")?.Value; // user id from token
        if (userId != null)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user != null)
            {

                if (user.Subscription!.EndDate < DateTime.Now)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Subscription expired");
                    return;

                }

            }
        }

        await _next(context);
    }
}
