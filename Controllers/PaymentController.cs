using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Interfaces;
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

        private readonly IPaymentService _paymentService;

        private readonly IConfiguration _config;

        public PaymentController(ApplicationDbContext context, UtilitiesClass utils, IPaymentService paymentService, IConfiguration config)
        {
            _paymentService = paymentService;
            _context = context;
            _utils = utils;
            _config = config;
        }
        [HttpPost("callback")]
        public async Task<IActionResult> PaymentCallback([FromBody] PaymentCallbackDto callbackDto)
        {
            try
            {
                string Link = await _paymentService.CreatePaymentSession(callbackDto.AmountCents ?? 0, callbackDto.OrderId); // The hell
                return Ok(Link);
            }

            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing the payment callback.", error = ex.Message });
            }
        }

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