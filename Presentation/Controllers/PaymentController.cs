using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Strategies;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Controllers
{

    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class PaymentController : ControllerBase
    {

        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;

        private readonly UserSubscriptionServices _userSubscriptionService;

        private readonly IPaymentService _paymentService;
        private readonly CreditCardPaymentStrategy _creditCardStrategy;
        private readonly MobileWalletPaymentStrategy _mobileWalletStrategy;
        private readonly FawryPaymentStrategy _fawryPaymentStrategy;


        public PaymentController(ApplicationDbContext context, IUserService service, IPaymentService paymentService, CreditCardPaymentStrategy creditCardStrategy,
        MobileWalletPaymentStrategy mobileWalletStrategy, FawryPaymentStrategy fawryPaymentStrategy, UserSubscriptionServices userSubscription)
        {
            _paymentService = paymentService;
            _context = context;
            _userService = service;
            _creditCardStrategy = creditCardStrategy;
            _mobileWalletStrategy = mobileWalletStrategy;
            _fawryPaymentStrategy = fawryPaymentStrategy;
            _userSubscriptionService = userSubscription;

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

                bool isPaid = await _context.ServiceProviders.AnyAsync(w => w.UserId == userId && w.IsAvailable);
                if (isPaid)
                {
                    return BadRequest(new { message = "User already has an active subscription." });
                }

                // Create pending payment record
                var payment = new PaymentTransaction
                {
                    UserId = userId,
                    Amount = callbackDto.AmountCents ?? 50,
                    PaymentMethod = callbackDto.PaymentMethod,
                    Status = PaymentStatus.Pending,
                    IdempotencyKey = Guid.NewGuid().ToString()
                };

                _context.PaymentTransactions.Add(payment);
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"✅ Payment record created: {payment.Id} for User: {userId}");

                // Create payment session with the payment ID
                string Link;
                if (callbackDto.PaymentMethod == "CreditCard")
                {
                    Link = await _paymentService.CreatePaymentSession(
                         payment.Amount,
                         user,
                         payment.Id,
                         _creditCardStrategy
                    );
                }
                else if (callbackDto.PaymentMethod == "Fawry")
                {
                     Link = await _paymentService.CreatePaymentSession(
                    payment.Amount,
                    user,
                    payment.Id,
                    _fawryPaymentStrategy
                    );
                }
                else
                {
                    Link = await _paymentService.CreatePaymentSession(
                    payment.Amount,
                    user,
                    payment.Id,
                    _mobileWalletStrategy
                    );
                }

                return Ok(new { message = Link, paymentId = payment.Id });
            }

            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing the payment callback.", error = ex.Message });
            }
        }
        // callback function to handle the webhook from paymob after payment
        [HttpPost("webhook")]
        [HttpGet("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PaymobNewWebHook()
        {
            //This is should be done after doing the payment
            try
            {
                Console.WriteLine($"🔔 WEBHOOK RECEIVED at {DateTime.UtcNow}");
                Console.WriteLine($"Request Method: {Request.Method}");

                // Handle both POST (JSON body) and GET (query parameters)
                bool success = false;
                string orderId = "";
                string merchantOrderId = "";
                string message = "";

                if (Request.Method == "GET")
                {
                    // Parse query parameters
                    var query = Request.Query;
                    Console.WriteLine($"Query Parameters: {string.Join(", ", query.Select(x => $"{x.Key}={x.Value}"))}");

                    success = query["success"].ToString().ToLower() == "true";
                    orderId = query["order"].ToString() ?? "";
                    merchantOrderId = query["merchant_order_id"].ToString() ?? "";
                    message = query["data.message"].ToString() ?? "";

                    Console.WriteLine($"✅ Payment Success: {success}");
                    Console.WriteLine($"Order ID: {orderId}");
                    Console.WriteLine($"Merchant Order ID: {merchantOrderId}");
                    Console.WriteLine($"Message: {message}");
                }
                else
                {
                    // Parse JSON body (POST)
                    using (var reader = new StreamReader(Request.Body))
                    {
                        var body = await reader.ReadToEndAsync();
                        Console.WriteLine($"Payload: {body}");

                        if (!string.IsNullOrEmpty(body))
                        {
                            var payload = JsonSerializer.Deserialize<JsonElement>(body);
                            var obj = payload.GetProperty("obj");

                            success = obj.GetProperty("success").GetBoolean();
                            orderId = obj.GetProperty("order").GetProperty("id").GetInt32().ToString();
                            message = obj.GetProperty("data").GetProperty("message").GetString() ?? "";
                            merchantOrderId = obj.GetProperty("order").GetProperty("merchant_order_id").GetString();

                            Console.WriteLine($"✅ Payment Success: {success}");
                        }
                    }
                }

                // Extract paymentId from merchant_order_id (it's now stored as paymentId instead of userId)
                int paymentId = 0;
                if (!string.IsNullOrEmpty(merchantOrderId) && int.TryParse(merchantOrderId, out paymentId))
                {
                    Console.WriteLine($"✅ Parsed Payment ID: {paymentId}");
                }
                else
                {
                    Console.WriteLine($"❌ Could not parse Payment ID from merchant_order_id: {merchantOrderId}");
                    return BadRequest(new { message = "Invalid merchant_order_id format" });
                }

                Console.WriteLine($"Webhook received for Payment ID: {paymentId}");

                // Log all headers
                foreach (var header in Request.Headers)
                {
                    Console.WriteLine($"Header - {header.Key}: {header.Value}");
                }

                if (success)
                {
                    // Fetch payment record from database
                    var payment = await _context.PaymentTransactions
                        .Include(p => p.User)
                        .ThenInclude(u => u.ServiceProvider)
                        .FirstOrDefaultAsync(p => p.Id == paymentId);

                    if (payment == null)
                    {
                        Console.WriteLine($"❌ Payment record not found with ID: {paymentId}");
                        return BadRequest(new { message = "Payment record not found" });
                    }

                    // Idempotency check: prevent double processing
                    if (payment.Status == PaymentStatus.Success)
                    {
                        Console.WriteLine($"✅ Payment already processed (idempotent). Payment ID: {paymentId}");
                        return Ok(new
                        {
                            message = "Payment already processed",
                            paymentId = payment.Id,
                            status = payment.Status.ToString()
                        });
                    }

                    // Update payment status
                    payment.Status = PaymentStatus.Processing;
                    payment.PaymobOrderId = int.TryParse(orderId, out var oid) ? oid : null;
                    payment.PaymobMerchantOrderId = merchantOrderId;
                    payment.PaymentGatewayResponse = message;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ Payment status updated to Processing");

                    // Get user and renew subscription
                    User UserFound = payment.User;
                    if (UserFound != null)
                    {
                        Console.WriteLine($"✅ User Found: {UserFound.Id}");
                        await _userSubscriptionService.RenewSubscription(UserFound);
                        if (UserFound.ServiceProvider != null)
                        {
                            UserFound.ServiceProvider.IsAvailable = true;
                        }
                        
                        // Final status update
                        payment.Status = PaymentStatus.Success;
                        payment.ProcessedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"✅ Subscription Renewed Successfully");
                        
                        return Ok(new
                        {
                            message = "Subscription Renewed Successfully",
                            userId = UserFound.Id,
                            paymentId = payment.Id,
                            status = payment.Status.ToString(),
                            orderId = orderId
                        });
                    }
                    else
                    {
                        Console.WriteLine($"❌ User not found for payment ID: {paymentId}");
                        payment.Status = PaymentStatus.Failed;
                        payment.ErrorMessage = "User not found";
                        payment.ProcessedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        return BadRequest(new { message = "User not found for this payment" });
                    }

                }
                else
                {
                    Console.WriteLine($"❌ Payment failed for Payment ID: {paymentId}, Message: {message}");
                    var payment = await _context.PaymentTransactions.FirstOrDefaultAsync(p => p.Id == paymentId);
                    if (payment != null)
                    {
                        payment.Status = PaymentStatus.Failed;
                        payment.ErrorMessage = message;
                        payment.ProcessedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    return BadRequest(new { message = "Payment failed: " + message, paymentId });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Webhook Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "An error occurred while processing the webhook.", error = ex.Message });
            }
        }
        /// <summary>
        /// Check payment status by payment ID. Can be called without authentication.
        /// </summary>
        [HttpGet("status/{paymentId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPaymentStatus(int paymentId)
        {
            try
            {
                var payment = await _context.PaymentTransactions
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found", paymentId });
                }

                return Ok(new
                {
                    paymentId = payment.Id,
                    status = payment.Status.ToString(),
                    amount = payment.Amount,
                    paymentMethod = payment.PaymentMethod,
                    createdAt = payment.CreatedAt,
                    processedAt = payment.ProcessedAt,
                    userId = payment.UserId,
                    errorMessage = payment.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching payment status", error = ex.Message });
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