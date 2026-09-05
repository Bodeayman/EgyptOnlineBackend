using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EgyptOnline.Infrastructure
{
    public interface ISmsService
    {
        Task SendOtpSmsAsync(string phoneNumber, string otp);
    }

    public class SmsService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _apiSecret;
        private readonly string _apiKey;
        private const string ApiUrl = "https://smsapi.zadx.net/api/v1/otp/send";

        public SmsService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _apiSecret = configuration["SMS:ApiSecret"] ?? throw new InvalidOperationException("SMS:ApiSecret is not configured");
            _apiKey = configuration["SMS:ApiKey"] ?? throw new InvalidOperationException("SMS:ApiKey is not configured");
        }

        public async Task SendOtpSmsAsync(string phoneNumber, string otp)
        {
            try
            {
                var payload = new
                {
                    to = phoneNumber,
                    otp = otp,
                    locale = "ar"
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
                request.Content = content;
                request.Headers.Add("X-Api-Secret", _apiSecret);
                request.Headers.Add("Idempotency-Key", $"qs-otp-{Guid.NewGuid()}");
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("X-Api-Key", _apiKey);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error("SMS API error: {StatusCode} - {Error}", (int)response.StatusCode, errorContent);
                    throw new HttpRequestException($"SMS API error {(int)response.StatusCode}: {errorContent}");
                }

                Log.Information("OTP SMS sent successfully to {PhoneNumber}", phoneNumber);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send OTP SMS to {PhoneNumber}", phoneNumber);
                throw;
            }
        }
    }
}
