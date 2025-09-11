using EgyptOnline.Models;

namespace EgyptOnline.Interfaces
{
    public interface IPaymentRepository
    {
        Task<IEnumerable<Payment>> GetAllPaymentsAsync();
        Task AddPaymentAsync(Payment payment);
    }
}