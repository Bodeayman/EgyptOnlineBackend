using EgyptOnline.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; }               // Actual JWT refresh token string
    public string UserId { get; set; }              // Foreign key to User
    public User User { get; set; }                  // Navigation property

    public DateTime Expires { get; set; }           // Expiration date
    public bool IsRevoked { get; set; }             // Revoked manually
    public DateTime Created { get; set; }           // When token was created
    public DateTime? Revoked { get; set; }          // When revoked
}
