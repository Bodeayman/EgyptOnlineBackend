namespace EgyptOnline.Domain.Interfaces
{
    public interface IOTPService
    {
        Task SendOtpAsync(string key, bool isRegister);
        Task<bool> ValidateOtpAsync(string key, string otp);

    }
}