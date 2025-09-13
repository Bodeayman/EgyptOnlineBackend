using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Models;

namespace EgyptOnline.Strategies
{
    public class DoctorStrategy : IUserStrategy
    {
        public async Task AddEntity(User user, ApplicationDbContext context)
        {
            Worker worker = user as Worker ?? new Worker();
            await context.Workers.AddAsync(worker);
        }
    }
}