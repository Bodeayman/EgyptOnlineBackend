using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using StackExchange.Redis;
using EgyptOnline.Utilities;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

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
    builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["RedisSettings:Configuration"];
    options.InstanceName = builder.Configuration["RedisSettings:InstanceName"];
});


    // IConnectionMultiplexer using settings from configuration
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        return ConnectionMultiplexer.Connect(builder.Configuration["RedisSettings:Configuration"]);
    });
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddIdentity<User, IdentityRole>(options =>
    {
        options.User.AllowedUserNameCharacters =
    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+"
    + "أابتثجحخدذرزسشصضطظعغفقكلمنهويءآأإىة٤٥٦٧٨٩٠"; // Arabic chars
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // Modular service registrations
    builder.Services.AddHostedService<SubscriptionCheckerService>();
    builder.Services.AddApplicationServices();
    builder.Services.ApiVersioningSettings();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddSwaggerWithJwt();

    // SignalR & Chat
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<MongoDB.Driver.IMongoClient>(sp =>
        new MongoDB.Driver.MongoClient(builder.Configuration["MongoDB:ConnectionString"])); // Placeholder
    builder.Services.AddScoped<EgyptOnline.Services.ChatService>();
    builder.Services.AddSingleton<EgyptOnline.Services.PresenceService>();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            builder =>
            {
                builder
                    .AllowAnyOrigin()    // Allow all domains
                    .AllowAnyMethod()    // Allow GET, POST, PUT, DELETE
                    .AllowAnyHeader();   // Allow headers like Content-Type
            });
    });

    if (builder.Environment.IsDevelopment())
    {
        FirebaseApp.Create(new AppOptions()
        {
            Credential = GoogleCredential.FromFile("serviceAccountKey.json")
        });
    }
    else
    {
        FirebaseApp.Create(new AppOptions()
        {
            Credential = GoogleCredential.FromFile("/app/config/serviceAccountKey.json")
        });
    }
















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
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            await IdentityExtensions.SeedRoles(roleManager);
            await IdentityExtensions.SeedAdmin(userManager, roleManager, builder.Configuration);

        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database migration failed");
            throw;
        }
    }

    // ---------- Middleware ----------
    app.UseStaticFiles(); // Serves from wwwroot by default

    // Also explicitly serve images folder
    var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
    Console.WriteLine($"Images path: {imagesPath}");
    Console.WriteLine($"Images path exists: {Directory.Exists(imagesPath)}");

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imagesPath),
        RequestPath = "/images"
    });
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
    app.UseCors("AllowAll");

    app.UseAuthentication();
    app.UseAuthorization();
    // app.UseMiddleware<SubscriptionCheckMiddleware>();

    // ---------- Serilog Request Logging ----------
    app.UseSerilogRequestLogging();

    // ---------- Map Endpoints ----------
    app.MapHub<EgyptOnline.Presentation.Hubs.ChatHub>("/chatHub");
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
