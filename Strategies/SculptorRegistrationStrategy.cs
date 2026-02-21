using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Utilities;

namespace EgyptOnline.Strategies
{
    public class SculptorRegistrationStrategy : IProviderRegistrationStrategy
    {
        public string? Validate(RegisterWorkerDto model)
        {


            if (model.Marketplace == null)
                return "Please Add the Marketplace";
            return null;
        }

        public ServicesProvider CreateProvider(RegisterWorkerDto model, User user)
        {
            return new Sculptor
            {
                User = user,
                UserId = user.Id,
                Bio = model.Bio,
                WorkerType = (WorkerTypes)model.WorkerType,
                ProviderType = model.ProviderType,
                ServicePricePerDay = model.Pay ?? 0,
                IsAvailable = true,
                MarketPlace = model.Marketplace,
            };
        }
    }
}
