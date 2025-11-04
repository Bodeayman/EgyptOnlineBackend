using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ---------- Serilog Configuration ----------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        restrictedToMinimumLevel: LogEventLevel.Information,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Debug,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}")
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting application in {Environment} environment", builder.Environment.EnvironmentName);

    // ---------- Configuration ----------
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    // ---------- Services ----------
    builder.Services.AddControllers();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddIdentity<User, IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // Modular service registrations
    builder.Services.AddApplicationServices();
    builder.Services.ApiVersioningSettings();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddSwaggerWithJwt();

    var app = builder.Build();

    // ---------- Run Migrations ----------
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            Log.Information("Running database migrations...");
            db.Database.Migrate();
            Log.Information("Database migrations completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database migration failed");
            throw;
        }
    }

    // ---------- Middleware ----------



    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseDeveloperExceptionPage(); // full details only in dev

    }
    else
    {
        app.UseExceptionHandler(errApp =>
        {
            errApp.Run(async context =>
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";

                // safe generic message
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    message = "An unexpected error occurred. Please contact support."
                });
                await context.Response.WriteAsync(json);
            });
        });
    }


    app.UseHttpsRedirection();

    // ---------- Global Exception Handler ----------
    app.UseExceptionHandler(errApp =>
       {
           errApp.Run(async context =>
           {
               context.Response.StatusCode = 500;
               context.Response.ContentType = "application/json";

               var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
               if (exceptionHandlerPathFeature != null)
               {
                   // log full details internally
                   Log.Error(exceptionHandlerPathFeature.Error,
                             "Unhandled exception for request {Method} {Path}",
                             context.Request.Method, context.Request.Path);
               }

               // return safe generic message to client
               var json = System.Text.Json.JsonSerializer.Serialize(new
               {
                   message = "An unexpected error occurred. Please contact support."
               });
               await context.Response.WriteAsync(json);
           });
       });

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    // app.UseMiddleware<SubscriptionCheckMiddleware>();

    // ---------- Serilog Request Logging ----------
    app.UseSerilogRequestLogging();

    // ---------- Map Endpoints ----------
    app.MapControllers();
    app.MapGet("/health", () => Results.Ok("Healthy"));

    Log.Information("Application started successfully on {Environment}", app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
