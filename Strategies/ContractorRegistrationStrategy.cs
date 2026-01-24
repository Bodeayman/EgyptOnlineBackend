using EgyptOnline.Dtos;
using EgyptOnline.Models;

namespace EgyptOnline.Strategies
{
    public class ContractorRegistrationStrategy : IProviderRegistrationStrategy
    {
        public string? Validate(RegisterWorkerDto model)
        {
            if (model.Specialization == null)
                return "Please Add the Specialization";

            return null;
        }

        public ServicesProvider CreateProvider(RegisterWorkerDto model, User user)
        {
            return new Contractor
            {
                User = user,
                UserId = user.Id,
                Bio = model.Bio,
                Specialization = model.Specialization,
                ProviderType = model.ProviderType,
                IsAvailable = true,
                Salary = model.Pay ?? 0,
            };
        }
    }
}
