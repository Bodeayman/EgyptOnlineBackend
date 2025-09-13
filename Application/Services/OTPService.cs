using EgyptOnline.Domain.Interfaces;

namespace EgyptOnline.Services
{




    public class SmsMisrOtpService : IOTPService
    {
        private readonly HttpClient _httpClient;
        public readonly IConfiguration _config;

        private readonly string _username;
        private readonly string _password = "YOUR_PASSWORD";
        private readonly string _senderToken = "YOUR_SENDER_TOKEN";
        private readonly string _templateToken = "YOUR_TEMPLATE_TOKEN";

        public SmsMisrOtpService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
            _username = _config["SMSMisr:UserName"]!;
            _password = _config["SMSMisr:Password"]!;
            _senderToken = _config["SMSMisr:SenderToken"]!;
            _templateToken = _config["SMSMisr:TemplateToken"]!;

        }

        public async Task<string> SendOtpAsync(string phoneNumber, bool isLive = false)
        {
            try
            {
                string otpCode = GenerateOtp();
                var environment = isLive ? 1 : 2;

                var url = $"https://smsmisr.com/api/OTP/?" +
                          $"environment={environment}&" +
                          $"username={_username}&" +
                          $"password={_password}&" +
                          $"sender={_senderToken}&" +
                          $"mobile={phoneNumber}&" +
                          $"template={_templateToken}&" +
                          $"otp={otpCode}";

                var response = await _httpClient.PostAsync(url, null);
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine(result);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public string GenerateOtp()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString(); // Generates 6-digit OTP
        }
    }
}