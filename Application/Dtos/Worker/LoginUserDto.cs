using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Dtos
{
    public class LoginWorkerDto
    {
        /// <summary>Phone number or email (phone is primary; e.g. 01012345678 or user@example.com)</summary>
        [Required(ErrorMessage = "Phone number or email is required")]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}