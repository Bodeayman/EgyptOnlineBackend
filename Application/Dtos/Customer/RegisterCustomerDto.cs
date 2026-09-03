using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Dtos
{
    /// <summary>
    /// Registration DTO for normal customer users
    /// Contains only account/identity details, without worker-specific fields.
    /// </summary>
    public class RegisterCustomerDto
    {
        [Required(ErrorMessage = "First name is required")]
        public string FirstName { get; set; } = string.Empty;

        public string? LastName { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format when provided")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^(010|011|012|015)\d{8}$", ErrorMessage = "Phone number must start with 010, 011, 012, or 015 and be 11 digits long")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Governorate is required")]
        public string Governorate { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; } = string.Empty;

        public string? District { get; set; }

        public string? ReferralUserName { get; set; }
    }
}
