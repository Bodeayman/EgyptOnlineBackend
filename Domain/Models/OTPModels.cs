namespace EgyptOnline.Models
{
    public class OtpRequestDto
    {
        /// <summary>Optional; user is identified by phone when email is not used.</summary>
        public string? Email { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class OtpVerifyDto
    {
        /// <summary>Optional; user is identified by phone when email is not used.</summary>
        public string? Email { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}