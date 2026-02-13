using EgyptOnline.Dtos;
using EgyptOnline.Models;

namespace EgyptOnline.Strategies
{
    public class AssistantRegistrationStrategy : IProviderRegistrationStrategy
    {
        public string? Validate(RegisterWorkerDto model)
        {
            if (model.Skill == null)
                return "Please Add the Skill";
            if (model.DerivedSpec == null)
                return "Please Add the Derived Specialization";
            if (model.Marketplace == null)
                return "Please Add the Marketplace";

            return null;
        }

        public ServicesProvider CreateProvider(RegisterWorkerDto model, User user)
        {
            return new Assistant
            {
                User = user,
                UserId = user.Id,
                Bio = model.Bio,
                Skill = model.Skill,
                ProviderType = model.ProviderType,
                ServicePricePerDay = model.Pay ?? 0,
                IsAvailable = true,
                DerivedSpec = model.DerivedSpec,
                MarketPlace = model.Marketplace,
            };
        }
    }
}
