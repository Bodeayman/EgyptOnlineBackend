using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class GooglePlayBillingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly UserSubscriptionServices _userSubscriptionService;

        public GooglePlayBillingController(
            ApplicationDbContext context,
            IUserService userService,
            UserSubscriptionServices userSubscriptionService)
        {
            _context = context;
            _userService = userService;
            _userSubscriptionService = userSubscriptionService;
        }

        /// <summary>
        /// Verify a Google Play subscription purchase and renew the user's subscription.
        /// This endpoint is called by the mobile app after a successful purchase using
        /// Google Play Billing on the device.
        /// </summary>
        [HttpPost("verify-subscription")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyAndActivateSubscription([FromBody] GooglePlaySubscriptionVerificationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            string userId = _userService.GetUserID(User);
            if (userId == null)
            {
                return Unauthorized(new { message = "You should sign in again" });
            }

            var user = await _context.Users
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return BadRequest(new { message = "The user is not found" });
            }

            // Here you must verify the purchase token with Google Play Developer API
            // (purchases.subscriptionsv2.get or purchases.subscriptions.get depending on the API you use).
            // Until that is implemented, we fail the request explicitly to avoid accepting
            // unverified purchases.
            var verificationResult = await VerifyWithGooglePlayAsync(request);
            if (!verificationResult.IsValid)
            {
                return BadRequest(new
                {
                    message = "Google Play subscription is not valid",
                    reason = verificationResult.FailureReason
                });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                string providerType = user.ServiceProvider?.ProviderType ?? "Worker";
                decimal subscriptionCost = ProviderPricingConfig.GetSubscriptionCost(providerType);

                var payment = new PaymentTransaction
                {
                    UserId = userId,
                    Amount = subscriptionCost,
                    PaymentMethod = "GooglePlay",
                    Status = PaymentStatus.Processing,
                    IdempotencyKey = Guid.NewGuid().ToString(),
                    PaymentGatewayResponse = verificationResult.RawResponseSnippet
                };

                _context.PaymentTransactions.Add(payment);
                await _context.SaveChangesAsync();

                var subscription = await _userSubscriptionService.RenewSubscription(user);

                if (subscription == null)
                {
                    payment.Status = PaymentStatus.Failed;
                    payment.ErrorMessage = "Subscription could not be renewed";
                    payment.ProcessedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    await transaction.RollbackAsync();

                    return StatusCode(500, new
                    {
                        message = "Subscription could not be renewed after successful Google Play verification"
                    });
                }

                payment.Status = PaymentStatus.Success;
                payment.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Subscription renewed successfully via Google Play",
                    userId = user.Id,
                    paymentId = payment.Id,
                    status = payment.Status.ToString(),
                    subscriptionEndDate = subscription.EndDate
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    message = "An error occurred while verifying the Google Play subscription.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Verifies the purchase with Google Play Developer API (server-to-server).
        /// Currently a STUB: it does not call Google yet, so it always returns IsValid = false.
        /// That way no one can send a fake purchaseToken and get a free subscription.
        /// When you implement the real call (see GooglePlayBillingFlow.md), this method will
        /// call Google's API and return IsValid = true only when Google confirms the purchase.
        /// </summary>
        private Task<GooglePlayVerificationResult> VerifyWithGooglePlayAsync(GooglePlaySubscriptionVerificationRequest request)
        {
            // TODO: Call Google Play Developer API (purchases.subscriptionsv2.get or
            // purchases.subscriptions.get) with packageName, productId, purchaseToken.
            // Validate: correct package/product, subscription active, acknowledged, not refunded.
            // Use idempotency (e.g. store purchaseToken) so the same token cannot activate twice.

            var result = new GooglePlayVerificationResult
            {
                IsValid = false,
                FailureReason = "Google Play verification is not implemented on the server yet.",
                RawResponseSnippet = null
            };

            return Task.FromResult(result);
        }
    }

    public class GooglePlaySubscriptionVerificationRequest
    {
        /// <summary>
        /// The package name of the app as defined in the Play Console (e.g. com.example.myapp).
        /// </summary>
        [Required]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// The product / subscription ID as configured in Google Play Console.
        /// </summary>
        [Required]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>
        /// The purchase token returned by Google Play Billing on the device.
        /// </summary>
        [Required]
        public string PurchaseToken { get; set; } = string.Empty;

        /// <summary>
        /// Optional Google orderId for additional idempotency / debugging.
        /// </summary>
        public string? OrderId { get; set; }
    }

    public class GooglePlayVerificationResult
    {
        public bool IsValid { get; set; }
        public string? FailureReason { get; set; }

        /// <summary>
        /// Optional short snippet of the raw Google response for logging / debugging.
        /// </summary>
        public string? RawResponseSnippet { get; set; }
    }
}

