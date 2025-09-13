using EgyptOnline.Data;
using EgyptOnline.Models;

namespace EgyptOnline.Domain.Interfaces
{
    public interface IUserStrategy
    {
        Task AddEntity(User user, ApplicationDbContext context);
    }
}