using System.Text;
using EgyptOnline.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using EgyptOnline.Repositories;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Application.Interfaces;
using EgyptOnline.Strategies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using StackExchange.Redis;
using EgyptOnline.Infrastructure;
using System.Security.Claims;
/*
This file is for adding functionalies to program.cs instead of packing everything in one file
like adding authentication and swagger configuration
*/
namespace EgyptOnline.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<UserImageService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ICDNService, LocalStorageService>();
            services.AddScoped<IPaymentStrategy, MobileWalletPaymentStrategy>();
            services.AddScoped<IPaymentStrategy, CreditCardPaymentStrategy>();
            services.AddScoped<IPaymentService, PaymobService>();
            services.AddScoped<CreditCardPaymentStrategy>();
            services.AddScoped<MobileWalletPaymentStrategy>();
            services.AddScoped<FawryPaymentStrategy>();
            services.AddScoped<UserRegisterationService>();
            services.AddScoped<UserSubscriptionServices>();
            services.AddScoped<NotificationService>();

            services.AddScoped<UserPointService>();
            services.AddSingleton<IEmailService, EmailService>();


            services.AddScoped<IOTPService, OtpService>();

            services.AddHttpClient();

            return services;
        }
        public static IServiceCollection ApiVersioningSettings(this IServiceCollection services)
        {
            services.AddApiVersioning(options =>
            {
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ReportApiVersions = true;
            });
            /*
            services.AddRateLimiter(options =>
 {
     options.AddFixedWindowLimiter("FixedPolicy", opt =>
     {
         opt.Window = TimeSpan.FromMinutes(1);
         opt.PermitLimit = 100;
         opt.QueueLimit = 2;
         opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
     });
 });
 */

            return services;
        }

        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("Jwt");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = ClaimTypes.Role

                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var path = context.HttpContext.Request.Path;

                        // Only intercept requests for the SignalR hub
                        if (path.StartsWithSegments("/chathub"))
                        {
                            // SignalR sends the token in query string: ?access_token=...
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                context.Token = accessToken;
                            }
                        }

                        return Task.CompletedTask;
                    }
                };

            });

            return services;
        }

        public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new() { Title = "Ma3ak API", Version = "v1" });
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your JWT token like: Bearer {your token}"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });

            return services;
        }

    }
}
