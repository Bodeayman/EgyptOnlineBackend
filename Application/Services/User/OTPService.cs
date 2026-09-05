using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Threading.Tasks;

namespace EgyptOnline.Services
{
    public class OtpService : IOTPService
    {
        private readonly IDistributedCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Random _rng = new Random();

        public OtpService(IDistributedCache cache, IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        public async Task SendOtpAsync(string key, bool isRegister)
        {
            var otp = _rng.Next(100000, 999999).ToString();

            // Store OTP in cache (synchronous, must complete before returning)
            await _cache.SetStringAsync(
                $"otp:{key}",
                otp,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

            // Extract phone number from key (key format: "email:phone" or just "phone")
            var phoneNumber = key.Contains(':') ? key.Split(':')[1] : key;

            // Trigger background SMS sending (fire-and-forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var smsService = scope.ServiceProvider.GetRequiredService<ISmsService>();
                    await smsService.SendOtpSmsAsync(phoneNumber, otp);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Background SMS sending failed for {PhoneNumber}", phoneNumber);
                }
            });

            Log.Information("OTP generated and SMS sending triggered for {Key}", key);
        }

        public async Task<bool> ValidateOtpAsync(string key, string otp)
        {
            var cached = await _cache.GetStringAsync($"otp:{key}");
            if (cached == null || cached != otp) return false;

            await _cache.RemoveAsync($"otp:{key}");
            return true;
        }
    }
}
