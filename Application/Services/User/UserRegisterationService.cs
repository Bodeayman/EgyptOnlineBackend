using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Services
{
    public class UserRegisterationService
    {
        private readonly UserManager<User> _userManager;
        private readonly UserSubscriptionServices _userSubscription;
        private readonly ApplicationDbContext _context;
        private readonly UserPointService _userPointService;

        public UserRegisterationService(UserManager<User> userManager, UserPointService userPointService, UserSubscriptionServices userSubscription, ApplicationDbContext context)
        {
            _userManager = userManager;
            _userSubscription = userSubscription;
            _context = context;
            _userPointService = userPointService;
        }
        public async Task<UserRegisterationResult> RegisterUser(RegisterWorkerDto model, IFormFile image)
        {
            try
            {
                Console.WriteLine("Sending the model right now");
                foreach (var prop in model.GetType().GetProperties())
                {
                    Console.WriteLine($"{prop.Name}: {prop.GetValue(model)}");
                }
                string UserName = Helper.GenerateUserName(model.FirstName, model.LastName ?? "");

                while (await _context.Users.AnyAsync(u => u.UserName == UserName))
                {
                    //Trying generating a new one , a unique one so it doesn't overkill the device
                    UserName = Helper.GenerateUserName(model.FirstName, model.LastName ?? "");
                }
                var user = new
                User
                {
                    UserName = UserName,
                    Email = model.Email,
                    PhoneNumber = $"+2{model.PhoneNumber}",
                    FirstName = model.FirstName,
                    Governorate = model.Governorate,
                    City = model.City,
                    District = model.District,
                    LastName = model.LastName,
                    NormalizedUserName = model.Email.ToUpperInvariant(),
                    NormalizedEmail = model.Email.ToUpperInvariant(),
                    SecurityStamp = Guid.NewGuid().ToString("D"),


                };
                Console.WriteLine("Checking if email or phone number exists");
                if (await _userManager.FindByEmailAsync(model.Email) != null)
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError { Description = "The email has been used before." }),
                        User = null
                    };
                }
                if (await _context.Users.AnyAsync(u => u.PhoneNumber == user.PhoneNumber))
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError { Description = "The phone number has been used before." }),
                        User = null
                    };
                }
                Console.WriteLine("Subscription is returned here");

                var sub = _userSubscription.AddSubscriptionForANewUser(user);
                bool pointsAdded = false;
                if (model.ReferralUserName != null)
                {
                    pointsAdded =
                 _userPointService.AddPointsToUser(model.ReferralUserName);
                    Console.WriteLine(model.ReferralUserName);
                    if (!pointsAdded)
                    {
                        return new UserRegisterationResult
                        {
                            Result = IdentityResult.Failed(new IdentityError { Description = "Referral User Name is invalid." }),
                            User = null
                        };
                    }
                }
                Console.WriteLine("Subscription returned");
                if (sub == null)
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError { Description = "User creation failed." }),
                        User = null
                    };
                }
                Console.WriteLine("Hashing Password");
                var passwordHasher = new PasswordHasher<User>();
                user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
                var userModel = _context.Users.Add(user);

                if (userModel == null)
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError { Description = "User creation failed." }),
                        User = null
                    };
                }
                else
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Success,
                        User = user
                    };
                }


            }
            catch (Exception ex)
            {
                return new UserRegisterationResult
                {
                    Result = IdentityResult.Failed(new IdentityError { Description = ex.Message }),
                    User = null
                };
            }

        }

    }
}