using EgyptOnline.Dtos;
using EgyptOnline.Models;

namespace EgyptOnline.Strategies
{
    public class MarketPlaceRegistrationStrategy : IProviderRegistrationStrategy
    {
        public string? Validate(RegisterWorkerDto model)
        {
            if (model.Business == null || model.Owner == null)
                return "Please Add the Business and the Owner";

            return null;
        }

        public ServicesProvider CreateProvider(RegisterWorkerDto model, User user)
        {
            return new MarketPlace
            {
                User = user,
                UserId = user.Id,
                Bio = model.Bio,
                ProviderType = model.ProviderType,
                Business = model.Business,
                IsAvailable = true,
                Owner = model.Owner,
            };
        }
    }
}
