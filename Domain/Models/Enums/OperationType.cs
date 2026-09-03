namespace EgyptOnline.Domain.Models.Enums
{
    /// <summary>
    /// Types of balance operations
    /// </summary>
    public enum OperationType
    {
        /// <summary>
        /// Money added to balance (deposit, manual add)
        /// </summary>
        Deposit = 0,

        /// <summary>
        /// Money removed from balance (withdrawal, manual subtract)
        /// </summary>
        Withdrawal = 1,

        /// <summary>
        /// Money moved from free to frozen balance
        /// </summary>
        Freeze = 2,

        /// <summary>
        /// Money moved from frozen to free balance
        /// </summary>
        Unfreeze = 3,

        /// <summary>
        /// Payout from contract completion
        /// </summary>
        Payout = 4,

        /// <summary>
        /// Refund from cancelled transaction
        /// </summary>
        Refund = 5,

        /// <summary>
        /// Manual adjustment by admin
        /// </summary>
        Adjustment = 6,

        /// <summary>
        /// Transfer between users
        /// </summary>
        Transfer = 7,

        /// <summary>
        /// Contract payment (escrow lock or release)
        /// </summary>
        ContractPayment = 8,

        /// <summary>
        /// Other operation not covered by specific types
        /// </summary>
        Other = 9
    }
}
