using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using EgyptOnline.Application.Services.Contract;
using EgyptOnline.Data;
using EgyptOnline.Dtos.Contract;
using EgyptOnline.Models;
using EgyptOnline.Services;
using System;
using System.Threading.Tasks;

namespace EgyptOnline.Tests
{
    /// <summary>
    /// ContractService Tests - Verifies all contract operations use USERNAME as the unique identifier.
    ///
    /// FOCUS:
    /// - Contract creation with usernames (not GUIDs)
    /// - Signing / approval flow keyed by username
    /// - Cancellation authorization by username
    /// - Attendance marking and wallet disbursement by username
    /// - Confirm arrival and apply penalty logic
    /// - GetUserIdByUsername resolution works correctly
    /// </summary>
    public class ContractServiceTests
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly ContractService _service;

        public ContractServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new ApplicationDbContext(options);
            _notificationMock = new Mock<INotificationService>();
            _service = new ContractService(_context, _notificationMock.Object);
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private async Task<(User contractor, User engineer, User worker)> SeedThreeUsersAsync(
            string contractorUsername = "contractor.user",
            string engineerUsername  = "engineer.user",
            string workerUsername    = "worker.user")
        {
            var contractor = new User
            {
                Id = Guid.NewGuid().ToString(),
                UserName = contractorUsername,
                Email = $"{contractorUsername}@test.com",
                Governorate = "Cairo", City = "Cairo"
            };
            var engineer = new User
            {
                Id = Guid.NewGuid().ToString(),
                UserName = engineerUsername,
                Email = $"{engineerUsername}@test.com",
                Governorate = "Cairo", City = "Cairo"
            };
            var worker = new User
            {
                Id = Guid.NewGuid().ToString(),
                UserName = workerUsername,
                Email = $"{workerUsername}@test.com",
                Governorate = "Cairo", City = "Cairo"
            };

            _context.Users.AddRange(contractor, engineer, worker);
            await _context.SaveChangesAsync();
            return (contractor, engineer, worker);
        }

        private async Task SeedWalletsAsync(User contractor, User engineer, User worker,
            decimal contractorBalance = 10_000m)
        {
            _context.UserWallets.AddRange(
                new UserWallet { UserId = contractor.Id, Balance = contractorBalance },
                new UserWallet { UserId = engineer.Id,   Balance = 0 },
                new UserWallet { UserId = worker.Id,     Balance = 0 }
            );
            await _context.SaveChangesAsync();
        }

        private CreateContractDto BuildDto(
            string contractorUsername,
            string engineerUsername,
            string workerUsername,
            decimal amount = 3000m) => new CreateContractDto
        {
            ContractorUsername     = contractorUsername,
            EngineerUsername       = engineerUsername,
            WorkerUsername         = workerUsername,
            TermsAndConditions     = "Standard terms",
            AgreedTotalAmount      = amount,
            SplitEnabled           = false,
            PenaltyClauseAmount    = 500m,
            PenaltyConditions      = "No-show",
            PenaltySplitContractorPercent = 60,
            PenaltySplitEngineerPercent   = 40,
            WorkLocation           = "Cairo, Egypt",
            FirstWorkingDay        = DateTime.UtcNow.AddDays(1)
        };

        // =========================================================
        // SCENARIO 1: CreateContractAsync — stores usernames, not GUIDs
        // =========================================================

