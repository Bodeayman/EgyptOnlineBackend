using EgyptOnline.Data;

namespace EgyptOnline.Services
{
    public class UserPointService
    {
        private readonly ApplicationDbContext _context;
        private readonly int PointsToAdd = 100;
        public UserPointService(ApplicationDbContext context)
        {
            _context = context;

        }
        public bool AddPointsToUser(string userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserName == userId);
            if (user != null)
            {
                user.Points += PointsToAdd;
                return true;
            }
            return false;
        }
    }
}