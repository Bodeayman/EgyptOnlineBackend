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
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Location = user.Location,

                PhoneNumber = user.PhoneNumber,
                ImageUrl = user.ImageUrl,
                UserType = user.UserType,
                Subscription = user.Subscription?.ToSubscriptionDto(),
                ServiceProvider = user.ServiceProvider?.ToServiceProviderDto(),
                Points = user.Points,


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
            Console.WriteLine(serviceProvider.GetType());

            var dto = new ServiceProviderDto
            {
                Id = serviceProvider.Id,
                IsAvailable = serviceProvider.IsAvailable,
                Bio = serviceProvider.Bio,
                ProviderType = serviceProvider.ProviderType,

            };

            // Set specialization based on provider type
            switch (serviceProvider)
            {
                case Worker worker:
                    dto.Specialization = worker.Skill;
                    dto.Pay = worker.ServicePricePerDay;
                    dto.WorkerTypes = worker.WorkerType;
                    break;
                case Contractor contractor:
                    // Assuming Contractor has an Expertise property
                    dto.Pay = contractor.Salary;
                    dto.Specialization = contractor.Specialization;
                    break;
                case Company company:
                    // Assuming Company has a Services property
                    dto.Owner = company.Owner;
                    dto.Business = company.Business;
                    break;
                case Engineer engineer:
                    dto.Specialization = engineer.Specialization;
                    dto.Pay = engineer.Salary;
                    break;
                case MarketPlace marketPlace:
                    dto.Business = marketPlace.Business;
                    break;
                default:
                    dto.Specialization = null;
                    break;
            }

            return dto;
        }
    }
}