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
        public async Task<UserRegisterationResult> RegisterUser(RegisterWorkerDto model)
        {
            try
            {
                Console.WriteLine("Sending the model right now");
                foreach (var prop in model.GetType().GetProperties())
                {
                    Console.WriteLine($"{prop.Name}: {prop.GetValue(model)}");
                }

                // Generate unique username
                string UserName = Helper.GenerateUserName(model.FirstName, model.LastName ?? "");
                while (await _context.Users.AnyAsync(u => u.UserName == UserName))
                    UserName = Helper.GenerateUserName(model.FirstName, model.LastName ?? "");

                // Check email only if provided (phone is primary identifier)
                if (!string.IsNullOrWhiteSpace(model.Email) && await _userManager.FindByEmailAsync(model.Email) != null)
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError
                        {
                            Description = "The email has been used before.",
                            Code = UserErrors.EmailAlreadyExists.ToString()
                        })
                    };
                }

                // Check phone
                var phone = EgyptOnline.Utilities.Helper.NormalizePhoneNumber(model.PhoneNumber);
                if (await _context.Users.AnyAsync(u => u.PhoneNumber == phone))
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError
                        {
                            Description = "The phone number has been used before.",
                            Code = UserErrors.PhoneNumberAlreadyExists.ToString()
                        })
                    };
                }

                // Create user object (email optional; phone is primary)
                var user = new User
                {
                    UserName = UserName,
                    Email = string.IsNullOrWhiteSpace(model.Email) ? "" : model.Email,
                    PhoneNumber = phone,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Governorate = model.Governorate,
                    City = model.City,
                    District = model.District,
                    ReferrerUserName = model.ReferralUserName,
                    ReferralRewardCount = 0
                };

                // Create the user via Identity
                var createResult = await _userManager.CreateAsync(user, model.Password);
                if (!createResult.Succeeded)
                {
                    return new UserRegisterationResult
                    {
                        Result = createResult,
                        User = null
                    };
                }

                // Add subscription
                var subscription = _userSubscription.AddSubscriptionForANewUser(user);
                if (subscription == null)
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError
                        {
                            Description = "Subscription creation failed.",
                            Code = UserErrors.GeneralError.ToString()
                        })
                    };
                }

                // Referral points
                if (!string.IsNullOrEmpty(model.ReferralUserName))
                {
                    var pointsAdded = _userPointService.AddPointsToUser(model.ReferralUserName, model.ProviderType);
                    /*if (!pointsAdded)
                    {
                        return new UserRegisterationResult
                        {
                            Result = IdentityResult.Failed(new IdentityError
                            {
                                Description = "Referral User Name is invalid.",
                                Code = UserErrors.ReferralUserNotFound.ToString()
                            })
                        };
                    } */
                }

                var isCustomer = !string.IsNullOrWhiteSpace(model.ProviderType) &&
                                 model.ProviderType.Equals("Customer", StringComparison.OrdinalIgnoreCase);

                var roleToAssign = isCustomer ? Roles.Customer : Roles.User;
                await _userManager.AddToRoleAsync(user, roleToAssign);

                // Ensure user has a digital wallet (without a number)
                var wallet = new UserWallet
                {
                    UserId = user.Id,
                    FreeBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserWallets.Add(wallet);
                await _context.SaveChangesAsync();

                // Everything is good
                return new UserRegisterationResult
                {
                    Result = IdentityResult.Success,
                    User = user
                };
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

        /// <summary>
        /// Register a normal customer user without worker provider profiles.
        /// </summary>
        public async Task<UserRegisterationResult> RegisterCustomer(RegisterCustomerDto model)
        {
            try
            {
                // Generate unique username
                string userName = Helper.GenerateUserName(model.FirstName, model.LastName ?? "");
                while (await _context.Users.AnyAsync(u => u.UserName == userName))
                    userName = Helper.GenerateUserName(model.FirstName, model.LastName ?? "");

                // Check email if provided
                if (!string.IsNullOrWhiteSpace(model.Email) && await _userManager.FindByEmailAsync(model.Email) != null)
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError
                        {
                            Description = "The email has been used before.",
                            Code = UserErrors.EmailAlreadyExists.ToString()
                        })
                    };
                }

                // Check phone number
                var phone = EgyptOnline.Utilities.Helper.NormalizePhoneNumber(model.PhoneNumber);
                if (await _context.Users.AnyAsync(u => u.PhoneNumber == phone))
                {
                    return new UserRegisterationResult
                    {
                        Result = IdentityResult.Failed(new IdentityError
                        {
                            Description = "The phone number has been used before.",
                            Code = UserErrors.PhoneNumberAlreadyExists.ToString()
                        })
                    };
                }

                // Create customer user
                var user = new User
                {
                    UserName = userName,
                    Email = string.IsNullOrWhiteSpace(model.Email) ? "" : model.Email,
                    PhoneNumber = phone,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Governorate = model.Governorate,
                    City = model.City,
                    District = model.District,
                    ReferrerUserName = model.ReferralUserName,
                    ReferralRewardCount = 0
                };

                var createResult = await _userManager.CreateAsync(user, model.Password);
                if (!createResult.Succeeded)
                {
                    return new UserRegisterationResult
                    {
                        Result = createResult,
                        User = null
                    };
                }

                // Add subscription
                _userSubscription.AddSubscriptionForANewUser(user);

                // Assign Customer role
                await _userManager.AddToRoleAsync(user, Roles.Customer);

                // Initialize wallet
                var wallet = new UserWallet
                {
                    UserId = user.Id,
                    FreeBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserWallets.Add(wallet);
                await _context.SaveChangesAsync();

                return new UserRegisterationResult
                {
                    Result = IdentityResult.Success,
                    User = user
                };
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
