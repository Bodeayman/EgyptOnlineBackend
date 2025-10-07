using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Identity;

namespace EgyptOnline.Services
{
    public class UserRegisterationService
    {
        private readonly UserManager<User> _userManager;
        private readonly UserSubscriptionServices _userSubscription;
        private readonly ApplicationDbContext _context;

        public UserRegisterationService(UserManager<User> userManager, UserSubscriptionServices userSubscription, ApplicationDbContext context)
        {
            _userManager = userManager;
            _userSubscription = userSubscription;
            _context = context;
        }
        public async Task<UserRegisterationResult> RegisterUser(RegisterWorkerDto model)
        {
            try
            {
                string UserType;
                if (model.UserType == "User")
                {
                    UserType = "User";
                }
                else
                {
                    UserType = "SP";
                }
                string UserName = Helper.GenerateUserName(model.FirstName, model.LastName);
                var user = new
                User
                {
                    UserName = UserName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    UserType = model.UserType,
                    FirstName = model.FirstName,
                    Location = model.Location,
                    LastName = model.LastName,
                    NormalizedUserName = model.Email.ToUpperInvariant(),
                    NormalizedEmail = model.Email.ToUpperInvariant(),
                    SecurityStamp = Guid.NewGuid().ToString("D"),
                };
                if (await _userManager.FindByEmailAsync(model.Email) != null)
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError { Description = "The email has been used before." }),
                        User = null
                    };
                }
                Console.WriteLine("Subscription is returned here");

                var sub = _userSubscription.AddSubscription(user);
                if (sub == null)
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError { Description = "User creation failed." }),
                        User = null
                    };
                }
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
                    Result = IdentityResult.Failed(new IdentityError { Description = "User creation failed." }),
                    User = null
                };
            }

        }

    }
}