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

        /// <summary>
        /// Initiate payment for subscription renewal.
        /// Payment amount and method are determined from configuration and query parameters.
        /// The amount is automatically calculated based on the user's provider type.
        /// </summary>
        /// <param name="paymentMethod">Payment method: "CreditCard" or "MobileWallet"</param>
        [HttpPost("subscribe")]
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> InitiateSubscriptionPayment([FromQuery] PaymentType paymentMethod)
        {
            try
            {
                // Validate payment method
                if (!IsValidPaymentMethod(paymentMethod))
                {
                    return BadRequest(new { message = "طريقة الدفع غير صالحة. المدعوم: CreditCard أو MobileWallet", errorCode = "INVALID_PAYMENT_METHOD" });
                }

                string userId = _userService.GetUserID(User);
                if (userId == null)
                {
                    return Unauthorized(new { message = "يرجى تسجيل الدخول مجدداً", errorCode = "UNAUTHORIZED" });
                }

                User user = await _context.Users
                    .Include(u => u.ServiceProvider)
                    .FirstOrDefaultAsync(p => p.Id == userId);

                if (user == null)
                {
                    return BadRequest(new { message = "المستخدم غير موجود", errorCode = "USER_NOT_FOUND" });
                }

                // Check if user already has active subscription


                // Get subscription cost from centralized configuration (single source of truth)
                string providerType = user.ServiceProvider?.ProviderType ?? "Worker";
                decimal subscriptionCost = ProviderPricingConfig.GetSubscriptionCost(providerType);


                // Create pending payment record
                var payment = new PaymentTransaction
                {
                    UserId = userId,
                    Amount = subscriptionCost,
                    PaymentMethod = paymentMethod.ToString(),
                    Status = PaymentStatus.Pending,
                    IdempotencyKey = Guid.NewGuid().ToString()
                };

                _context.PaymentTransactions.Add(payment);
                await _context.SaveChangesAsync();


                // Create payment session with the payment ID
                string paymentLink;
                if (paymentMethod == PaymentType.CreditCard)
                {
                    paymentLink = await _paymentService.CreatePaymentSession(
                        payment.Amount,
                        user,
                        payment.Id,
                        _creditCardStrategy
                    );
                }
                else // MobileWallet
                {

                    paymentLink = await _paymentService.CreatePaymentSession(
                        payment.Amount,
                        user,
                        payment.Id,
                        _mobileWalletStrategy
                    );
                }

                return Ok(new
                {
                    message = "تم إنشاء جلسة الدفع بنجاح",
                    paymentLink = paymentLink,
                    paymentId = payment.Id,
                    amount = payment.Amount,
                    currency = "EGP",
                    paymentMethod = paymentMethod
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ أثناء معالجة طلب الدفع", errorCode = "INTERNAL_ERROR" });
            }
        }

        /// <summary>
        /// Validate if payment method is supported.
        /// Currently supports: CreditCard, MobileWallet
        /// Note: Fawry and VodafoneCash are not edited as per requirements.
        /// </summary>
        private bool IsValidPaymentMethod(PaymentType paymentMethod)
        {
            return paymentMethod switch
            {
                PaymentType.CreditCard or PaymentType.MobileWallet => true,
                _ => false
            };
        }
        // callback function to handle the webhook from paymob after payment
        [HttpPost("webhook")]
        [HttpGet("webhook")]
        public async Task<IActionResult> PaymobNewWebHook()
        {
            try
            {
                bool success = false;
                string orderId = "";
                string merchantOrderId = "";
                string message = "";

                using (var reader = new StreamReader(Request.Body))
                {
                    var body = await reader.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(body))
                    {
                        var payload = JsonSerializer.Deserialize<JsonElement>(body);
                        var obj = payload.GetProperty("obj");

                        success = obj.GetProperty("success").GetBoolean();
                        orderId = obj.GetProperty("order").GetProperty("id").GetInt32().ToString();
                        message = obj.GetProperty("data").GetProperty("message").GetString() ?? "";
                        merchantOrderId = obj.GetProperty("order").GetProperty("merchant_order_id").GetString();
                    }
                }

                int paymentId = 0;
                if (!(!string.IsNullOrEmpty(merchantOrderId) && int.TryParse(merchantOrderId, out paymentId)))
                    return BadRequest(new { message = "صيغة معرف الطلب غير صحيحة", errorCode = "INVALID_ORDER_ID" });

                if (success)
                {
                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            PaymentTransaction? payment = null;
                            if (_context.Database.IsRelational())
                            {
                                payment = await _context.PaymentTransactions
                                    .FromSqlInterpolated($"SELECT * FROM \"PaymentTransactions\" WHERE \"Id\" = {paymentId} FOR UPDATE")
                                    .Include(p => p.User)
                                        .ThenInclude(u => u.ServiceProvider)
                                    .FirstOrDefaultAsync();
                            }
                            else
                            {
                                payment = await _context.PaymentTransactions
                                    .Include(p => p.User)
                                        .ThenInclude(u => u.ServiceProvider)
                                    .FirstOrDefaultAsync(p => p.Id == paymentId);
                            }

                            if (payment == null)
                            {
                                await transaction.RollbackAsync();
                                return BadRequest(new { message = "سجل الدفع غير موجود", errorCode = "PAYMENT_NOT_FOUND" });
                            }

                            if (payment.Status == PaymentStatus.Success)
                            {
                                await transaction.RollbackAsync();
                                return Ok(new
                                {
                                    message = "تمت معالجة عملية الدفع مسبقاً",
                                    paymentId = payment.Id,
                                    status = payment.Status.ToString()
                                });
                            }

                            payment.Status = PaymentStatus.Processing;
                            payment.PaymobOrderId = int.TryParse(orderId, out var oid) ? oid : null;
                            payment.PaymobMerchantOrderId = merchantOrderId;
                            payment.PaymentGatewayResponse = message;
                            await _context.SaveChangesAsync();

                            User UserFound = payment.User;
                            if (UserFound == null)
                            {
                                payment.Status = PaymentStatus.Failed;
                                payment.ErrorMessage = "User not found";
                                payment.ProcessedAt = DateTime.UtcNow;
                                await _context.SaveChangesAsync();
                                await transaction.RollbackAsync();
                                return BadRequest(new { message = "المستخدم المرتبط بعملية الدفع غير موجود", errorCode = "USER_NOT_FOUND" });
                            }

                            await _userSubscriptionService.RenewSubscription(UserFound);

                            payment.Status = PaymentStatus.Success;
                            payment.ProcessedAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();

                            await transaction.CommitAsync();

                            return Ok(new
                            {
                                message = "تم تجديد الاشتراك بنجاح",
                                userId = UserFound.Id,
                                paymentId = payment.Id,
                                status = payment.Status.ToString(),
                                orderId = orderId
                            });
                        }
                        catch (Exception)
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }
                else
                {
                    var payment = await _context.PaymentTransactions.FirstOrDefaultAsync(p => p.Id == paymentId);
                    if (payment != null)
                    {
                        payment.Status = PaymentStatus.Failed;
                        payment.ErrorMessage = message;
                        payment.ProcessedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    return BadRequest(new { message = "فشلت عملية الدفع", errorCode = "PAYMENT_FAILED", paymentId });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ أثناء معالجة بيانات الدفع", errorCode = "INTERNAL_ERROR" });
            }
        }
        /// <summary>
        /// Check payment status by payment ID. Can be called without authentication.
        /// </summary>
        [HttpGet("status/{paymentId}")]
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> GetPaymentStatus(int paymentId)
        {
            try
            {
                var payment = await _context.PaymentTransactions
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null)
                {
                    return NotFound(new { message = "عملية الدفع غير موجودة", errorCode = "PAYMENT_NOT_FOUND", paymentId });
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
                return StatusCode(500, new { message = "حدث خطأ أثناء جلب حالة الدفع", errorCode = "INTERNAL_ERROR" });
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
