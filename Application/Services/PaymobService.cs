using System.Text.Json;
using EgyptOnline.Domain.Interfaces;

using EgyptOnline.Models;

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

        public async Task<string> CreatePaymentSession(decimal? amount, string orderId, User user, string currency = "EGP")
        {
            try
            {
                Console.WriteLine("Starting Payment Session Creation");
                var apiKey = _config["Payment:APIKey"];
                var integrationId = int.Parse(_config["Payment:PaymentIntegration"]!);
                var iframeId = _config["Payment:IFrame"];
                if (apiKey == null || integrationId == 0 || iframeId == null)
                    throw new Exception("Paymob configuration is missing");
                // Step 1: Auth
                var authResponse = await _httpClient.PostAsJsonAsync(
                    "https://accept.paymob.com/api/auth/tokens",
                    new { api_key = apiKey });

                authResponse.EnsureSuccessStatusCode();
                var authJson = await authResponse.Content.ReadFromJsonAsync<JsonElement>();
                string token = authJson.GetProperty("token").GetString()
                    ?? throw new Exception("Failed to retrieve auth token");
                Console.WriteLine("Auth Token Retrieved");
                // Step 2: Register Order
                var orderResponse = await _httpClient.PostAsJsonAsync(
                    "https://accept.paymob.com/api/ecommerce/orders",
                    new
                    {
                        auth_token = token,
                        delivery_needed = "false",
                        amount_cents = (int)(amount * 100),
                        currency = currency,
                        merchant_order_id = orderId,
                        user_id = user.Id,
                        items = Array.Empty<object>()
                    });
                Console.WriteLine(orderResponse.Content.ReadAsStringAsync().Result);
                orderResponse.EnsureSuccessStatusCode();
                var orderJson = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
                int orderIdInt = orderJson.GetProperty("id").GetInt32();
                Console.WriteLine("Register Order");

                // Step 3: Get Payment Key
                var paymentKeyResponse = await _httpClient.PostAsJsonAsync(
                    "https://accept.paymob.com/api/acceptance/payment_keys",
                    new
                    {
                        auth_token = token,
                        amount_cents = (int)(amount * 100),
                        expiration = 3600,
                        order_id = orderIdInt,
                        user_id = "",
                        billing_data = new
                        {
                            first_name = user.UserName,
                            email = user.Email,
                            phone_number = user.PhoneNumber,
                        },
                        currency = currency,
                        integration_id = integrationId
                    });

                paymentKeyResponse.EnsureSuccessStatusCode();
                var paymentKeyJson = await paymentKeyResponse.Content.ReadFromJsonAsync<JsonElement>();
                string paymentToken = paymentKeyJson.GetProperty("token").GetString()
                    ?? throw new Exception("Failed to retrieve payment key");
                Console.WriteLine("Payment Key Retrieved");

                // Step 4: Return iframe URL
                return $"https://accept.paymob.com/api/acceptance/iframes/{iframeId}?payment_token={paymentToken}";
            }
            catch (Exception ex)
            {
                // Log the exception (not implemented here)
                throw new Exception($"Error creating payment session , {ex.Message}");
            }
        }

    }
}