namespace EgyptOnline.Models
{
    public class OtpRequestDto
    {
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class OtpVerifyDto
    {
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Otp { get; set; }
        public string NewPassword { get; set; }
    }
}