using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Interfaces;
using EgyptOnline.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {

        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;

        private readonly IPaymentService _paymentService;


        public PaymentController(ApplicationDbContext context, IUserService service, IPaymentService paymentService)
        {
            _paymentService = paymentService;
            _context = context;
            _userService = service;
        }

        // This function returns the iframe that we will call the payment from
        [HttpPost("callback")]
        public async Task<IActionResult> PaymentCallback([FromBody] PaymentCallbackDto callbackDto)
        {
            try
            {
                string Link = await _paymentService.CreatePaymentSession(
                 callbackDto.AmountCents,
                 callbackDto.OrderId,
                 callbackDto.currency
                 ); // The hell
                return Ok(Link);
            }

            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing the payment callback.", error = ex.Message });
            }
        }
        // callback function to handle the webhook from paymob after payment
        [HttpPost("webhook")]
        public IActionResult PaymobNewWebHook([FromBody] JsonElement payload)
        {
            try
            {
                var obj = payload.GetProperty("obj");

                bool success = obj.GetProperty("success").GetBoolean();
                string orderId = obj.GetProperty("order").GetProperty("id").GetInt32().ToString();
                string message = obj.GetProperty("data").GetProperty("message").GetString() ?? "";

                Console.WriteLine($"Webhook received for Order ID: {orderId}, Success: {success}, Message: {message}");

                if (success)
                {
                    Console.WriteLine($"Payment succeeded for Order ID: {orderId}");
                    return Ok(new { message = "Payment processed successfully", orderId });
                }
                else
                {
                    Console.WriteLine($"Payment failed for Order ID: {orderId}");
                    return BadRequest(new { message = "Payment failed", orderId });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing webhook: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while processing the webhook.", error = ex.Message });
            }

        }
        // Add types of payment to the user inventory
        [HttpPost("addPayment")]
        public async Task<IActionResult> AddPayment([FromBody] CreatePaymentDto paymentDto)
        {
            var userId = _userService.GetUserID(User);
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
        // Get all payment methods of the user/worker
        [HttpGet("allPayments")]
        public async Task<IActionResult> GetAllPayments()
        {
            var userId = _userService.GetUserID(User);

            var payments = await _context.Payments.Where(p => p.WorkerId == userId).ToListAsync();

            return Ok(payments);
        }
    }
}