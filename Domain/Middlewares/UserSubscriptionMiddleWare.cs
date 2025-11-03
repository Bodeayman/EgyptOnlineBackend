using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

public class SubscriptionCheckMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionCheckMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine("The subscription middleware is in effect");

        var path = context.Request.Path.Value?.ToLower();
        if (path != null && (path.Contains("/register") || path.Contains("/login")))
        {
            await _next(context);
            return;
        }

        // resolve scoped services here (inside request)
        var userService = context.RequestServices.GetRequiredService<IUserService>();
        var userManager = context.RequestServices.GetRequiredService<UserManager<User>>();

        var userId = userService.GetUserID(context.User);


        Console.WriteLine(userId);
        if (userId != null)
        {
            var db = context.RequestServices.GetRequiredService<ApplicationDbContext>();

            // ✅ Load user with Subscription eagerly
            var user = await db.Users
                .Include(u => u.Subscription)
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                Console.WriteLine("Checking subscription...");
                if (user.Subscription.EndDate < DateTime.Now)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    user.ServiceProvider.IsAvailable = false;
                    await db.SaveChangesAsync();
                    var response = new
                    {
                        message = "Your subscription is expired. Please renew it again.",
                        lastDate = user.Subscription.EndDate.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    // Serialize it as JSON
                    var json = System.Text.Json.JsonSerializer.Serialize(response,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true // makes it pretty-printed
                        });

                    await context.Response.WriteAsync(json);
                    return;
                }
            }
        }

        await _next(context);
    }
}
