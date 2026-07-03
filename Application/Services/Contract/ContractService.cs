using System.Text.Json;
using EgyptOnline.Data;
using EgyptOnline.Dtos.Contract;
using EgyptOnline.Domain.Models;
using EgyptOnline.Domain.Models.Enums;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using ContractModel = EgyptOnline.Models.Contract;
using ContractDayModel = EgyptOnline.Domain.Models.ContractDay;

namespace EgyptOnline.Application.Services.Contract
{
    public class ContractService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly Wallet.WalletService _walletService;
        private readonly Complaint.ComplaintService _complaintService;
        private readonly ILogger<ContractService> _logger;

        public ContractService(ApplicationDbContext context, INotificationService notificationService, Wallet.WalletService walletService, Complaint.ComplaintService complaintService, ILogger<ContractService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _walletService = walletService;
            _complaintService = complaintService;
            _logger = logger;
        }

        #region 2-Party Contract System (New Simplified Logic)

        public async Task<ContractModel> CreateContractAsync(ContractModel contract)
        {
            var clientUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == contract.ClientUserId);
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == contract.ServiceProviderPhoneNumber);

            if (clientUser == null)
                throw new InvalidOperationException($"Client user not found: {contract.ClientUserId}");
            if (providerUser == null)
                throw new InvalidOperationException($"Service provider not found with phone number: {contract.ServiceProviderPhoneNumber}");

            var hasSufficientBalance = await _walletService.HasSufficientFreeBalanceAsync(contract.ClientUserId, contract.TotalAmount.Value);
            if (!hasSufficientBalance)
                throw new InvalidOperationException($"Client has insufficient free balance. Required: {contract.TotalAmount}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _walletService.TransferFreeToFrozenAsync(contract.ClientUserId, contract.TotalAmount.Value);

                contract.Status = "pending";
                contract.CreatedAt = DateTime.UtcNow;

                _context.Contracts.Add(contract);
                await _context.SaveChangesAsync();

                var contractDays = new List<ContractDayModel>();
                for (int day = 1; day <= contract.TotalDays.Value; day++)
                {
                    var contractDay = new ContractDayModel
                    {
                        ContractId = contract.Id,
                        DayNumber = day,
                        Date = contract.StartDate.Value.AddDays(day - 1),
                        ProviderArrived = false,
                        Status = ContractDayStatus.Pending,
                        IsProcessed = false
                    };
                    contractDays.Add(contractDay);
                }

                _context.ContractDays.AddRange(contractDays);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Created 2-party contract {ContractId} for client {ClientId} and provider phone {ProviderPhone}. Total: {TotalAmount}",
                    contract.Id, contract.ClientUserId, contract.ServiceProviderPhoneNumber, contract.TotalAmount);

                // Send notification to service provider
                await _notificationService.SendNotificationToUser(
                    providerUser.Id,
                    "عقد جديد",
                    $"تم إنشاء عقد جديد #{contract.Id} بقيمة {contract.TotalAmount} جنيه",
                    contract.ClientUserId
                );

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ContractModel> ProviderRejectContractAsync(int contractId, string providerUserId)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"Contract not found: {contractId}");

            // Verify the caller is the service provider by phone number
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == providerUserId);
            if (providerUser == null || providerUser.PhoneNumber != contract.ServiceProviderPhoneNumber)
                throw new UnauthorizedAccessException("You are not authorized to reject this contract");

            if (contract.Status != "pending")
                throw new InvalidOperationException($"Contract is not in Pending status. Current status: {contract.Status}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _walletService.TransferFrozenToFreeAsync(contract.ClientUserId, contract.TotalAmount.Value);

                contract.Status = "cancelled";
                contract.CancelledAt = DateTime.UtcNow;
                contract.CancelledBy = contract.ServiceProviderPhoneNumber;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Provider rejected contract {ContractId}. Funds restored to client {ClientId}", contractId, contract.ClientUserId);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ContractModel> ProviderAcceptContractAsync(int contractId, string providerUserId)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"Contract not found: {contractId}");

            // Verify the caller is the service provider by phone number
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == providerUserId);
            if (providerUser == null || providerUser.PhoneNumber != contract.ServiceProviderPhoneNumber)
                throw new UnauthorizedAccessException("You are not authorized to accept this contract");

            if (contract.Status != "pending")
                throw new InvalidOperationException($"Contract is not in Pending status. Current status: {contract.Status}");

            contract.Status = "active";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Provider accepted contract {ContractId}. Status changed to Active", contractId);

            // Send notification to client
            await _notificationService.SendNotificationToUser(
                contract.ClientUserId,
                "تم قبول العقد",
                $"تم قبول العقد #{contract.Id} من قبل مقدم الخدمة",
                providerUserId
            );

            return contract;
        }

        public async Task<ContractDayModel> RegisterArrivalAsync(int contractId, int dayNumber, string providerUserId)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"Contract not found: {contractId}");

            // Verify the caller is the service provider by phone number
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == providerUserId);
            if (providerUser == null || providerUser.PhoneNumber != contract.ServiceProviderPhoneNumber)
                throw new UnauthorizedAccessException("You are not authorized to register arrival for this contract");

            if (contract.Status != "active")
                throw new InvalidOperationException($"Contract is not in Active status. Current status: {contract.Status}");

            var contractDay = contract.ContractDays.FirstOrDefault(cd => cd.DayNumber == dayNumber);
            if (contractDay == null)
                throw new InvalidOperationException($"Contract day {dayNumber} not found for contract {contractId}");

            if (contractDay.ProviderArrived)
                throw new InvalidOperationException($"Provider has already arrived for day {dayNumber}");

            contractDay.ProviderArrived = true;
            contractDay.ArrivalTime = DateTime.UtcNow;
            contractDay.Status = ContractDayStatus.Pending;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Provider arrived for contract {ContractId}, day {DayNumber}", contractId, dayNumber);

            // Send notification to client
            await _notificationService.SendNotificationToUser(
                contract.ClientUserId,
                "وصول مقدم الخدمة",
                $"وصل مقدم الخدمة لموقع العمل - يوم {dayNumber} من العقد #{contractId}",
                providerUserId
            );

            return contractDay;
        }

        public async Task<ContractModel> ReportDisputeAsync(int contractId, int dayNumber, string reason)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"Contract not found: {contractId}");

            if (contract.Status != "active")
                throw new InvalidOperationException($"Contract is not in Active status. Current status: {contract.Status}");

            var contractDay = contract.ContractDays.FirstOrDefault(cd => cd.DayNumber == dayNumber);
            if (contractDay == null)
                throw new InvalidOperationException($"Contract day {dayNumber} not found");

            var shiftEndTime = contract.ShiftEndTime.Value;
            var gracePeriodEnd = contractDay.Date.Add(shiftEndTime).AddHours(3);
            var currentTime = DateTime.UtcNow;

            if (currentTime > gracePeriodEnd)
                throw new InvalidOperationException($"Dispute cannot be reported after 3-hour grace period expired. Grace period ended at: {gracePeriodEnd}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                contract.Status = "suspended";

                contractDay.Status = ContractDayStatus.AbsentDisputed;
                contractDay.DisputeReportedAt = DateTime.UtcNow;
                contractDay.DisputeReason = reason;

                await _context.SaveChangesAsync();

                // Create complaint for admin review
                await _complaintService.FileComplaintAsync(
                    contract.ClientUserId,
                    contractId,
                    "غياب مقدم الخدمة",
                    $"تم الإبلاغ عن غياب مقدم الخدمة في يوم {dayNumber}. السبب: {reason}"
                );

                await transaction.CommitAsync();

                _logger.LogInformation("Dispute reported for contract {ContractId}, day {DayNumber}. Reason: {Reason}", contractId, dayNumber, reason);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ContractModel> MutualTerminationAsync(int contractId, string reason)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"Contract not found: {contractId}");

            if (contract.Status != "active" && contract.Status != "suspended")
                throw new InvalidOperationException($"Contract must be Active or Suspended for mutual termination. Current status: {contract.Status}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Keep all funds frozen - no auto-distribution
                // Admin will manually resolve via balance override

                contract.Status = "terminated";
                contract.TerminatedAt = DateTime.UtcNow;
                contract.TerminatedBy = "Mutual";
                contract.CancelledBy = "Mutual";
                contract.CancelledAt = DateTime.UtcNow;
                contract.TerminationReason = reason;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Mutual termination for contract {ContractId}. Reason: {Reason}. Funds remain frozen for admin resolution",
                    contractId, reason);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ContractModel> ClientUnilateralTerminationAsync(int contractId, string reason)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"Contract not found: {contractId}");

            if (contract.Status != "active" && contract.Status != "suspended")
                throw new InvalidOperationException($"Contract must be Active or Suspended for unilateral termination. Current status: {contract.Status}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Keep all funds frozen - no auto-distribution
                // Admin will manually resolve via balance override

                contract.Status = "terminated";
                contract.TerminatedAt = DateTime.UtcNow;
                contract.TerminatedBy = contract.ClientUserId;
                contract.CancelledBy = contract.ClientUserId;
                contract.CancelledAt = DateTime.UtcNow;
                contract.TerminationReason = reason;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Client unilateral termination for contract {ContractId}. Reason: {Reason}. Funds remain frozen for admin resolution",
                    contractId, reason);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ContractModel> ProviderUnilateralTerminationAsync(int contractId, string providerUserId, string reason)
        {
            var contract = await _context.Contracts
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException($"Contract not found: {contractId}");

            // Verify the caller is the service provider by phone number
            var providerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == providerUserId);
            if (providerUser == null || providerUser.PhoneNumber != contract.ServiceProviderPhoneNumber)
                throw new UnauthorizedAccessException("You are not authorized to terminate this contract");

            if (contract.Status != "active" && contract.Status != "suspended")
                throw new InvalidOperationException($"Contract must be Active or Suspended for unilateral termination. Current status: {contract.Status}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Keep all funds frozen - no auto-distribution
                // Admin will manually resolve via balance override

                contract.Status = "terminated";
                contract.TerminatedAt = DateTime.UtcNow;
                contract.TerminatedBy = contract.ServiceProviderPhoneNumber;
                contract.CancelledBy = contract.ServiceProviderPhoneNumber;
                contract.CancelledAt = DateTime.UtcNow;
                contract.TerminationReason = reason;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Provider unilateral termination for contract {ContractId}. Reason: {Reason}. Funds remain frozen for admin resolution",
                    contractId, reason);

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ContractModel?> GetContractByIdAsync(int contractId)
        {
            return await _context.Contracts
                .Include(c => c.ClientUser)
                .Include(c => c.ContractDays)
                .FirstOrDefaultAsync(c => c.Id == contractId);
        }

        public async Task<List<ContractModel>> GetContractsByUserIdAsync(string userId, string? status = null, int pageNumber = 1, int pageSize = 20)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            // Get the user's phone number to match against ServiceProviderPhoneNumber
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return new List<ContractModel>();

            var query = _context.Contracts
                .Include(c => c.ContractDays)
                .Where(c => c.ServiceProviderPhoneNumber == user.PhoneNumber || c.ClientUserId == userId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<ContractModel>> GetActiveContractsForAutoPayoutAsync(DateTime currentTime)
        {
            return await _context.Contracts
                .Include(c => c.ContractDays)
                .Where(c => c.Status == "active")
                .ToListAsync();
        }

        #endregion
    }
}
