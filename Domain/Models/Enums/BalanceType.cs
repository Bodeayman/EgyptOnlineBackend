namespace EgyptOnline.Domain.Models.Enums
{
    /// <summary>
    /// Types of wallet balances
    /// </summary>
    public enum BalanceType
    {
        /// <summary>
        /// Free balance available for withdrawal and transfers
        /// </summary>
        Free = 0,

        /// <summary>
        /// Frozen balance locked for pending contracts or transactions
        /// </summary>
        Frozen = 1
    }
}
