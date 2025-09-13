namespace EgyptOnline.Domain.Interfaces
{
    public interface IOTPService
    {
        public Task<string> SendOtpAsync(string phoneNumber, bool isLive = false);
        public string GenerateOtp();

    }
}