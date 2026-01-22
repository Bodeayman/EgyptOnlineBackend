using EgyptOnline.Models;
using EgyptOnline.Strategies;
using EgyptOnline.Utilities;

namespace EgyptOnline.Domain.Interfaces
{
    public interface IPaymentService
    {
        public Task<string> CreatePaymentSession(decimal amount, User user, int paymentId, IPaymentStrategy paymentStrategy);
    }
}