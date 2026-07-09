using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using EgyptOnline.Domain.Models.Enums;

namespace EgyptOnline.Dtos.Wallet
{
    public class WalletDepositDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Amount must be positive")]
        public int Amount { get; set; }
    }

    public class WalletWithdrawDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Amount must be positive")]
        public int Amount { get; set; }
    }

    public class WalletTransferDto
    {
        [Required(ErrorMessage = "ToUserId is required")]
        public string ToUserId { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Amount must be positive")]
        public int Amount { get; set; }
    }

    public class SubmitDepositRequestDto
    {
        [Required(ErrorMessage = "مبلغ الإيداع مطلوب")]
        [Range(1, int.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من أو يساوي 1")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "رقم المحفظة المحول منها مطلوب")]
        [MaxLength(100)]
        public string SourceWalletNumber { get; set; } = string.Empty;

        public PaymentType PaymentType { get; set; } = PaymentType.MobileWallet;

        [Required(ErrorMessage = "اسم صاحب المحفظة مطلوب")]
        [MaxLength(200)]
        public string WalletOwnerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "صورة إيصال التحويل مطلوبة")]
        public IFormFile ReceiptImage { get; set; } = null!;
    }

    public class SubmitWithdrawRequestDto
    {
        [Required(ErrorMessage = "مبلغ السحب مطلوب")]
        [Range(1, int.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من أو يساوي 1")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "رقم المحفظة المحول إليها مطلوب")]
        [MaxLength(100)]
        public string DestinationWalletNumber { get; set; } = string.Empty;

        public PaymentType PaymentType { get; set; } = PaymentType.MobileWallet;

        [Required(ErrorMessage = "اسم صاحب المحفظة مطلوب")]
        [MaxLength(200)]
        public string WalletOwnerName { get; set; } = string.Empty;
    }

    public class UpdateWalletNumberDto
    {
        [Required(ErrorMessage = "رقم المحفظة مطلوب")]
        [MaxLength(50)]
        public string WalletNumber { get; set; } = string.Empty;
    }
}
