using EgyptOnline.Dtos;
using EgyptOnline.Models;

namespace EgyptOnline.Utilities
{
    public static class ProfileMappingExtensions
    {
        public static ShowProfileDto ToShowProfileDto(this User user)
        {
            var dto = new ShowProfileDto
            {
                Id = user.Id,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Location = user.Location,

                PhoneNumber = user.PhoneNumber,
                ImageUrl = user.ImageUrl,
                UserType = user.UserType,
                Subscription = user.Subscription?.ToSubscriptionDto(),
                ServiceProvider = user.ServiceProvider?.ToServiceProviderDto()
            };

            return dto;
        }

        private static SubscriptionDto ToSubscriptionDto(this Subscription subscription)
        {
            return new SubscriptionDto
            {
                Id = subscription.Id,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                IsActive = subscription.IsActive
            };
        }

        private static ServiceProviderDto ToServiceProviderDto(this ServicesProvider serviceProvider)
        {
            var dto = new ServiceProviderDto
            {
                Id = serviceProvider.Id,
                IsAvailable = serviceProvider.IsAvailable,
                Bio = serviceProvider.Bio,
                ProviderType = serviceProvider.ProviderType
            };

            // Set specialization based on provider type
            switch (serviceProvider)
            {
                case Worker worker:
                    dto.Specialization = worker.Skill;
                    dto.SpecializationType = "Skill";
                    break;
                case Contractor contractor:
                    // Assuming Contractor has an Expertise property
                    dto.Specialization = contractor.GetType().GetProperty("Expertise")?.GetValue(contractor)?.ToString();
                    dto.SpecializationType = "Expertise";
                    break;
                case Company company:
                    // Assuming Company has a Services property
                    dto.Specialization = company.GetType().GetProperty("Services")?.GetValue(company)?.ToString();
                    dto.SpecializationType = "Services";
                    break;
                default:
                    dto.Specialization = null;
                    dto.SpecializationType = null;
                    break;
            }

            return dto;
        }
    }
}