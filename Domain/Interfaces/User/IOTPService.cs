namespace EgyptOnline.Domain.Interfaces
{
    public interface IOTPService
    {
        Task SendOtpAsync(string phoneNumber, bool isRegister);
        Task<bool> ValidateOtpAsync(string phoneNumber, string otp);

    }
}