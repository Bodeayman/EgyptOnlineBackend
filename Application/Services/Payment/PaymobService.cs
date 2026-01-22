using System.Text.Json;
using EgyptOnline.Domain.Interfaces;

using EgyptOnline.Models;
using EgyptOnline.Strategies;
using EgyptOnline.Utilities;

namespace EgyptOnline.Services
{
    public class PaymobService : IPaymentService
    {
        public async Task<string> CreatePaymentSession(decimal amount, User user, int paymentId, IPaymentStrategy strategy)
        {
            try
            {
                var result = await strategy.PayAsync(amount, user, paymentId);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating payment session: {ex.Message}");
            }
        }

    }
}