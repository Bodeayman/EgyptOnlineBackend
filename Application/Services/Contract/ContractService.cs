using System.Text.Json;
using EgyptOnline.Data;
using EgyptOnline.Dtos.Contract;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EgyptOnline.Application.Services.Contract
{
    public class ContractService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public ContractService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<Models.Contract> CreateContractAsync(CreateContractDto dto, string creatorUsername)
        {
            // Validate penalty distribution sums to 100%
            if (Math.Abs(dto.PenaltySplitContractorPercent + dto.PenaltySplitEngineerPercent - 100) > 0.01)
                throw new InvalidOperationException("مجموع نسب توزيع الجزاء يجب ان يساوي 100%");

            // Validate first working day is not in the past
            if (dto.FirstWorkingDay.HasValue && dto.FirstWorkingDay.Value.Date < DateTime.UtcNow.Date)
                throw new InvalidOperationException("اول يوم عمل لا يمكن ان يكون في الماضي");

            // Validate all 3 users exist
            var contractorExists = await _context.Users.AnyAsync(u => u.UserName == dto.ContractorUsername);
            var engineerExists = await _context.Users.AnyAsync(u => u.UserName == dto.EngineerUsername);
            var workerExists = await _context.Users.AnyAsync(u => u.UserName == dto.WorkerUsername);

            if (!contractorExists) throw new InvalidOperationException("المقاول غير موجود");
            if (!engineerExists) throw new InvalidOperationException("المهندس غير موجود");
            if (!workerExists) throw new InvalidOperationException("العامل غير موجود");

            // Auto-generate installment schedule if split is enabled
            object installmentsData = dto.Installments ?? new List<object>();
            if (dto.SplitEnabled && dto.SplitDays > 0 && dto.Installments == null)
            {
                var daily = dto.AgreedTotalAmount / dto.SplitDays;
                var autoInstallments = new List<object>();
                var startDate = dto.FirstWorkingDay ?? DateTime.UtcNow;
                for (int i = 1; i <= dto.SplitDays; i++)
                {
                    autoInstallments.Add(new
                    {
                        dayIndex = i,
                        amount = daily,
                        dueDate = startDate.AddDays(i).ToString("dd/MM/yyyy"),
                        status = "pending"
                    });
                }
                installmentsData = autoInstallments;
            }

            var contract = new Models.Contract
            {
                ContractorUsername = dto.ContractorUsername,
                EngineerUsername = dto.EngineerUsername,
                WorkerUsername = dto.WorkerUsername,
                TermsAndConditions = dto.TermsAndConditions,
                AgreedTotalAmount = dto.AgreedTotalAmount,
                SplitEnabled = dto.SplitEnabled,
                SplitDays = dto.SplitDays,
                DailyAmount = dto.DailyAmount,
                InstallmentsJson = JsonSerializer.Serialize(installmentsData),
                PenaltyClauseAmount = dto.PenaltyClauseAmount,
                PenaltyConditions = dto.PenaltyConditions,
                PenaltySplitContractorPercent = dto.PenaltySplitContractorPercent,
                PenaltySplitEngineerPercent = dto.PenaltySplitEngineerPercent,
                FirstWorkingDay = dto.FirstWorkingDay,
                WorkLocation = dto.WorkLocation,
                Status = "pending_signatures",
                ApprovalsJson = JsonSerializer.Serialize(new Dictionary<string, bool>
                {
                    [dto.ContractorUsername] = false,
                    [dto.EngineerUsername] = false,
                    [dto.WorkerUsername] = false
                }),
                HistoryJson = JsonSerializer.Serialize(new[]
                {
                    new { id = Guid.NewGuid().ToString(), type = "system", message = "تم انشاء العقد", createdAt = DateTime.UtcNow }
                })
            };

            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();

            // Notify all parties using Firebase
            await SafeNotify(dto.ContractorUsername, "عقد جديد", $"تم انشاء عقد جديد #{contract.Id}");
            await SafeNotify(dto.EngineerUsername, "عقد جديد", $"العقد #{contract.Id} بانتظار توقيعك");
            await SafeNotify(dto.WorkerUsername, "عقد جديد", $"العقد #{contract.Id} بانتظار توقيعك");

            return contract;
        }

        public async Task<Models.Contract?> GetByIdAsync(int id)
        {
            return await _context.Contracts.FirstOrDefaultAsync(c => c.Id == id);
        }

        /// <summary>
        /// Returns all contracts where the given username is any party (contractor, engineer, or worker).
        /// No join needed — username is stored directly on the contract.
        /// </summary>
        public async Task<List<Models.Contract>> GetMyContractsAsync(string username, int pageNumber = 1, int pageSize = Constants.PAGE_SIZE)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            return await Helper.PaginateUsers(
                    _context.Contracts
                        .Where(c =>
                            c.ContractorUsername == username ||
                            c.EngineerUsername == username ||
                            c.WorkerUsername == username)
                        .OrderByDescending(c => c.CreatedAt),
                    pageNumber,
                    pageSize)
                .ToListAsync();
        }

        public async Task<Models.Contract> SignContractAsync(int contractId, string username, bool accepted)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            var approvals = JsonSerializer.Deserialize<Dictionary<string, bool>>(contract.ApprovalsJson) ?? new();

            // Verify user is a party to this contract
            if (!approvals.ContainsKey(username))
                throw new UnauthorizedAccessException("ليس لديك صلاحية التوقيع على هذا العقد");

            approvals[username] = accepted;
            contract.ApprovalsJson = JsonSerializer.Serialize(approvals);

            // If all three signed, lock escrow and activate
            if (approvals.TryGetValue(contract.ContractorUsername, out var c) && c &&
                approvals.TryGetValue(contract.EngineerUsername, out var e) && e &&
                approvals.TryGetValue(contract.WorkerUsername, out var w) && w)
            {
                // Check contractor wallet balance for escrow
                var contractorId = await GetUserIdByUsername(contract.ContractorUsername);
                var contractorWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contractorId);
                if (contractorWallet != null && contractorWallet.Balance >= contract.AgreedTotalAmount)
                {
                    contractorWallet.Balance -= contract.AgreedTotalAmount;
                    contractorWallet.UpdatedAt = DateTime.UtcNow;
                    contract.EscrowAmount = contract.AgreedTotalAmount;
                    contract.Status = "active";

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = contractorId,
                        Type = "escrow_lock",
                        Amount = contract.AgreedTotalAmount,
                        Description = $"حجز مبلغ العقد #{contract.Id}",
                        ContractId = contract.Id
                    });
                }
            }

            await _context.SaveChangesAsync();
            await SafeNotify(username, "توقيع العقد", $"تم توقيعك على العقد #{contract.Id}");

            return contract;
        }

        public async Task<Models.Contract> CancelContractAsync(int contractId, string actorUsername)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status == "cancelled")
                throw new InvalidOperationException("العقد ملغي بالفعل");
            if (contract.Status != "active")
                throw new InvalidOperationException("لا يمكن الغاء عقد غير نشط");

            // Verify actor is a party to this contract
            if (actorUsername != contract.ContractorUsername && actorUsername != contract.EngineerUsername && actorUsername != contract.WorkerUsername)
                throw new UnauthorizedAccessException("ليس لديك صلاحية الغاء هذا العقد");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Freeze all remaining funds
                _context.FundMovementLogs.Add(new FundMovementLog
                {
                    ContractId = contract.Id,
                    InstallmentIndex = -1,
                    Action = "freeze",
                    Amount = contract.EscrowAmount,
                    TriggeredBy = actorUsername,
                    Reason = "الغاء العقد"
                });

                contract.Status = "cancelled";
                contract.CancelledAt = DateTime.UtcNow;
                contract.CancelledBy = actorUsername;

                // Update history
                var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                history.Add(new { id = Guid.NewGuid().ToString(), type = "cancellation", message = "تم الغاء العقد", createdAt = DateTime.UtcNow });
                contract.HistoryJson = JsonSerializer.Serialize(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await SafeNotify(contract.ContractorUsername, "الغاء العقد", $"تم الغاء العقد #{contract.Id}");
            await SafeNotify(contract.EngineerUsername, "الغاء العقد", $"تم الغاء العقد #{contract.Id}");
            await SafeNotify(contract.WorkerUsername, "الغاء العقد", $"تم الغاء العقد #{contract.Id}");

            return contract;
        }

        public async Task<Models.Contract> MarkAttendanceAsync(int contractId, string actorUsername, string status)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");

            // Only contractor or engineer can mark attendance
            if (actorUsername != contract.ContractorUsername && actorUsername != contract.EngineerUsername)
                throw new UnauthorizedAccessException("فقط المقاول أو المهندس يمكنه تسجيل الحضور");

            var today = DateTime.UtcNow.Date;
            var existing = await _context.AttendanceRecords
                .FirstOrDefaultAsync(a => a.ContractId == contractId && a.Date == today);
            if (existing != null)
                throw new InvalidOperationException("تم تسجيل الحضور لهذا اليوم بالفعل");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    ContractId = contractId,
                    Date = today,
                    Status = status,
                    MarkedBy = actorUsername
                });

                var installments = JsonSerializer.Deserialize<List<JsonElement>>(contract.InstallmentsJson) ?? new();

                if (status == "attended")
                {
                    var updatedInstallments = new List<Dictionary<string, object>>();
                    bool released = false;

                    for (int i = 0; i < installments.Count; i++)
                    {
                        var inst = installments[i];
                        var dict = new Dictionary<string, object>
                        {
                            ["dayIndex"] = inst.GetProperty("dayIndex").GetInt32(),
                            ["amount"] = inst.GetProperty("amount").GetDecimal(),
                            ["dueDate"] = inst.GetProperty("dueDate").GetString() ?? "",
                            ["status"] = inst.TryGetProperty("status", out var s) ? s.GetString() ?? "pending" : "pending"
                        };

                        if (!released && dict["status"].ToString() == "pending")
                        {
                            dict["status"] = "paid";
                            var amount = (decimal)dict["amount"];

                            // Release funds to worker wallet
                            var workerId = await GetUserIdByUsername(contract.WorkerUsername);
                            var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == workerId);
                            if (workerWallet != null)
                            {
                                workerWallet.Balance += amount;
                                workerWallet.UpdatedAt = DateTime.UtcNow;
                                contract.EscrowAmount -= amount;

                                _context.WalletTransactions.Add(new WalletTransaction
                                {
                                    UserId = workerId,
                                    Type = "installment_release",
                                    Amount = amount,
                                    Description = $"صرف قسط - العقد #{contract.Id}",
                                    ContractId = contract.Id
                                });

                                _context.FundMovementLogs.Add(new FundMovementLog
                                {
                                    ContractId = contract.Id,
                                    InstallmentIndex = i,
                                    Action = "release",
                                    Amount = amount,
                                    TriggeredBy = actorUsername,
                                    Reason = "حضور العامل"
                                });
                            }
                            released = true;
                        }
                        updatedInstallments.Add(dict);
                    }
                    contract.InstallmentsJson = JsonSerializer.Serialize(updatedInstallments);

                    await SafeNotify(contract.WorkerUsername, "حضور", $"تم تاكيد حضورك - العقد #{contract.Id}");
                }
                else if (status == "absent")
                {
                    var updatedInstallments = new List<Dictionary<string, object>>();
                    bool held = false;

                    for (int i = 0; i < installments.Count; i++)
                    {
                        var inst = installments[i];
                        var dict = new Dictionary<string, object>
                        {
                            ["dayIndex"] = inst.GetProperty("dayIndex").GetInt32(),
                            ["amount"] = inst.GetProperty("amount").GetDecimal(),
                            ["dueDate"] = inst.GetProperty("dueDate").GetString() ?? "",
                            ["status"] = inst.TryGetProperty("status", out var s) ? s.GetString() ?? "pending" : "pending"
                        };

                        if (!held && dict["status"].ToString() == "pending")
                        {
                            dict["status"] = "held";
                            var amount = (decimal)dict["amount"];

                            _context.FundMovementLogs.Add(new FundMovementLog
                            {
                                ContractId = contract.Id,
                                InstallmentIndex = i,
                                Action = "hold",
                                Amount = amount,
                                TriggeredBy = actorUsername,
                                Reason = "غياب العامل"
                            });
                            held = true;
                        }
                        updatedInstallments.Add(dict);
                    }
                    contract.InstallmentsJson = JsonSerializer.Serialize(updatedInstallments);

                    await SafeNotify(contract.WorkerUsername, "غياب", $"تم تسجيل غيابك - العقد #{contract.Id}");
                }

                // Update history
                var hist = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                var statusMsg = status == "attended" ? "تم تاكيد الحضور" : "تم تسجيل الغياب";
                hist.Add(new { id = Guid.NewGuid().ToString(), type = "attendance", message = statusMsg, createdAt = DateTime.UtcNow });
                contract.HistoryJson = JsonSerializer.Serialize(hist);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Models.Contract> DisburseInstallmentAsync(int contractId, string actorUsername, int installmentIndex)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");

            // Only contractor or engineer can disburse
            if (actorUsername != contract.ContractorUsername && actorUsername != contract.EngineerUsername)
                throw new UnauthorizedAccessException("فقط المقاول أو المهندس يمكنه صرف الأقساط");

            var installments = JsonSerializer.Deserialize<List<JsonElement>>(contract.InstallmentsJson) ?? new();
            if (installmentIndex < 0 || installmentIndex >= installments.Count)
                throw new InvalidOperationException("رقم القسط غير صحيح");

            var inst = installments[installmentIndex];
            var currentStatus = inst.TryGetProperty("status", out var sv) ? sv.GetString() ?? "pending" : "pending";

            if (currentStatus == "paid")
                throw new InvalidOperationException("هذا القسط مدفوع بالفعل");
            if (currentStatus != "held")
                throw new InvalidOperationException("لا يمكن صرف قسط غير محتجز");

            var amount = inst.GetProperty("amount").GetDecimal();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var workerId = await GetUserIdByUsername(contract.WorkerUsername);
                var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == workerId)
                    ?? throw new InvalidOperationException("محفظة العامل غير موجوده");

                workerWallet.Balance += amount;
                workerWallet.UpdatedAt = DateTime.UtcNow;
                contract.EscrowAmount -= amount;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = workerId,
                    Type = "manual_disbursement",
                    Amount = amount,
                    Description = $"صرف يدوي - العقد #{contract.Id} - القسط {installmentIndex + 1}",
                    ContractId = contract.Id
                });

                _context.FundMovementLogs.Add(new FundMovementLog
                {
                    ContractId = contract.Id,
                    InstallmentIndex = installmentIndex,
                    Action = "release",
                    Amount = amount,
                    TriggeredBy = actorUsername,
                    Reason = "صرف يدوي"
                });

                // Update installment status
                var updatedInstallments = new List<Dictionary<string, object>>();
                for (int i = 0; i < installments.Count; i++)
                {
                    var item = installments[i];
                    var dict = new Dictionary<string, object>
                    {
                        ["dayIndex"] = item.GetProperty("dayIndex").GetInt32(),
                        ["amount"] = item.GetProperty("amount").GetDecimal(),
                        ["dueDate"] = item.GetProperty("dueDate").GetString() ?? "",
                        ["status"] = item.TryGetProperty("status", out var s2) ? s2.GetString() ?? "pending" : "pending"
                    };
                    if (i == installmentIndex) dict["status"] = "paid";
                    updatedInstallments.Add(dict);
                }
                contract.InstallmentsJson = JsonSerializer.Serialize(updatedInstallments);

                // Update history
                var hist = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                hist.Add(new { id = Guid.NewGuid().ToString(), type = "disbursement", message = $"تم صرف القسط {installmentIndex + 1} يدويا", createdAt = DateTime.UtcNow });
                contract.HistoryJson = JsonSerializer.Serialize(hist);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await SafeNotify(contract.WorkerUsername, "صرف قسط", $"تم صرف القسط {installmentIndex + 1} لك - العقد #{contract.Id}");

            return contract;
        }

        public async Task<object> GetInstallmentsAsync(int contractId)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            return JsonSerializer.Deserialize<List<JsonElement>>(contract.InstallmentsJson) ?? new List<JsonElement>();
        }

        public async Task<Models.Contract> ConfirmArrivalAsync(int contractId, string actorUsername)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");
            if (contract.ArrivalConfirmed)
                throw new InvalidOperationException("تم تأكيد الحضور بالفعل");
            if (actorUsername != contract.ContractorUsername && actorUsername != contract.EngineerUsername)
                throw new UnauthorizedAccessException("فقط المقاول أو المهندس يمكنه تأكيد الحضور");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var workerId = await GetUserIdByUsername(contract.WorkerUsername);
                var contractorId = await GetUserIdByUsername(contract.ContractorUsername);
                var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == workerId);
                if (workerWallet == null)
                    throw new InvalidOperationException("محفظة العامل غير موجوده");

                var amount = contract.EscrowAmount;
                contract.EscrowAmount = 0;
                contract.ArrivalConfirmed = true;
                contract.Status = "completed";
                workerWallet.Balance += amount;
                workerWallet.UpdatedAt = DateTime.UtcNow;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = workerId,
                    Type = "transfer_in",
                    Amount = amount,
                    Description = $"دفعة العقد #{contract.Id}",
                    FromUserId = contractorId,
                    ToUserId = workerId,
                    ContractId = contract.Id
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await SafeNotify(contract.ContractorUsername, "تأكيد حضور", $"تم تأكيد حضور العامل - العقد #{contract.Id}");
            await SafeNotify(contract.EngineerUsername, "تأكيد حضور", $"تم تأكيد حضور العامل - العقد #{contract.Id}");
            await SafeNotify(contract.WorkerUsername, "تحويل مبلغ", $"تم تحويل مبلغ العقد #{contract.Id}");

            return contract;
        }

        public async Task<Models.Contract> ApplyPenaltyAsync(int contractId, string actorUsername)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");

            // Only contractor or engineer can apply penalty
            if (actorUsername != contract.ContractorUsername && actorUsername != contract.EngineerUsername)
                throw new UnauthorizedAccessException("فقط المقاول أو المهندس يمكنه تطبيق الشرط الجزائي");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var contractorId = await GetUserIdByUsername(contract.ContractorUsername);
                var engineerId = await GetUserIdByUsername(contract.EngineerUsername);
                var contractorWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contractorId);
                var engineerWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == engineerId);

                if (contractorWallet == null || engineerWallet == null)
                    throw new InvalidOperationException("محافظ اطراف العقد غير موجوده");

                var penalty = Math.Min(contract.EscrowAmount, contract.PenaltyClauseAmount);
                var contractorShare = penalty * (decimal)(contract.PenaltySplitContractorPercent / 100.0);
                var engineerShare = penalty * (decimal)(contract.PenaltySplitEngineerPercent / 100.0);

                contract.EscrowAmount -= penalty;
                contract.NoShowProcessed = true;
                contract.Status = "completed";

                contractorWallet.Balance += contractorShare;
                contractorWallet.UpdatedAt = DateTime.UtcNow;
                engineerWallet.Balance += engineerShare;
                engineerWallet.UpdatedAt = DateTime.UtcNow;

                _context.WalletTransactions.AddRange(
                    new WalletTransaction
                    {
                        UserId = contractorId,
                        Type = "penalty_distribution",
                        Amount = contractorShare,
                        Description = $"توزيع الشرط الجزائي من العقد #{contract.Id}",
                        ContractId = contract.Id
                    },
                    new WalletTransaction
                    {
                        UserId = engineerId,
                        Type = "penalty_distribution",
                        Amount = engineerShare,
                        Description = $"توزيع الشرط الجزائي من العقد #{contract.Id}",
                        ContractId = contract.Id
                    }
                );

                _context.FundMovementLogs.Add(new FundMovementLog
                {
                    ContractId = contract.Id,
                    InstallmentIndex = -1,
                    Action = "release",
                    Amount = penalty,
                    TriggeredBy = actorUsername,
                    Reason = "تطبيق الشرط الجزائي"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await SafeNotify(contract.ContractorUsername, "شرط جزائي", $"تم تطبيق الشرط الجزائي على العقد #{contract.Id}");
            await SafeNotify(contract.EngineerUsername, "شرط جزائي", $"تم تطبيق الشرط الجزائي على العقد #{contract.Id}");
            await SafeNotify(contract.WorkerUsername, "شرط جزائي", $"تم تطبيق الشرط الجزائي على العقد #{contract.Id}");

            return contract;
        }

        /// <summary>
        /// Fire-and-forget notification with error swallowing. Accepts username, resolves to user ID internally.
        /// </summary>
        private async Task SafeNotify(string username, string title, string body)
        {
            try
            {
                var userId = (await _context.Users.FirstOrDefaultAsync(u => u.UserName == username))?.Id;
                if (string.IsNullOrEmpty(userId)) return;
                await _notificationService.SendNotificationToUser(userId, title, body);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send notification to {Username}: {Title}", username, title);
            }
        }

        private async Task<string> GetUserIdByUsername(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            return user?.Id ?? throw new InvalidOperationException($"User '{username}' not found");
        }

        // ─── 2-PARTY CONTRACT METHODS ────────────────────────────────────────

        private async Task SafeNotifyDirect(string userId, string title, string body)
        {
            try
            {
                await _notificationService.SendNotificationToUser(userId, title, body);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send direct notification to User {UserId}: {Title}", userId, title);
            }
        }

        public async Task<Models.Contract> CreateSimpleContractAsync(CreateSimpleContractDto dto, string clientUserId)
        {
            var worker = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.WorkerUserId);
            if (worker == null)
                throw new InvalidOperationException("العامل المحدد غير موجود");

            var clientWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == clientUserId);
            if (clientWallet == null)
            {
                clientWallet = new UserWallet { UserId = clientUserId };
                _context.UserWallets.Add(clientWallet);
                await _context.SaveChangesAsync();
            }

            var totalWages = dto.DailySalary * dto.DurationDays;
            var requiredEscrow = totalWages + dto.PenaltyClauseAmount;

            if (clientWallet.Balance < requiredEscrow)
                throw new InvalidOperationException($"الرصيد غير كافٍ. تحتاج إلى {requiredEscrow} جنيه لشحن العقد والشرط الجزائي.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                clientWallet.Balance -= requiredEscrow;
                clientWallet.UpdatedAt = DateTime.UtcNow;

                var contract = new Models.Contract
                {
                    ClientUserId = clientUserId,
                    WorkerUserId = dto.WorkerUserId,
                    WorkerUsername = worker.UserName ?? string.Empty,
                    ContractorUsername = string.Empty,
                    EngineerUsername = string.Empty,
                    IsSimpleContract = true,
                    DurationDays = dto.DurationDays,
                    DailySalary = dto.DailySalary,
                    DailyAmount = dto.DailySalary,
                    AgreedTotalAmount = totalWages,
                    SplitDays = dto.DurationDays,
                    WorkplaceAddress = dto.WorkplaceAddress,
                    WorkLocation = dto.WorkplaceAddress,
                    Notes = dto.Notes,
                    PenaltyClauseAmount = dto.PenaltyClauseAmount,
                    ClientPenaltyPaid = true,
                    WorkerPenaltyPaid = false,
                    EscrowAmount = requiredEscrow,
                    Status = "pending_signatures",
                    HistoryJson = JsonSerializer.Serialize(new[]
                    {
                        new { id = Guid.NewGuid().ToString(), type = "system", message = "تم إنشاء عقد بسيط وبانتظار موافقة العامل وتأمين الشرط الجزائي", createdAt = DateTime.UtcNow }
                    })
                };

                _context.Contracts.Add(contract);

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = clientUserId,
                    Type = "escrow_lock",
                    Amount = requiredEscrow,
                    Description = $"حجز مبلغ العقد والشرط الجزائي للعقد #{contract.Id}",
                    ContractId = contract.Id
                });

                _context.FundMovementLogs.Add(new FundMovementLog
                {
                    ContractId = contract.Id,
                    InstallmentIndex = -1,
                    Action = "lock",
                    Amount = requiredEscrow,
                    TriggeredBy = clientUserId,
                    Reason = "إنشاء العقد وتأمين الضمان"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await SafeNotifyDirect(dto.WorkerUserId, "عقد عمل جديد", $"تم إرسال عقد عمل جديد لك بقيمة يومية {dto.DailySalary} جنيه وبانتظار موافقتك.");

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Models.Contract> WorkerRespondAsync(int contractId, string workerUserId, bool accept)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == contractId && c.WorkerUserId == workerUserId && c.IsSimpleContract)
                ?? throw new KeyNotFoundException("العقد المحدد غير موجود أو لست مخولاً للموافقة عليه");

            if (contract.Status != "pending_signatures")
                throw new InvalidOperationException("هذا العقد غير معلق للتوقيع أو تمت معالجته بالفعل");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (accept)
                {
                    var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == workerUserId);
                    if (workerWallet == null)
                    {
                        workerWallet = new UserWallet { UserId = workerUserId };
                        _context.UserWallets.Add(workerWallet);
                        await _context.SaveChangesAsync();
                    }

                    if (workerWallet.Balance < contract.PenaltyClauseAmount)
                        throw new InvalidOperationException($"الرصيد غير كافٍ. تحتاج إلى {contract.PenaltyClauseAmount} جنيه لتأمين الشرط الجزائي للموافقة على العقد.");

                    workerWallet.Balance -= contract.PenaltyClauseAmount;
                    workerWallet.UpdatedAt = DateTime.UtcNow;

                    contract.WorkerPenaltyPaid = true;
                    contract.EscrowAmount += contract.PenaltyClauseAmount;
                    contract.Status = "active";

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = workerUserId,
                        Type = "escrow_lock",
                        Amount = contract.PenaltyClauseAmount,
                        Description = $"حجز الشرط الجزائي للعقد #{contract.Id}",
                        ContractId = contract.Id
                    });

                    _context.FundMovementLogs.Add(new FundMovementLog
                    {
                        ContractId = contract.Id,
                        InstallmentIndex = -1,
                        Action = "lock",
                        Amount = contract.PenaltyClauseAmount,
                        TriggeredBy = workerUserId,
                        Reason = "تأمين الشرط الجزائي من العامل وتفعيل العقد"
                    });

                    var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                    history.Add(new { id = Guid.NewGuid().ToString(), type = "activation", message = "تم تفعيل العقد وبدء سريانه بعد موافقة وتأمين الشرط الجزائي من العامل", createdAt = DateTime.UtcNow });
                    contract.HistoryJson = JsonSerializer.Serialize(history);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await SafeNotifyDirect(contract.ClientUserId!, "تم قبول العقد", $"وافق العامل على عقد العمل #{contract.Id} وبدأ سريانه.");
                }
                else
                {
                    var clientWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == contract.ClientUserId);
                    if (clientWallet != null)
                    {
                        clientWallet.Balance += contract.EscrowAmount;
                        clientWallet.UpdatedAt = DateTime.UtcNow;

                        _context.WalletTransactions.Add(new WalletTransaction
                        {
                            UserId = contract.ClientUserId!,
                            Type = "escrow_refund",
                            Amount = contract.EscrowAmount,
                            Description = $"استرداد قيمة العقد #{contract.Id} لرفض العامل",
                            ContractId = contract.Id
                        });
                    }

                    contract.EscrowAmount = 0;
                    contract.Status = "cancelled";

                    var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                    history.Add(new { id = Guid.NewGuid().ToString(), type = "rejection", message = "تم رفض العقد من قبل العامل وتم إرجاع الرصيد للمقاول/العميل", createdAt = DateTime.UtcNow });
                    contract.HistoryJson = JsonSerializer.Serialize(history);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await SafeNotifyDirect(contract.ClientUserId!, "رفض العقد", $"رفض العامل عقد العمل #{contract.Id} وتم إعادة الرصيد لمحفظتك.");
                }

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Models.Contract> WorkerCheckInAsync(int contractId, string workerUserId)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == contractId && c.WorkerUserId == workerUserId && c.IsSimpleContract)
                ?? throw new KeyNotFoundException("العقد غير موجود أو لست مسجلاً كعامل به");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط حالياً");

            var todayEgypt = DateTime.UtcNow.AddHours(3).Date;
            if (contract.CheckInDate == todayEgypt)
                throw new InvalidOperationException("لقد قمت بتسجيل الحضور اليوم بالفعل");

            contract.CheckInDate = todayEgypt;
            contract.CheckInTime = DateTime.UtcNow;
            contract.CheckInStatus = "pending";

            var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
            history.Add(new { id = Guid.NewGuid().ToString(), type = "checkin", message = $"سجل العامل وصوله لبدء الوردية", createdAt = DateTime.UtcNow });
            contract.HistoryJson = JsonSerializer.Serialize(history);

            await _context.SaveChangesAsync();

            await SafeNotifyDirect(contract.ClientUserId!, "حضور العامل", $"سجل العامل وصوله للعمل اليوم. يرجى تأكيد الحضور - العقد #{contract.Id}.");

            return contract;
        }

        public async Task<Models.Contract> ClientApproveCheckInAsync(int contractId, string clientUserId)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == contractId && c.ClientUserId == clientUserId && c.IsSimpleContract)
                ?? throw new KeyNotFoundException("العقد غير موجود أو لست مسجلاً كعميل به");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");

            if (contract.CheckInStatus != "pending")
                throw new InvalidOperationException("لا يوجد طلب حضور معلق بانتظار التأكيد حالياً");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == contract.WorkerUserId);
                if (workerWallet == null)
                    throw new InvalidOperationException("محفظة العامل غير موجودة");

                var amount = contract.DailySalary;
                contract.EscrowAmount -= amount;
                contract.DaysWorked += 1;
                contract.CheckInStatus = "approved";

                workerWallet.Balance += amount;
                workerWallet.UpdatedAt = DateTime.UtcNow;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = contract.WorkerUserId!,
                    Type = "installment_release",
                    Amount = amount,
                    Description = $"صرف قسط - تأكيد المقاول - العقد #{contract.Id}",
                    ContractId = contract.Id
                });

                _context.FundMovementLogs.Add(new FundMovementLog
                {
                    ContractId = contract.Id,
                    InstallmentIndex = contract.DaysWorked - 1,
                    Action = "release",
                    Amount = amount,
                    TriggeredBy = clientUserId,
                    Reason = "تأكيد المقاول لحضور العامل"
                });

                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    ContractId = contract.Id,
                    Date = contract.CheckInDate!.Value,
                    Status = "attended",
                    MarkedBy = clientUserId,
                    CheckInTime = contract.CheckInTime
                });

                var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                history.Add(new { id = Guid.NewGuid().ToString(), type = "attendance_approved", message = "تم تأكيد حضور العامل من قبل العميل وتم صرف اليومية", createdAt = DateTime.UtcNow });

                if (contract.DaysWorked == contract.DurationDays)
                {
                    contract.Status = "completed";

                    var clientWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == clientUserId);
                    if (clientWallet != null)
                    {
                        clientWallet.Balance += contract.PenaltyClauseAmount;
                        clientWallet.UpdatedAt = DateTime.UtcNow;
                        _context.WalletTransactions.Add(new WalletTransaction
                        {
                            UserId = clientUserId,
                            Type = "penalty_refund",
                            Amount = contract.PenaltyClauseAmount,
                            Description = $"استرداد الشرط الجزائي لاكتمال العقد #{contract.Id}",
                            ContractId = contract.Id
                        });
                    }

                    workerWallet.Balance += contract.PenaltyClauseAmount;
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = contract.WorkerUserId!,
                        Type = "penalty_refund",
                        Amount = contract.PenaltyClauseAmount,
                        Description = $"استرداد الشرط الجزائي لاكتمال العقد #{contract.Id}",
                        ContractId = contract.Id
                    });

                    contract.EscrowAmount -= (2 * contract.PenaltyClauseAmount);

                    history.Add(new { id = Guid.NewGuid().ToString(), type = "completion", message = "تم اكتمال مدة العقد بنجاح والإفراج عن الضمانات للطرفين", createdAt = DateTime.UtcNow });

                    await SafeNotifyDirect(contract.ClientUserId!, "اكتمال العقد", $"اكتمل العقد #{contract.Id} بنجاح وتم إعادة الشرط الجزائي لمحفظتك.");
                    await SafeNotifyDirect(contract.WorkerUserId!, "اكتمال العقد", $"اكتمل العقد #{contract.Id} بنجاح وتم إعادة الشرط الجزائي لمحفظتك.");
                }
                else
                {
                    await SafeNotifyDirect(contract.WorkerUserId!, "تأكيد حضور", $"تم تأكيد حضورك اليوم وصرف {amount} جنيه للعقد #{contract.Id}.");
                }

                contract.HistoryJson = JsonSerializer.Serialize(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Models.Contract> ClientReportNoShowAsync(int contractId, string clientUserId)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == contractId && c.ClientUserId == clientUserId && c.IsSimpleContract)
                ?? throw new KeyNotFoundException("العقد غير موجود أو لست مسجلاً كعميل به");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");

            if (contract.CheckInStatus != "pending")
                throw new InvalidOperationException("لا يوجد طلب حضور معلق للإبلاغ عنه");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                contract.CheckInStatus = "reported";
                contract.Status = "paused";

                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    ContractId = contract.Id,
                    Date = contract.CheckInDate!.Value,
                    Status = "absent",
                    MarkedBy = clientUserId,
                    CheckInTime = contract.CheckInTime
                });

                var complaint = new EgyptOnline.Models.Complaint
                {
                    ReporterUserId = clientUserId,
                    ContractId = contract.Id,
                    Reason = "no_show",
                    Description = $"بلاغ غياب: أبلغ العميل أن العامل لم يلتزم بالعمل وحضر بالتسجيل فقط اليوم {contract.CheckInDate!.Value:dd/MM/yyyy}.",
                    Status = "open",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Complaints.Add(complaint);

                var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                history.Add(new { id = Guid.NewGuid().ToString(), type = "no_show_reported", message = "تم تسجيل غياب العامل وإحالة الخلاف إلى قسم المنازعات وتجميد العقد", createdAt = DateTime.UtcNow });
                contract.HistoryJson = JsonSerializer.Serialize(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await SafeNotifyDirect(contract.WorkerUserId!, "تم الإبلاغ عن غيابك", $"أبلغ العميل عن غيابك اليوم للعقد #{contract.Id} وتم تجميد العقد للمراجعة.");
                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Models.Contract> TerminateMutualAsync(int contractId, string actorUserId, bool accept)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsSimpleContract)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.ClientUserId != actorUserId && contract.WorkerUserId != actorUserId)
                throw new UnauthorizedAccessException("غير مصرح لك بإنهاء هذا العقد");

            if (contract.Status != "active" && contract.Status != "paused")
                throw new InvalidOperationException("يجب أن يكون العقد نشطاً أو مجمداً لإنشائه ودياً");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (contract.ClientUserId == actorUserId)
                {
                    contract.ClientTerminationRequested = accept;
                }
                else if (contract.WorkerUserId == actorUserId)
                {
                    contract.WorkerTerminationRequested = accept;
                }

                var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();

                if (contract.ClientTerminationRequested && contract.WorkerTerminationRequested)
                {
                    var clientWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == contract.ClientUserId);
                    var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == contract.WorkerUserId);

                    if (clientWallet == null || workerWallet == null)
                        throw new InvalidOperationException("محافظ أطراف العقد غير موجودة");

                    var wagesRemaining = (contract.DurationDays - contract.DaysWorked) * contract.DailySalary;
                    var clientRefund = wagesRemaining + contract.PenaltyClauseAmount;
                    var workerRefund = contract.PenaltyClauseAmount;

                    clientWallet.Balance += clientRefund;
                    clientWallet.UpdatedAt = DateTime.UtcNow;

                    workerWallet.Balance += workerRefund;
                    workerWallet.UpdatedAt = DateTime.UtcNow;

                    contract.EscrowAmount = 0;
                    contract.Status = "cancelled";
                    contract.CancelledAt = DateTime.UtcNow;
                    contract.CancelledBy = "Mutual";

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = contract.ClientUserId!,
                        Type = "escrow_refund",
                        Amount = clientRefund,
                        Description = $"استرداد رصيد الأجر المتبقي والضمان لإنهاء ودي للعقد #{contract.Id}",
                        ContractId = contract.Id
                    });

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = contract.WorkerUserId!,
                        Type = "penalty_refund",
                        Amount = workerRefund,
                        Description = $"استرداد الضمان لإنهاء ودي للعقد #{contract.Id}",
                        ContractId = contract.Id
                    });

                    _context.FundMovementLogs.Add(new FundMovementLog
                    {
                        ContractId = contract.Id,
                        InstallmentIndex = -1,
                        Action = "release",
                        Amount = wagesRemaining + (2 * contract.PenaltyClauseAmount),
                        TriggeredBy = "Mutual",
                        Reason = "إنهاء ودي بالتراضي بين الطرفين"
                    });

                    history.Add(new { id = Guid.NewGuid().ToString(), type = "mutual_termination", message = "تم إنهاء العقد بالتراضي واسترداد الضمانات للطرفين", createdAt = DateTime.UtcNow });
                    contract.HistoryJson = JsonSerializer.Serialize(history);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await SafeNotifyDirect(contract.ClientUserId!, "إنهاء العقد بالتراضي", $"تم إنهاء العقد #{contract.Id} ودياً وتم تسوية الحسابات وإرجاع الضمان لمحفظتك.");
                    await SafeNotifyDirect(contract.WorkerUserId!, "إنهاء العقد بالتراضي", $"تم إنهاء العقد #{contract.Id} ودياً وتم إرجاع الضمان لمحفظتك.");
                }
                else
                {
                    var requestMsg = contract.ClientUserId == actorUserId 
                        ? "المقاول/العميل يطلب إنهاء العقد بالتراضي" 
                        : "العامل يطلب إنهاء العقد بالتراضي";

                    history.Add(new { id = Guid.NewGuid().ToString(), type = "termination_requested", message = requestMsg, createdAt = DateTime.UtcNow });
                    contract.HistoryJson = JsonSerializer.Serialize(history);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var otherUserId = contract.ClientUserId == actorUserId ? contract.WorkerUserId! : contract.ClientUserId!;
                    await SafeNotifyDirect(otherUserId, "طلب إنهاء عقد ودي", $"طلب الطرف الآخر إنهاء العقد #{contract.Id} ودياً بالتراضي. يرجى تأكيد القبول.");
                }

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Models.Contract> TerminateByClientAsync(int contractId, string clientUserId)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == contractId && c.ClientUserId == clientUserId && c.IsSimpleContract)
                ?? throw new KeyNotFoundException("العقد غير موجود أو لست مسجلاً كعميل به");

            if (contract.Status != "active" && contract.Status != "paused")
                throw new InvalidOperationException("لا يمكن إلغاء العقد في حالته الحالية");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var clientWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == clientUserId);
                if (clientWallet == null)
                    throw new InvalidOperationException("محفظتك غير موجودة");

                var wagesRemaining = (contract.DurationDays - contract.DaysWorked) * contract.DailySalary;
                var totalRefund = wagesRemaining + (2 * contract.PenaltyClauseAmount);

                clientWallet.Balance += totalRefund;
                clientWallet.UpdatedAt = DateTime.UtcNow;

                contract.EscrowAmount = 0;
                contract.Status = "cancelled";
                contract.CancelledAt = DateTime.UtcNow;
                contract.CancelledBy = clientUserId;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = clientUserId,
                    Type = "escrow_refund",
                    Amount = totalRefund,
                    Description = $"استرداد المتبقي ومصادرة الشرط الجزائي للعقد #{contract.Id}",
                    ContractId = contract.Id
                });

                _context.FundMovementLogs.Add(new FundMovementLog
                {
                    ContractId = contract.Id,
                    InstallmentIndex = -1,
                    Action = "release",
                    Amount = totalRefund,
                    TriggeredBy = clientUserId,
                    Reason = "إلغاء أحادي من قبل العميل ومصادرة الشرط الجزائي"
                });

                var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                history.Add(new { id = Guid.NewGuid().ToString(), type = "client_termination", message = "قام العميل بإنهاء العقد من طرف واحد وتمت مصادرة الشرط الجزائي لصالحه", createdAt = DateTime.UtcNow });
                contract.HistoryJson = JsonSerializer.Serialize(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await SafeNotifyDirect(contract.ClientUserId!, "إنهاء العقد من طرفك", $"قمت بإنهاء العقد #{contract.Id} من طرف واحد واسترداد أجر الأيام غير المنجزة ومصادرة الشرط الجزائي.");
                await SafeNotifyDirect(contract.WorkerUserId!, "إنهاء العقد من قبل العميل", $"قام العميل بإنهاء العقد #{contract.Id} أحادياً وتم خصم ومصادرة الشرط الجزائي منك لصالح العميل.");

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Models.Contract> TerminateByWorkerAsync(int contractId, string workerUserId)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == contractId && c.WorkerUserId == workerUserId && c.IsSimpleContract)
                ?? throw new KeyNotFoundException("العقد غير موجود أو لست مسجلاً كعامل به");

            if (contract.Status != "active" && contract.Status != "paused")
                throw new InvalidOperationException("لا يمكن إلغاء العقد في حالته الحالية");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var clientWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == contract.ClientUserId);
                if (clientWallet == null)
                    throw new InvalidOperationException("محفظة العميل غير موجودة");

                var wagesRemaining = (contract.DurationDays - contract.DaysWorked) * contract.DailySalary;
                var totalRefund = wagesRemaining + (2 * contract.PenaltyClauseAmount);

                clientWallet.Balance += totalRefund;
                clientWallet.UpdatedAt = DateTime.UtcNow;

                contract.EscrowAmount = 0;
                contract.Status = "cancelled";
                contract.CancelledAt = DateTime.UtcNow;
                contract.CancelledBy = workerUserId;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = contract.ClientUserId!,
                    Type = "escrow_refund",
                    Amount = totalRefund,
                    Description = $"استرداد ومصادرة الشرط الجزائي لانسحاب العامل - العقد #{contract.Id}",
                    ContractId = contract.Id
                });

                _context.FundMovementLogs.Add(new FundMovementLog
                {
                    ContractId = contract.Id,
                    InstallmentIndex = -1,
                    Action = "release",
                    Amount = totalRefund,
                    TriggeredBy = workerUserId,
                    Reason = "انسحاب العامل أحادياً ومصادرة الشرط الجزائي لصالح العميل"
                });

                var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                history.Add(new { id = Guid.NewGuid().ToString(), type = "worker_termination", message = "قام العامل بالانسحاب وإنهاء العقد أحادياً ومصادرة الشرط الجزائي لصالح العميل", createdAt = DateTime.UtcNow });
                contract.HistoryJson = JsonSerializer.Serialize(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await SafeNotifyDirect(contract.ClientUserId!, "انسحاب العامل وإنهاء العقد", $"انسحب العامل من العقد #{contract.Id} أحادياً، وتم تسوية أجر الأيام المتبقية والشرط الجزائي لصالح محفظتك.");
                await SafeNotifyDirect(contract.WorkerUserId!, "إنهاء العقد من طرفك", $"قمت بإنهاء العقد #{contract.Id} أحادياً وتم مصادرة الشرط الجزائي منك لصالح العميل.");

                return contract;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task ProcessAutoPayoutsAsync(DateTime egyptTime)
        {
            var todayEgypt = egyptTime.Date;
            var isPast5Pm = egyptTime.TimeOfDay >= new TimeSpan(17, 0, 0);

            var contractsToPayout = await _context.Contracts
                .Where(c => c.IsSimpleContract && c.Status == "active" && c.CheckInStatus == "pending" && c.CheckInDate.HasValue)
                .ToListAsync();

            foreach (var contract in contractsToPayout)
            {
                if (contract.CheckInDate.Value < todayEgypt || (contract.CheckInDate.Value == todayEgypt && isPast5Pm))
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contract.WorkerUserId);
                        if (workerWallet == null) continue;

                        var amount = contract.DailySalary;
                        contract.EscrowAmount -= amount;
                        contract.DaysWorked += 1;
                        contract.CheckInStatus = "approved";

                        workerWallet.Balance += amount;
                        workerWallet.UpdatedAt = DateTime.UtcNow;

                        _context.WalletTransactions.Add(new WalletTransaction
                        {
                            UserId = contract.WorkerUserId!,
                            Type = "installment_release",
                            Amount = amount,
                            Description = $"صرف تلقائي بعد فترة السماح - العقد #{contract.Id}",
                            ContractId = contract.Id
                        });

                        _context.FundMovementLogs.Add(new FundMovementLog
                        {
                            ContractId = contract.Id,
                            InstallmentIndex = contract.DaysWorked - 1,
                            Action = "release",
                            Amount = amount,
                            TriggeredBy = "System_AutoPayout",
                            Reason = "صرف تلقائي بعد مرور 3 ساعات سماح"
                        });

                        _context.AttendanceRecords.Add(new AttendanceRecord
                        {
                            ContractId = contract.Id,
                            Date = contract.CheckInDate.Value,
                            Status = "attended",
                            MarkedBy = "System_AutoPayout",
                            CheckInTime = contract.CheckInTime
                        });

                        var history = JsonSerializer.Deserialize<List<object>>(contract.HistoryJson) ?? new();
                        history.Add(new { id = Guid.NewGuid().ToString(), type = "auto_payout", message = "تم الصرف التلقائي لليومية بعد انتهاء المهلة", createdAt = DateTime.UtcNow });

                        if (contract.DaysWorked == contract.DurationDays)
                        {
                            contract.Status = "completed";

                            var clientWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contract.ClientUserId);
                            if (clientWallet != null)
                            {
                                clientWallet.Balance += contract.PenaltyClauseAmount;
                                clientWallet.UpdatedAt = DateTime.UtcNow;
                                _context.WalletTransactions.Add(new WalletTransaction
                                {
                                    UserId = contract.ClientUserId!,
                                    Type = "penalty_refund",
                                    Amount = contract.PenaltyClauseAmount,
                                    Description = $"استرداد الشرط الجزائي لاكتمال العقد #{contract.Id}",
                                    ContractId = contract.Id
                                });
                            }

                            workerWallet.Balance += contract.PenaltyClauseAmount;
                            _context.WalletTransactions.Add(new WalletTransaction
                            {
                                UserId = contract.WorkerUserId!,
                                Type = "penalty_refund",
                                Amount = contract.PenaltyClauseAmount,
                                Description = $"استرداد الشرط الجزائي لاكتمال العقد #{contract.Id}",
                                ContractId = contract.Id
                            });

                            contract.EscrowAmount -= (2 * contract.PenaltyClauseAmount);

                            history.Add(new { id = Guid.NewGuid().ToString(), type = "completion", message = "تم اكتمال مدة العقد بنجاح تلقائياً والإفراج عن الضمانات للطرفين", createdAt = DateTime.UtcNow });

                            await SafeNotifyDirect(contract.ClientUserId!, "اكتمال العقد", $"اكتمل العقد #{contract.Id} بنجاح وتم إعادة الشرط الجزائي لمحفظتك.");
                            await SafeNotifyDirect(contract.WorkerUserId!, "اكتمال العقد", $"اكتمل العقد #{contract.Id} بنجاح وتم إعادة الشرط الجزائي لمحفظتك.");
                        }
                        else
                        {
                            await SafeNotifyDirect(contract.ClientUserId!, "صرف قسط تلقائي", $"تم صرف اليومية للعامل تلقائياً لعدم اتخاذ إجراء قبل الساعة 5 مساءً - العقد #{contract.Id}");
                            await SafeNotifyDirect(contract.WorkerUserId!, "صرف قسط تلقائي", $"تم صرف اليومية لك تلقائياً للعقد #{contract.Id}");
                        }

                        contract.HistoryJson = JsonSerializer.Serialize(history);

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        Log.Error(ex, "Failed to auto payout contract {ContractId}", contract.Id);
                    }
                }
            }
        }
    }
}
