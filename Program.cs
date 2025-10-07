using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configure Serilog ----------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        restrictedToMinimumLevel: LogEventLevel.Information,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Debug,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}"
    )
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

    // Validate JWT Configuration Early
    var jwtKey = builder.Configuration["Jwt:Key"];
    var jwtRefreshKey = builder.Configuration["Jwt:RefreshKey"];
    var jwtIssuer = builder.Configuration["Jwt:Issuer"];
    var jwtAudience = builder.Configuration["Jwt:Audience"];

    if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtRefreshKey))
    {
        throw new InvalidOperationException(
            "JWT configuration is incomplete. Ensure Jwt__Key and Jwt__RefreshKey environment variables are set.");
    }

    builder.Services.AddControllers();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddIdentity<User, IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.AddApplicationServices();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddSwaggerWithJwt();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // ---------- Run migrations ----------
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
            Log.Error(ex, "Database migration failed. Check pending migrations and connection string.");
            throw; // Keep throw during dev to see actual error
        }
    }


    // ---------- Middleware ----------
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseHttpsRedirection();
    }

    app.UseCors();

    // ---------- Global Exception Handler ----------
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionHandlerPathFeature != null)
            {
                var ex = exceptionHandlerPathFeature.Error;
                Log.Error(ex, "Unhandled exception for request {Method} {Path}", context.Request.Method, context.Request.Path);
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "An unexpected error occurred",
                    detail = ex.Message
                });
            }
        });
    });

    app.UseAuthentication();
    app.UseAuthorization();

    // ---------- Serilog Request Logging ----------
    app.UseSerilogRequestLogging();

    // ---------- Map endpoints ----------
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
