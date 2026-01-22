using System.Text.Json;
using EgyptOnline.Models;

namespace EgyptOnline.Strategies
{
    public interface IPaymentStrategy
    {
        public Task<string> PayAsync(decimal amount, User user, int paymentId);
    }
    public class CreditCardPaymentStrategy : IPaymentStrategy
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public CreditCardPaymentStrategy(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<string> PayAsync(decimal amount, User user, int paymentId)
        {
            // 1. Auth
            var authResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/auth/tokens",
                new { api_key = _config["Payment:APIKey"] });
            authResponse.EnsureSuccessStatusCode();
            var token = (await authResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

            // 2. Register Order - Use paymentId as merchant_order_id
            var orderResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/ecommerce/orders",
                new { auth_token = token, delivery_needed = "false", amount_cents = (int)(amount * 100), merchant_order_id = paymentId.ToString(), items = Array.Empty<object>() });
            orderResponse.EnsureSuccessStatusCode();
            var orderId = (await orderResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            // 3. Get Payment Key
            var integrationId = int.Parse(_config["Payment:CreditCardId"]!);
            Console.WriteLine(integrationId);
            var paymentKeyResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/acceptance/payment_keys",
                new
                {
                    auth_token = token,
                    amount_cents = (int)(amount * 100),
                    expiration = 3600,
                    order_id = orderId,
                    billing_data = new
                    {
                        first_name = user?.FirstName ?? "Customer",
                        last_name = user?.LastName ?? "Customer",
                        email = user?.Email ?? "customer@example.com",
                        phone_number = user?.PhoneNumber ?? "20100000000",
                        apartment = "NA",
                        floor = "NA",
                        building = "NA",
                        street = user?.District ?? "NA",
                        shipping_method = "NA",
                        postal_code = "00000",
                        city = user?.City ?? "Cairo",
                        state = user?.Governorate ?? "Cairo",
                        country = "Egypt"
                    },
                    integration_id = integrationId,
                    currency = "EGP"
                });


            paymentKeyResponse.EnsureSuccessStatusCode();

            var paymentToken = (await paymentKeyResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

            // 4. Return iframe URL
            var iframeId = _config["Payment:IFrame"];
            Console.WriteLine(iframeId);
            return $"https://accept.paymob.com/api/acceptance/iframes/{iframeId}?payment_token={paymentToken}";
        }
    }

    public class MobileWalletPaymentStrategy : IPaymentStrategy
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public MobileWalletPaymentStrategy(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<string> PayAsync(decimal amount, User user, int paymentId)
        {
            // 1. Auth
            var authResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/auth/tokens",
                new { api_key = _config["Payment:APIKey"] });
            authResponse.EnsureSuccessStatusCode();
            var token = (await authResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

            // 2. Register Order - Use paymentId as merchant_order_id
            var orderResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/ecommerce/orders",
                new { auth_token = token, delivery_needed = "false", amount_cents = (int)(amount * 100), merchant_order_id = paymentId.ToString(), items = Array.Empty<object>() });
            orderResponse.EnsureSuccessStatusCode();
            var orderId = (await orderResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            // 3. Get Payment Key
            var integrationId = int.Parse(_config["Payment:MobileWalletId"]!);
            var paymentKeyResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/acceptance/payment_keys",
                new
                {
                    auth_token = token,
                    amount_cents = (int)(amount * 100),
                    expiration = 3600,
                    order_id = orderId,
                    integration_id = integrationId,
                    billingData = new
                    {
                        first_name = user?.FirstName ?? user?.UserName ?? "Customer",
                        last_name = user?.LastName ?? user?.UserName ?? "Customer",
                        email = user?.Email ?? "customer@example.com",
                        phone_number = user?.PhoneNumber ?? "20100000000",
                        apartment = "NA",
                        floor = "NA",
                        building = "NA",
                        street = "NA",
                        shipping_method = "NA",
                        postal_code = user?.District ?? "00000",
                        city = user?.City ?? "Cairo",
                        state = user?.Governorate ?? "Cairo",
                        country = "Egypt"
                    },
                    currency = "EGP"
                });
            paymentKeyResponse.EnsureSuccessStatusCode();
            var paymentToken = (await paymentKeyResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

            // 4. Initiate Wallet Payment
            var walletResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/acceptance/payments/pay",
                new
                {
                    source = new { identifier = user.PhoneNumber, subtype = "WALLET" },
                    payment_token = paymentToken
                });

            return await walletResponse.Content.ReadAsStringAsync(); // JSON with pending OTP info
        }
    }




    public class FawryPaymentStrategy : IPaymentStrategy
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public FawryPaymentStrategy(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<string> PayAsync(decimal amount, User user, int paymentId)
        {
            // 1. Auth
            var authResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/auth/tokens",
                new { api_key = _config["Payment:APIKey"] });
            authResponse.EnsureSuccessStatusCode();
            var token = (await authResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

            // 2. Register Order - Use paymentId as merchant_order_id
            var orderResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/ecommerce/orders",
                new { auth_token = token, delivery_needed = "false", amount_cents = (int)(amount * 100), merchant_order_id = paymentId.ToString(), items = Array.Empty<object>() });
            orderResponse.EnsureSuccessStatusCode();
            var orderId = (await orderResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            // 3. Get Payment Key
            // Assuming config has Payment:FawryId, if not user needs to add it or we use placeholder
            var integrationId = int.Parse(_config["Payment:FawryId"] ?? "0");
            var paymentKeyResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/acceptance/payment_keys",
                new
                {
                    auth_token = token,
                    amount_cents = (int)(amount * 100),
                    expiration = 3600,
                    order_id = orderId,
                    integration_id = integrationId,
                    billingData = new
                    {
                        first_name = user?.FirstName ?? user?.UserName ?? "Customer",
                        last_name = user?.LastName ?? user?.UserName ?? "Customer",
                        email = user?.Email ?? "customer@example.com",
                        phone_number = user?.PhoneNumber ?? "20100000000",
                        apartment = "NA",
                        floor = "NA",
                        building = "NA",
                        street = "NA",
                        shipping_method = "NA",
                        postal_code = user?.District ?? "00000",
                        city = user?.City ?? "Cairo",
                        state = user?.Governorate ?? "Cairo",
                        country = "Egypt"
                    },
                    currency = "EGP"
                });
            paymentKeyResponse.EnsureSuccessStatusCode();
            var paymentToken = (await paymentKeyResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

            // 4. Initiate Fawry Payment
            var fawryResponse = await _httpClient.PostAsJsonAsync("https://accept.paymob.com/api/acceptance/payments/pay",
                new
                {
                    source = new { identifier = "AGGREGATOR", subtype = "AGGREGATOR" },
                    payment_token = paymentToken
                });

            fawryResponse.EnsureSuccessStatusCode();
            // Return reference number
            var responseJson = await fawryResponse.Content.ReadFromJsonAsync<JsonElement>();
            // Typically returned in data.bill_reference
            if (responseJson.TryGetProperty("data", out var data) && data.TryGetProperty("bill_reference", out var billRef))
            {
                return billRef.GetInt32().ToString();
            }
            return "Reference number not found";
        }
    }
}