using System.ComponentModel.DataAnnotations;
using EgyptOnline.Models;
using EgyptOnline.Utilities;

namespace EgyptOnline.Dtos
{
  public class RegisterWorkerDto
  {
    public required string FirstName { get; set; } = string.Empty;

    public string? LastName { get; set; }



    //The Annotations check the data during the runtime
    [EmailAddress]
    public required string Email { get; set; } = string.Empty;


    //For the Egyptain Phone Number
    // [RegularExpression(@"^\+20\d{10}$", ErrorMessage = "Phone number must start with +20 and contain 11 digits total.")]
    public required string PhoneNumber { get; set; } = string.Empty;
    public required string Password { get; set; } = string.Empty;




    //insteadd of the location i want to add the governorate and the city and the district
    public required string Governorate { get; set; }
    public required string City { get; set; }
    public string? District { get; set; }


    //SP related
    public string? Bio { get; set; }

    public string? ProviderType { get; set; } = "Worker";

    //Worker
    public string? Skill { get; set; }
    public WorkerTypes WorkerType { get; set; } = WorkerTypes.PerDay;

    //Company
    public string? Business { get; set; }

    public string? Owner { get; set; }
    public decimal? Pay { get; set; } = 0;
    //Contractor
    public string? Specialization { get; set; }

    public string? ReferralUserName { get; set; }


  }
}

