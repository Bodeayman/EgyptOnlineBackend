using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Strategies;
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
        private readonly IUserService _userService;

        private readonly IPaymentService _paymentService;
        private readonly CreditCardPaymentStrategy _creditCardStrategy;
        private readonly MobileWalletPaymentStrategy _mobileWalletStrategy;


        public PaymentController(ApplicationDbContext context, IUserService service, IPaymentService paymentService, CreditCardPaymentStrategy creditCardStrategy,
        MobileWalletPaymentStrategy mobileWalletStrategy)
        {
            _paymentService = paymentService;
            _context = context;
            _userService = service;
            _creditCardStrategy = creditCardStrategy;
            _mobileWalletStrategy = mobileWalletStrategy;

        }

        // This function returns the iframe that we will call the payment from
        [HttpPost("callback")]
        public async Task<IActionResult> PaymentCallback([FromBody] PaymentCallbackDto callbackDto)
        {
            try
            {
                string userId = _userService.GetUserID(User);
                if (userId == null)
                {
                    return Unauthorized(new { message = "You should sign in again" });
                }
                User user = await _context.Users.FirstOrDefaultAsync(p => p.Id == userId);
                if (user == null)
                {
                    return BadRequest(new { message = "The user is not found" });

                }
                var ServicesProvider = await _context.ServiceProviders.FirstOrDefaultAsync(sp => sp.UserId == userId);
                bool isPaid = await _context.ServiceProviders.AnyAsync(w => w.UserId == userId && w.IsAvailable);
                if (isPaid)
                {
                    return BadRequest(new { message = "User already has an active subscription." });
                }
                string Link;
                if (callbackDto.PaymentMethod == "CreditCard")
                {
                    Link = await _paymentService.CreatePaymentSession(
                         callbackDto.AmountCents ?? 50,
                         user
                        , _creditCardStrategy

                         );
                }
                else
                {
                    Link = await _paymentService.CreatePaymentSession(
                    callbackDto.AmountCents ?? 50,
                    user
                    , _mobileWalletStrategy
                    );
                }

                return Ok(new { message = Link });
            }

            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing the payment callback.", error = ex.Message });
            }
        }
        // callback function to handle the webhook from paymob after payment
        [HttpPost("webhook")]
        public async Task<IActionResult> PaymobNewWebHook([FromBody] JsonElement payload)
        {
            //This is should be done after doing the payment
            try
            {
                var obj = payload.GetProperty("obj");

                bool success = obj.GetProperty("success").GetBoolean();
                string orderId = obj.GetProperty("order").GetProperty("id").GetInt32().ToString();
                string message = obj.GetProperty("data").GetProperty("message").GetString() ?? "";
                Console.WriteLine(obj.ToString());
                foreach (var header in Request.Headers)
                {
                    Console.WriteLine($"{header.Key}: {header.Value}");
                }

                string userId = "";

                Console.WriteLine(userId);


                if (success)
                {
                    bool workerFound = await _context.Workers.AnyAsync(p => p.UserId == userId);
                    if (workerFound)
                    {

                        Worker worker = await _context.Workers.FirstAsync(p => p.UserId == userId);
                        worker.IsAvailable = true;
                        await _context.SaveChangesAsync();
                        return Ok(worker);
                    }
                    return Ok(new { message = "Payment processed successfully", orderId });
                }
                else
                {
                    Console.WriteLine($"Payment failed for Order ID: {orderId}");
                    return BadRequest(new { message = "Payment failed, Please retry paying again soon", orderId });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing webhook: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while processing the webhook.", error = ex.Message });
            }
        }
        [HttpPost("pay-mobile-wallet")]
        public async Task<IActionResult> PayMobileWallet()
        {
            return Ok();
        }
        // Add types of payment to the user inventory

        // Get all payment methods of the user/worker

    }
}