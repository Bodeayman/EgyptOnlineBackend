using System.Text.Json;
using EgyptOnline.Interfaces;

namespace EgyptOnline.Services
{
    public class PaymobService : IPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public PaymobService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<string> CreatePaymentSession(decimal amount, string orderId)
        {
            var apiKey = _config["Paymob:ApiKey"];

            // Step 1: Authenticate & get token (if required by Paymob)
            var authPayload = new { api_key = apiKey };
            var authResponse = await _httpClient.PostAsJsonAsync(
                "https://accept.paymob.com/api/auth/tokens", authPayload);
            authResponse.EnsureSuccessStatusCode();
            var authJson = await authResponse.Content.ReadFromJsonAsync<JsonElement>();
            string token = authJson.GetProperty("token").GetString();
            if (token == null)
            {
                throw new Exception("Failed to retrieve auth token from Paymob.");
            }

            var orderPayload = new
            {
                auth_token = token,
                amount_cents = (int)(amount * 100),
                currency = "EGP",
                merchant_order_id = orderId
            };
            var orderResponse = await _httpClient.PostAsJsonAsync(
                "https://accept.paymob.com/api/ecommerce/orders", orderPayload);
            orderResponse.EnsureSuccessStatusCode();
            var orderJson = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
            string paymentToken = orderJson.GetProperty("id").GetRawText();

            return paymentToken;
        }
    }
}