using EgyptOnline.Models;

namespace EgyptOnline.Domain.Interfaces
{
    public interface IPaymentService
    {
        public Task<string> CreatePaymentSession(decimal? amount, string orderId, Worker worker, string currency = "EGP");
    }
}