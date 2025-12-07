using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Infrastructure; // IEmailService
using EgyptOnline.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Threading.Tasks;

namespace EgyptOnline.Services
{
    public class OtpService : IOTPService
    {
        private readonly IDistributedCache _cache;
        private readonly IEmailService _emailService;
        private readonly Random _rng = new Random();

        public OtpService(IDistributedCache cache, IEmailService emailService)
        {
            _cache = cache;
            _emailService = emailService;
        }

        public async Task SendOtpAsync(string key, bool isRegister)
        {
            var otp = _rng.Next(100000, 999999).ToString();

            // Store OTP in cache
            await _cache.SetStringAsync(
                $"otp:{key}",
                otp,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

            // Extract email from key (key format: "email:phone")
            var email = key.Split(':')[0];

            // Send OTP via email
            await _emailService.SendEmailAsync(
                email,
                "Your OTP Code",
                $"Your OTP code is: {otp}. It expires in 5 minutes.");

            Console.WriteLine($"[OTP] {otp} sent to {email}");
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
