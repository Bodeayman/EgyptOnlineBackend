using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Identity;

namespace EgyptOnline.Services
{
    public class UserRegisterationService
    {
        private readonly UserManager<User> _userManager;
        public UserRegisterationService(UserManager<User> userManager)
        {
            _userManager = userManager;
        }
        public async Task<UserRegisterationResult> registerUser(RegisterWorkerDto model)
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
                LastName = model.LastName
            };
            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                return new UserRegisterationResult
                {
                    Result = IdentityResult.Failed(),
                    User = null
                };
            }
            var result = await _userManager.CreateAsync(user, model.Password);
            return new UserRegisterationResult
            {
                Result = IdentityResult.Success,
                User = user
            }; ;
        }
    }
}