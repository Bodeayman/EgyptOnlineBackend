namespace EgyptOnline.Domain.Models.Enums
{
    /// <summary>
    /// Contract payment models
    /// </summary>
    public enum ContractType
    {
        /// <summary>
        /// Daily salary paid out after each completed shift (Default)
        /// </summary>
        PerDay = 0,

        /// <summary>
        /// Single lump-sum payout released upon contract completion
        /// </summary>
        Batch = 1,

        /// <summary>
        /// Attendance tracked daily, payouts accumulated and settled in full at contract end
        /// </summary>
        EndOfDays = 2
    }
}
