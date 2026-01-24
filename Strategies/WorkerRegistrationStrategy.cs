using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Utilities;

namespace EgyptOnline.Strategies
{
    public class WorkerRegistrationStrategy : IProviderRegistrationStrategy
    {
        public string? Validate(RegisterWorkerDto model)
        {
            if (model.Skill == null)
                return "Please Add the Skill";

            return null;
        }

        public ServicesProvider CreateProvider(RegisterWorkerDto model, User user)
        {
            return new Worker
            {
                User = user,
                UserId = user.Id,
                Bio = model.Bio,
                WorkerType = (WorkerTypes)model.WorkerType,
                Skill = model.Skill,
                ProviderType = model.ProviderType,
                ServicePricePerDay = model.Pay ?? 0,
                IsAvailable = true,
            };
        }
    }
}