        [Fact]
        public async Task CreateContract_ShouldStoreUsernames_NotGuids()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!);

            // ACT
            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            // ASSERT
            Assert.NotNull(contract);
            Assert.Equal(contractor.UserName, contract.ContractorUsername);
            Assert.Equal(engineer.UserName,   contract.EngineerUsername);
            Assert.Equal(worker.UserName,     contract.WorkerUsername);

            // Must NOT equal the GUIDs
            Assert.NotEqual(contractor.Id, contract.ContractorUsername);
            Assert.NotEqual(worker.Id,     contract.WorkerUsername);
        }

        [Fact]
        public async Task CreateContract_ApprovalsJson_ShouldUseUsernames()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!);

            // ACT
            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            // ASSERT — ApprovalsJson keys must be usernames
            Assert.Contains(contractor.UserName!, contract.ApprovalsJson);
            Assert.Contains(engineer.UserName!,   contract.ApprovalsJson);
            Assert.Contains(worker.UserName!,      contract.ApprovalsJson);

            // Must NOT contain GUIDs as keys
            Assert.DoesNotContain(contractor.Id, contract.ApprovalsJson);
            Assert.DoesNotContain(worker.Id,     contract.ApprovalsJson);
        }

        [Fact]
        public async Task CreateContract_WithUnknownUsername_ShouldThrow()
        {
            // ARRANGE — only seed contractor and engineer, worker doesn't exist
            var (contractor, engineer, _) = await SeedThreeUsersAsync();
            var dto = BuildDto(contractor.UserName!, engineer.UserName!, "ghost.worker");

            // ACT & ASSERT
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateContractAsync(dto, contractor.UserName!));
        }

        [Fact]
        public async Task CreateContract_PenaltyMismatch_ShouldThrow()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!);
            dto.PenaltySplitContractorPercent = 70; // 70 + 40 != 100
            dto.PenaltySplitEngineerPercent   = 40;

            // ACT & ASSERT
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateContractAsync(dto, contractor.UserName!));
        }

        [Fact]
        public async Task CreateContract_WithSplitEnabled_ShouldAutoGenerateInstallments()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!, 3000m);
            dto.SplitEnabled = true;
            dto.SplitDays = 3;
            dto.DailyAmount = 1000m;

            // ACT
            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            // ASSERT — installments generated
            Assert.NotEqual("[]", contract.InstallmentsJson);
            Assert.Contains("dayIndex", contract.InstallmentsJson);
        }

        // =========================================================
        // SCENARIO 2: SignContractAsync — approvals keyed by username
        // =========================================================

        [Fact]
        public async Task SignContract_AllPartiesSign_ShouldActivateAndLockEscrow()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, contractorBalance: 5000m);

            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!, 3000m);
            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            // ACT — all three sign
            await _service.SignContractAsync(contract.Id, contractor.UserName!, accepted: true);
            await _service.SignContractAsync(contract.Id, engineer.UserName!,  accepted: true);
            var result = await _service.SignContractAsync(contract.Id, worker.UserName!, accepted: true);

            // ASSERT
            Assert.Equal("active", result.Status);
            Assert.Equal(3000m, result.EscrowAmount);

            // Contractor wallet deducted
            var contractorWallet = await _context.UserWallets
                .FirstAsync(w => w.UserId == contractor.Id);
            Assert.Equal(2000m, contractorWallet.Balance); // 5000 - 3000
        }

        [Fact]
        public async Task SignContract_WhenUserNotParty_ShouldThrowUnauthorized()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!);
            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            // ACT & ASSERT — outsider signs
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.SignContractAsync(contract.Id, "random.outsider", accepted: true));
        }

        [Fact]
        public async Task SignContract_OnlyTwoSign_ShouldRemainPendingSignatures()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker);

            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!);
            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            // ACT — only contractor and engineer sign
            await _service.SignContractAsync(contract.Id, contractor.UserName!, accepted: true);
            var result = await _service.SignContractAsync(contract.Id, engineer.UserName!, accepted: true);

            // ASSERT — still waiting for worker
            Assert.Equal("pending_signatures", result.Status);
        }

        // =========================================================
        // SCENARIO 3: CancelContractAsync — actor matched by username
        // =========================================================

        [Fact]
        public async Task CancelContract_ByContractorUsername_ShouldSucceed()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 5000m);

            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!, 3000m);
            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            // Activate it
            await _service.SignContractAsync(contract.Id, contractor.UserName!, true);
            await _service.SignContractAsync(contract.Id, engineer.UserName!, true);
            await _service.SignContractAsync(contract.Id, worker.UserName!, true);

            // ACT
            var cancelled = await _service.CancelContractAsync(contract.Id, contractor.UserName!);

            // ASSERT
            Assert.Equal("cancelled", cancelled.Status);
            Assert.Equal(contractor.UserName, cancelled.CancelledBy); // username stored, not GUID
        }

        [Fact]
        public async Task CancelContract_ByOutsider_ShouldThrowUnauthorized()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 5000m);

            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!, 3000m);
            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            await _service.SignContractAsync(contract.Id, contractor.UserName!, true);
            await _service.SignContractAsync(contract.Id, engineer.UserName!, true);
            await _service.SignContractAsync(contract.Id, worker.UserName!, true);

            // ACT & ASSERT
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.CancelContractAsync(contract.Id, "unknown.user"));
        }

        [Fact]
        public async Task CancelContract_AlreadyCancelled_ShouldThrow()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 5000m);

            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!, 3000m);
            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            await _service.SignContractAsync(contract.Id, contractor.UserName!, true);
            await _service.SignContractAsync(contract.Id, engineer.UserName!, true);
            await _service.SignContractAsync(contract.Id, worker.UserName!, true);

            await _service.CancelContractAsync(contract.Id, contractor.UserName!);

            // ACT & ASSERT — cancel again
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CancelContractAsync(contract.Id, contractor.UserName!));
        }

        // =========================================================
        // SCENARIO 4: MarkAttendanceAsync — only contractor/engineer
        // =========================================================

        private async Task<Models.Contract> CreateActiveContractAsync(
            User contractor, User engineer, User worker, decimal amount = 3000m)
        {
            var dto = new CreateContractDto
            {
                ContractorUsername = contractor.UserName!,
                EngineerUsername   = engineer.UserName!,
                WorkerUsername     = worker.UserName!,
                AgreedTotalAmount  = amount,
                SplitEnabled       = true,
                SplitDays          = 3,
                DailyAmount        = amount / 3,
                PenaltyClauseAmount           = 500m,
                PenaltySplitContractorPercent = 60,
                PenaltySplitEngineerPercent   = 40,
                WorkLocation       = "Cairo",
                FirstWorkingDay    = DateTime.UtcNow.AddDays(1),
                TermsAndConditions = "Terms"
            };

            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);
            await _service.SignContractAsync(contract.Id, contractor.UserName!, true);
            await _service.SignContractAsync(contract.Id, engineer.UserName!, true);
            await _service.SignContractAsync(contract.Id, worker.UserName!, true);

            return await _context.Contracts.FirstAsync(c => c.Id == contract.Id);
        }

        [Fact]
        public async Task MarkAttendance_Attended_ShouldReleaseInstallmentToWorker()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 6000m);

            var contract = await CreateActiveContractAsync(contractor, engineer, worker, 3000m);

            // ACT
            var result = await _service.MarkAttendanceAsync(contract.Id, contractor.UserName!, "attended");

            // ASSERT — first installment paid, worker wallet credited
            Assert.Contains("paid", result.InstallmentsJson);

            var workerWallet = await _context.UserWallets.FirstAsync(w => w.UserId == worker.Id);
            Assert.True(workerWallet.Balance > 0);
        }

        [Fact]
        public async Task MarkAttendance_Absent_ShouldHoldInstallment()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 6000m);

            var contract = await CreateActiveContractAsync(contractor, engineer, worker, 3000m);

            // ACT
            var result = await _service.MarkAttendanceAsync(contract.Id, engineer.UserName!, "absent");

            // ASSERT — first installment held, worker NOT credited
            Assert.Contains("held", result.InstallmentsJson);

            var workerWallet = await _context.UserWallets.FirstAsync(w => w.UserId == worker.Id);
            Assert.Equal(0m, workerWallet.Balance);
        }

        [Fact]
        public async Task MarkAttendance_ByWorker_ShouldThrowUnauthorized()
        {
            // ARRANGE — only contractor/engineer can mark attendance
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 6000m);

            var contract = await CreateActiveContractAsync(contractor, engineer, worker, 3000m);

            // ACT & ASSERT
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.MarkAttendanceAsync(contract.Id, worker.UserName!, "attended"));
        }

        // =========================================================
        // SCENARIO 5: DisburseInstallmentAsync
        // =========================================================

        [Fact]
        public async Task DisburseInstallment_HeldInstallment_ShouldPayWorker()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 6000m);

            var contract = await CreateActiveContractAsync(contractor, engineer, worker, 3000m);

            // Mark absent so first installment is in "held" state
            await _service.MarkAttendanceAsync(contract.Id, contractor.UserName!, "absent");

            // ACT — manually disburse the held installment
            var result = await _service.DisburseInstallmentAsync(contract.Id, contractor.UserName!, 0);

            // ASSERT
            Assert.Contains("paid", result.InstallmentsJson);

            var workerWallet = await _context.UserWallets.FirstAsync(w => w.UserId == worker.Id);
            Assert.True(workerWallet.Balance > 0);
        }

        [Fact]
        public async Task DisburseInstallment_ByWorker_ShouldThrowUnauthorized()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 6000m);

            var contract = await CreateActiveContractAsync(contractor, engineer, worker, 3000m);
            await _service.MarkAttendanceAsync(contract.Id, contractor.UserName!, "absent");

            // ACT & ASSERT
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.DisburseInstallmentAsync(contract.Id, worker.UserName!, 0));
        }

        // =========================================================
        // SCENARIO 6: ConfirmArrivalAsync
        // =========================================================

        [Fact]
        public async Task ConfirmArrival_ShouldCompleteContractAndPayWorker()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 6000m);

            var contract = await CreateActiveContractAsync(contractor, engineer, worker, 3000m);

            // ACT
            var result = await _service.ConfirmArrivalAsync(contract.Id, contractor.UserName!);

            // ASSERT
            Assert.Equal("completed", result.Status);
            Assert.True(result.ArrivalConfirmed);
            Assert.Equal(0m, result.EscrowAmount);

            var workerWallet = await _context.UserWallets.FirstAsync(w => w.UserId == worker.Id);
            Assert.True(workerWallet.Balance > 0);
        }

        [Fact]
        public async Task ConfirmArrival_AlreadyConfirmed_ShouldThrow()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 6000m);

            var contract = await CreateActiveContractAsync(contractor, engineer, worker, 3000m);
            await _service.ConfirmArrivalAsync(contract.Id, contractor.UserName!);

            // ACT & ASSERT
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ConfirmArrivalAsync(contract.Id, contractor.UserName!));
        }

        // =========================================================
        // SCENARIO 7: ApplyPenaltyAsync
        // =========================================================

        [Fact]
        public async Task ApplyPenalty_ShouldDistributePenaltyToContractorAndEngineer()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 6000m);

            var contract = await CreateActiveContractAsync(contractor, engineer, worker, 3000m);

            var contractorBalanceBefore = (await _context.UserWallets.FirstAsync(w => w.UserId == contractor.Id)).Balance;
            var engineerBalanceBefore   = (await _context.UserWallets.FirstAsync(w => w.UserId == engineer.Id)).Balance;

            // ACT
            var result = await _service.ApplyPenaltyAsync(contract.Id, contractor.UserName!);

            // ASSERT
            Assert.Equal("completed", result.Status);
            Assert.True(result.NoShowProcessed);

            var contractorWallet = await _context.UserWallets.FirstAsync(w => w.UserId == contractor.Id);
            var engineerWallet   = await _context.UserWallets.FirstAsync(w => w.UserId == engineer.Id);

            // Both received penalty shares
            Assert.True(contractorWallet.Balance > contractorBalanceBefore);
            Assert.True(engineerWallet.Balance   > engineerBalanceBefore);
        }

        [Fact]
        public async Task ApplyPenalty_ByWorker_ShouldThrowUnauthorized()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            await SeedWalletsAsync(contractor, engineer, worker, 6000m);

            var contract = await CreateActiveContractAsync(contractor, engineer, worker, 3000m);

            // ACT & ASSERT
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.ApplyPenaltyAsync(contract.Id, worker.UserName!));
        }

        // =========================================================
        // SCENARIO 8: GetByIdAsync
        // =========================================================

        [Fact]
        public async Task GetById_ExistingContract_ShouldReturnContract()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!);
            var created = await _service.CreateContractAsync(dto, contractor.UserName!);

            // ACT
            var fetched = await _service.GetByIdAsync(created.Id);

            // ASSERT
            Assert.NotNull(fetched);
            Assert.Equal(created.Id, fetched!.Id);
            Assert.Equal(contractor.UserName, fetched.ContractorUsername);
        }

        [Fact]
        public async Task GetById_NonExistent_ShouldReturnNull()
        {
            // ACT
            var result = await _service.GetByIdAsync(99999);

            // ASSERT
            Assert.Null(result);
        }

        // =========================================================
        // SCENARIO 9: GetInstallmentsAsync
        // =========================================================

        [Fact]
        public async Task GetInstallments_SplitContract_ShouldReturnInstallmentList()
        {
            // ARRANGE
            var (contractor, engineer, worker) = await SeedThreeUsersAsync();
            var dto = BuildDto(contractor.UserName!, engineer.UserName!, worker.UserName!, 3000m);
            dto.SplitEnabled = true;
            dto.SplitDays    = 3;
            dto.DailyAmount  = 1000m;

            var contract = await _service.CreateContractAsync(dto, contractor.UserName!);

            // ACT
            var installments = await _service.GetInstallmentsAsync(contract.Id);

            // ASSERT
            Assert.NotNull(installments);
        }

        [Fact]
        public async Task GetInstallments_NonExistentContract_ShouldThrow()
        {
            // ACT & ASSERT
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _service.GetInstallmentsAsync(99999));
        }
    }
}
