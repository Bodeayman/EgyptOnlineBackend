using EgyptOnline.Application.Services.Wallet;
using EgyptOnline.Dtos.Wallet;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize(Roles = Roles.User)]
    public class WalletController : ControllerBase
    {
        private readonly WalletService _walletService;
        private readonly ICDNService _cdnService;

        public WalletController(WalletService walletService, ICDNService cdnService)
        {
            _walletService = walletService;
            _cdnService = cdnService;
        }

        private string? GetUserId() => User.FindFirst("uid")?.Value;

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var wallet = await _walletService.GetBalanceAsync(userId);
                return Ok(new { data = new { userId, balance = wallet.Balance } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Submit a manual deposit request with a receipt image.
        /// POST /api/v1/Wallet/deposit (multipart/form-data)
        /// </summary>
        [HttpPost("deposit")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Deposit([FromForm] SubmitDepositRequestDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                // Validate and upload receipt image
                var receiptPath = await ValidateAndUploadReceiptAsync(dto.ReceiptImage, $"receipt_{userId}");
                if (string.IsNullOrEmpty(receiptPath))
                    return BadRequest(new { message = "فشل رفع صورة الإيصال" });

                var request = await _walletService.SubmitDepositRequestAsync(userId, dto.Amount, dto.SourceWalletNumber, receiptPath);

                return Ok(new
                {
                    message = "تم تقديم طلب الإيداع بنجاح وهو قيد المراجعة من الإدارة",
                    data = new
                    {
                        request.Id,
                        request.Amount,
                        request.SourceWalletNumber,
                        request.Status,
                        request.CreatedAt
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Submit a withdrawal request.
        /// POST /api/v1/Wallet/withdraw
        /// </summary>
        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] SubmitWithdrawRequestDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var request = await _walletService.SubmitWithdrawRequestAsync(userId, dto.Amount, dto.DestinationWalletNumber, dto.WalletOwnerName);

                return Ok(new
                {
                    message = "تم تقديم طلب السحب بنجاح وهو قيد المراجعة من الإدارة",
                    data = new
                    {
                        request.Id,
                        request.Amount,
                        request.DestinationWalletNumber,
                        request.WalletOwnerName,
                        request.Status,
                        request.CreatedAt
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        private async Task<string?> ValidateAndUploadReceiptAsync(IFormFile file, string filePrefix)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("ملف الإيصال فارغ أو غير موجود.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                throw new ArgumentException("نوع الملف غير صالح. يرجى رفع صورة إيصال بصيغة JPG, PNG أو WEBP.");

            const int maxFileSize = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxFileSize)
                throw new ArgumentException("حجم الملف كبير جداً. الحد الأقصى هو 5 ميجابايت.");

            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var uniqueFileName = $"{filePrefix}_{Guid.NewGuid()}{extension}";
            return await _cdnService.UploadPrivateImageAsync(fileBytes, uniqueFileName, "receipts");
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] WalletTransferDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Validation failed", errors = ModelState });

                var (fromWallet, toWallet) = await _walletService.TransferAsync(userId, dto.ToUserId, dto.Amount);
                return Ok(new
                {
                    message = "تم التحويل بنجاح",
                    data = new { fromBalance = fromWallet.Balance, toBalance = toWallet.Balance }
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var transactions = await _walletService.GetTransactionHistoryAsync(userId, pageNumber, pageSize);
                return Ok(new { data = transactions, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
    }
}
