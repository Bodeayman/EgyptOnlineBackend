using EgyptOnline.Dtos;
using EgyptOnline.Models;

namespace EgyptOnline.Strategies
{
    public class EngineerRegistrationStrategy : IProviderRegistrationStrategy
    {
        public string? Validate(RegisterWorkerDto model)
        {
            if (model.Specialization == null)
                return "Please Add the Specialization";

            return null;
        }

        public ServicesProvider CreateProvider(RegisterWorkerDto model, User user)
        {
            return new Engineer
            {
                User = user,
                UserId = user.Id,
                Bio = model.Bio,
                ProviderType = model.ProviderType,
                Salary = model.Pay ?? 0,
                Specialization = model.Specialization,
                IsAvailable = true,
            };
        }
    }
}
