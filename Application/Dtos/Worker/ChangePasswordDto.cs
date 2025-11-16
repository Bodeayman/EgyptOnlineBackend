namespace EgyptOnline.Dtos
{



    public class ChangePasswordDto
    {
        /// <summary>
        /// The current password of the user (for verification)
        /// </summary>
        public string CurrentPassword { get; set; } = null!;

        /// <summary>
        /// The new password the user wants to set
        /// </summary>
        public string NewPassword { get; set; } = null!;

        /// <summary>
        /// Optional: confirm new password (frontend usually checks this)
        /// </summary>
        public string? ConfirmPassword { get; set; }
    }

}