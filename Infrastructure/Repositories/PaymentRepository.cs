using EgyptOnline.Data;
using EgyptOnline.Application.Interfaces;
using EgyptOnline.Models;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly ApplicationDbContext _context;
        public PaymentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

    }

}