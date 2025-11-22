using EgyptOnline.Data;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Services
{
    public class UserPointService
    {
        private readonly ApplicationDbContext _context;
        public UserPointService(ApplicationDbContext context)
        {
            _context = context;

        }
        public bool AddPointsToUser(string userId)
        {
            var user = _context.Users.Include(u => u.ServiceProvider).FirstOrDefault(u => u.UserName == userId);
            if (user != null)
            {
                if (user.ServiceProvider.ProviderType == "Worker")
                    user.Points += 25;
                else if (user.ServiceProvider.ProviderType == "Company" || user.ServiceProvider.ProviderType == "Marketplace")
                    user.Points += 100;
                else if (user.ServiceProvider.ProviderType == "Engineer" || user.ServiceProvider.ProviderType == "Contractor")
                    user.Points += 50;

                return true;
            }
            return false;
        }
    }
}