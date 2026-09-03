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

    public class BalanceTransactionDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public BalanceType BalanceType { get; set; }
        public OperationType OperationType { get; set; }
        public int Amount { get; set; }
        public int BalanceBefore { get; set; }
        public int BalanceAfter { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BalanceAuditQueryFilter
    {
        public string? UserId { get; set; }
        public BalanceType? BalanceType { get; set; }
        public OperationType? OperationType { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class PagedBalanceTransactionsResponse
    {
        public List<BalanceTransactionDto> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}
