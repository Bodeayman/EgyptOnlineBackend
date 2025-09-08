using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {

        private readonly ApplicationDbContext _context;
        private readonly UtilitiesClass _utils;

        public PaymentController(ApplicationDbContext context, UtilitiesClass utils)
        {
            _context = context;
            _utils = utils;
        }
        [HttpPost]
        public IActionResult ProcessPayment()
        {
            // Payment processing logic will go here
            return Ok(new { message = "Payment processed successfully!" });
        }
        [HttpPost("addPayment")]
        public async Task<IActionResult> AddPayment([FromBody] CreatePaymentDto paymentDto)
        {
            var userId = _utils.GetUserID(User);
            Console.WriteLine(userId);


            // return Ok(userId);
            var payment = await _context.Payments.AddAsync(new Payment
            {

                WorkerId = userId,
                PaymentType = paymentDto.PaymentType,
                PaymentCode = paymentDto.PaymentCode
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Payment Method added successfully!" });

        }

        [HttpGet("allPayments")]
        public async Task<IActionResult> GetAllPayments()
        {
            var userId = _utils.GetUserID(User);

            var payments = await _context.Payments.Where(p => p.WorkerId == userId).ToListAsync();

            return Ok(payments);
        }
    }
}