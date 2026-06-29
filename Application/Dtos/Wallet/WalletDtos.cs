using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EgyptOnline.Dtos.Wallet
{
    public class WalletDepositDto
    {
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive")]
        public decimal Amount { get; set; }
    }

    public class WalletWithdrawDto
    {
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive")]
        public decimal Amount { get; set; }
    }

    public class WalletTransferDto
    {
        [Required(ErrorMessage = "ToUserId is required")]
        public string ToUserId { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive")]
        public decimal Amount { get; set; }
    }

    public class SubmitDepositRequestDto
    {
        [Required(ErrorMessage = "مبلغ الإيداع مطلوب")]
        [Range(1.0, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من أو يساوي 1")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "رقم المحفظة المحول منها مطلوب")]
        [MaxLength(100)]
        public string SourceWalletNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "صورة إيصال التحويل مطلوبة")]
        public IFormFile ReceiptImage { get; set; } = null!;
    }

    public class SubmitWithdrawRequestDto
    {
        [Required(ErrorMessage = "مبلغ السحب مطلوب")]
        [Range(1.0, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من أو يساوي 1")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "رقم المحفظة المحول إليها مطلوب")]
        [MaxLength(100)]
        public string DestinationWalletNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم صاحب المحفظة مطلوب")]
        [MaxLength(200)]
        public string WalletOwnerName { get; set; } = string.Empty;
    }
}
