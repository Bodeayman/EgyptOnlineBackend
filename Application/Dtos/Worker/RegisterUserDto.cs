using System.ComponentModel.DataAnnotations;
using EgyptOnline.Models;
using EgyptOnline.Utilities;

namespace EgyptOnline.Dtos
{
  public class RegisterWorkerDto
  {
    [Required]
    public string FirstName { get; set; } = string.Empty;

    public string? LastName { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;


    [Required]

    public string PhoneNumber { get; set; } = string.Empty;


    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Governorate { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    public string? District { get; set; }

    public string? Bio { get; set; }

    public string? ProviderType { get; set; } = "Worker";

    // Worker,Assistant attirbute
    public string? Skill { get; set; }

    public string? DerivedSpec { get; set; }
    public string? Marketplace { get; set; }


    // WorkerType as int for Swagger
    public int WorkerType { get; set; } = 0; // 0 = PerDay, 1 = Fixed

    // Company
    public string? Business { get; set; }
    public string? Owner { get; set; }

    public decimal? Pay { get; set; } = 0;
    // Contractor
    public string? Specialization { get; set; }

    public string? ReferralUserName { get; set; } = null;

    public bool IsOAuth { get; set; } = false;
    public string? GoogleId { get; set; } = null;
  }

}

