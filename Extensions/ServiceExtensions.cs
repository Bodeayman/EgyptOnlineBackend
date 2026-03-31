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

            // ─── Contract / Wallet / KYC Module ─────────────────────
            services.AddScoped<EgyptOnline.Application.Services.Contract.ContractService>();
            services.AddScoped<EgyptOnline.Application.Services.Wallet.WalletService>();
            services.AddScoped<EgyptOnline.Application.Services.Kyc.KycService>();

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
            services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = 429;

        // Token Bucket: Most production-friendly algorithm
        // Allows burst traffic while maintaining average rate
        options.AddTokenBucketLimiter("tokenBucket", opt =>
        {
            opt.TokenLimit = 50;                        // Max burst smaller than 100 to be safe
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 15;                        // Slightly higher queue for bursts
            opt.ReplenishmentPeriod = TimeSpan.FromSeconds(60);
            opt.TokensPerPeriod = 30;                   // 30 req/min (~0.5 req/sec average)
            opt.AutoReplenishment = true;
        });


        options.OnRejected = DefaultRejectedHandler;
    });



            return services;
        }

        private static Func<OnRejectedContext, CancellationToken, ValueTask> DefaultRejectedHandler = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = 429;
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                message = "Too many requests. Please try again later.",
                retryAfter = context.HttpContext.Response.Headers["Retry-After"]
            }, token);
        };

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
