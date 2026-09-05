using EgyptOnline.Services;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Tests.Unit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using FakeItEasy;
using System;
using System.Threading.Tasks;

namespace EgyptOnline.Tests.Unit.OTP
{
    public class OtpServiceTests : UnitTestBase
    {
        private readonly OtpService _otpService;
        private readonly IDistributedCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;

        public OtpServiceTests()
        {
            _cache = Cache;
            _scopeFactory = A.Fake<IServiceScopeFactory>();
            _otpService = new OtpService(_cache, _scopeFactory);
        }

        [Fact]
        public async Task SendOtpAsync_ShouldStoreOtpInCache()
        {
            // Arrange
            string key = "+201234567890";

            // Act
            await _otpService.SendOtpAsync(key, false);

            // Assert - Give background task time to complete
            await Task.Delay(100);

            // Verify OTP was stored in cache
            var cachedOtp = await _cache.GetStringAsync($"otp:{key}");
            Assert.NotNull(cachedOtp);
            Assert.Equal(6, cachedOtp.Length);
            Assert.True(int.TryParse(cachedOtp, out _));
        }

        [Fact]
        public async Task SendOtpAsync_ShouldTriggerBackgroundSmsSending()
        {
            // Arrange
            string key = "+201234567890";

            // Act
            await _otpService.SendOtpAsync(key, false);

            // Assert - Background task should have been triggered
            A.CallTo(_scopeFactory).WithReturnType<IServiceScope>()
                .MustHaveHappened();
        }

        [Fact]
        public async Task ValidateOtpAsync_ShouldReturnTrue_WhenOtpMatches()
        {
            // Arrange
            string key = "+201234567890";
            string otp = "123456";
            await _cache.SetStringAsync($"otp:{key}", otp);

            // Act
            bool result = await _otpService.ValidateOtpAsync(key, otp);

            // Assert
            Assert.True(result);
            var cached = await _cache.GetStringAsync($"otp:{key}");
            Assert.Null(cached); // Should be removed after validation
        }

        [Fact]
        public async Task ValidateOtpAsync_ShouldReturnFalse_WhenOtpDoesNotMatch()
        {
            // Arrange
            string key = "+201234567890";
            await _cache.SetStringAsync($"otp:{key}", "123456");

            // Act
            bool result = await _otpService.ValidateOtpAsync(key, "654321");

            // Assert
            Assert.False(result);
            var cached = await _cache.GetStringAsync($"otp:{key}");
            Assert.NotNull(cached); // Should still be present when mismatch
        }

        [Fact]
        public async Task ValidateOtpAsync_ShouldReturnFalse_WhenOtpExpired()
        {
            // Arrange
            string key = "+201234567890";
            // Don't store OTP to simulate expiration

            // Act
            bool result = await _otpService.ValidateOtpAsync(key, "123456");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SendOtpAsync_ShouldExtractPhoneNumber_FromEmailPhoneKey()
        {
            // Arrange
            string key = "test@example.com:+201234567890";

            // Act
            await _otpService.SendOtpAsync(key, false);

            // Assert - Give background task time to complete
            await Task.Delay(100);

            // Verify OTP was stored with full key
            var cachedOtp = await _cache.GetStringAsync($"otp:{key}");
            Assert.NotNull(cachedOtp);
            Assert.Equal(6, cachedOtp.Length);
        }
    }
}
