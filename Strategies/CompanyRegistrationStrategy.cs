using EgyptOnline.Dtos;
using EgyptOnline.Models;

namespace EgyptOnline.Strategies
{
    public class CompanyRegistrationStrategy : IProviderRegistrationStrategy
    {
        public string? Validate(RegisterWorkerDto model)
        {
            if (model.Business == null || model.Owner == null)
                return "Please Add the Business and the Owner";

            return null;
        }

        public ServicesProvider CreateProvider(RegisterWorkerDto model, User user)
        {
            return new Company
            {
                User = user,
                UserId = user.Id,
                Bio = model.Bio,
                ProviderType = model.ProviderType,
                Owner = model.Owner,
                Business = model.Business,
                IsAvailable = true,
            };
        }
    }
}
