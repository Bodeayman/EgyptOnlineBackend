using System.ComponentModel.DataAnnotations;

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
}
