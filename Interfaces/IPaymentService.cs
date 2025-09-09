namespace EgyptOnline.Interfaces
{
    public interface IPaymentService
    {
        public Task<string> CreatePaymentSession(decimal amount, string orderId);
    }
}