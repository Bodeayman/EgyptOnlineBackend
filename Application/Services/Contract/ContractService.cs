using System.Text.Json;
using EgyptOnline.Data;
using EgyptOnline.Dtos.Contract;
using EgyptOnline.Models;
using EgyptOnline.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EgyptOnline.Application.Services.Contract
{
    public class ContractService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public ContractService(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<Models.Contract> CreateContractAsync(CreateContractDto dto, string creatorUserId)
        {
            // Validate penalty distribution sums to 100%
            if (Math.Abs(dto.PenaltySplitContractorPercent + dto.PenaltySplitEngineerPercent - 100) > 0.01)
                throw new InvalidOperationException("مجموع نسب توزيع الجزاء يجب ان يساوي 100%");

            // Validate first working day is not in the past
            if (dto.FirstWorkingDay.HasValue && dto.FirstWorkingDay.Value.Date < DateTime.UtcNow.Date)
                throw new InvalidOperationException("اول يوم عمل لا يمكن ان يكون في الماضي");

            // Validate all 3 users exist
            var contractorExists = await _context.Users.AnyAsync(u => u.Id == dto.ContractorId);
            var engineerExists = await _context.Users.AnyAsync(u => u.Id == dto.EngineerId);
            var workerExists = await _context.Users.AnyAsync(u => u.Id == dto.WorkerId);

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
                ContractorId = dto.ContractorId,
                EngineerId = dto.EngineerId,
                WorkerId = dto.WorkerId,
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
                    [dto.ContractorId] = false,
                    [dto.EngineerId] = false,
                    [dto.WorkerId] = false
                }),
                HistoryJson = JsonSerializer.Serialize(new[]
                {
                    new { id = Guid.NewGuid().ToString(), type = "system", message = "تم انشاء العقد", createdAt = DateTime.UtcNow }
                })
            };

            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();

            // Notify all parties using Firebase
            await SafeNotify(dto.ContractorId, "عقد جديد", $"تم انشاء عقد جديد #{contract.Id}");
            await SafeNotify(dto.EngineerId, "عقد جديد", $"العقد #{contract.Id} بانتظار توقيعك");
            await SafeNotify(dto.WorkerId, "عقد جديد", $"العقد #{contract.Id} بانتظار توقيعك");

            return contract;
        }

        public async Task<Models.Contract?> GetByIdAsync(int id)
        {
            return await _context.Contracts.FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Models.Contract> SignContractAsync(int contractId, string userId, bool accepted)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            var approvals = JsonSerializer.Deserialize<Dictionary<string, bool>>(contract.ApprovalsJson) ?? new();

            // Verify user is a party to this contract
            if (!approvals.ContainsKey(userId))
                throw new UnauthorizedAccessException("ليس لديك صلاحية التوقيع على هذا العقد");

            approvals[userId] = accepted;
            contract.ApprovalsJson = JsonSerializer.Serialize(approvals);

            // If all three signed, lock escrow and activate
            if (approvals.TryGetValue(contract.ContractorId, out var c) && c &&
                approvals.TryGetValue(contract.EngineerId, out var e) && e &&
                approvals.TryGetValue(contract.WorkerId, out var w) && w)
            {
                // Check contractor wallet balance for escrow
                var contractorWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contract.ContractorId);
                if (contractorWallet != null && contractorWallet.Balance >= contract.AgreedTotalAmount)
                {
                    contractorWallet.Balance -= contract.AgreedTotalAmount;
                    contractorWallet.UpdatedAt = DateTime.UtcNow;
                    contract.EscrowAmount = contract.AgreedTotalAmount;
                    contract.Status = "active";

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = contract.ContractorId,
                        Type = "escrow_lock",
                        Amount = contract.AgreedTotalAmount,
                        Description = $"حجز مبلغ العقد #{contract.Id}",
                        ContractId = contract.Id
                    });
                }
            }

            await _context.SaveChangesAsync();
            await SafeNotify(userId, "توقيع العقد", $"تم توقيعك على العقد #{contract.Id}");

            return contract;
        }

        public async Task<Models.Contract> CancelContractAsync(int contractId, string actorUserId)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status == "cancelled")
                throw new InvalidOperationException("العقد ملغي بالفعل");
            if (contract.Status != "active")
                throw new InvalidOperationException("لا يمكن الغاء عقد غير نشط");

            // Verify actor is a party to this contract
            if (actorUserId != contract.ContractorId && actorUserId != contract.EngineerId && actorUserId != contract.WorkerId)
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
                    TriggeredBy = actorUserId,
                    Reason = "الغاء العقد"
                });

                contract.Status = "cancelled";
                contract.CancelledAt = DateTime.UtcNow;
                contract.CancelledBy = actorUserId;

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

            await SafeNotify(contract.ContractorId, "الغاء العقد", $"تم الغاء العقد #{contract.Id}");
            await SafeNotify(contract.EngineerId, "الغاء العقد", $"تم الغاء العقد #{contract.Id}");
            await SafeNotify(contract.WorkerId, "الغاء العقد", $"تم الغاء العقد #{contract.Id}");

            return contract;
        }

        public async Task<Models.Contract> MarkAttendanceAsync(int contractId, string actorUserId, string status)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");

            // Only contractor or engineer can mark attendance
            if (actorUserId != contract.ContractorId && actorUserId != contract.EngineerId)
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
                    MarkedBy = actorUserId
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
                            var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contract.WorkerId);
                            if (workerWallet != null)
                            {
                                workerWallet.Balance += amount;
                                workerWallet.UpdatedAt = DateTime.UtcNow;
                                contract.EscrowAmount -= amount;

                                _context.WalletTransactions.Add(new WalletTransaction
                                {
                                    UserId = contract.WorkerId,
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
                                    TriggeredBy = actorUserId,
                                    Reason = "حضور العامل"
                                });
                            }
                            released = true;
                        }
                        updatedInstallments.Add(dict);
                    }
                    contract.InstallmentsJson = JsonSerializer.Serialize(updatedInstallments);

                    await SafeNotify(contract.WorkerId, "حضور", $"تم تاكيد حضورك - العقد #{contract.Id}");
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
                                TriggeredBy = actorUserId,
                                Reason = "غياب العامل"
                            });
                            held = true;
                        }
                        updatedInstallments.Add(dict);
                    }
                    contract.InstallmentsJson = JsonSerializer.Serialize(updatedInstallments);

                    await SafeNotify(contract.WorkerId, "غياب", $"تم تسجيل غيابك - العقد #{contract.Id}");
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

        public async Task<Models.Contract> DisburseInstallmentAsync(int contractId, string actorUserId, int installmentIndex)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");

            // Only contractor or engineer can disburse
            if (actorUserId != contract.ContractorId && actorUserId != contract.EngineerId)
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
                var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contract.WorkerId)
                    ?? throw new InvalidOperationException("محفظة العامل غير موجوده");

                workerWallet.Balance += amount;
                workerWallet.UpdatedAt = DateTime.UtcNow;
                contract.EscrowAmount -= amount;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = contract.WorkerId,
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
                    TriggeredBy = actorUserId,
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

            await SafeNotify(contract.WorkerId, "صرف قسط", $"تم صرف القسط {installmentIndex + 1} لك - العقد #{contract.Id}");

            return contract;
        }

        public async Task<object> GetInstallmentsAsync(int contractId)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            return JsonSerializer.Deserialize<List<JsonElement>>(contract.InstallmentsJson) ?? new List<JsonElement>();
        }

        public async Task<Models.Contract> ConfirmArrivalAsync(int contractId, string actorUserId)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");
            if (contract.ArrivalConfirmed)
                throw new InvalidOperationException("تم تأكيد الحضور بالفعل");
            if (actorUserId != contract.ContractorId && actorUserId != contract.EngineerId)
                throw new UnauthorizedAccessException("فقط المقاول أو المهندس يمكنه تأكيد الحضور");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var workerWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contract.WorkerId);
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
                    UserId = contract.WorkerId,
                    Type = "transfer_in",
                    Amount = amount,
                    Description = $"دفعة العقد #{contract.Id}",
                    FromUserId = contract.ContractorId,
                    ToUserId = contract.WorkerId,
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

            await SafeNotify(contract.ContractorId, "تأكيد حضور", $"تم تأكيد حضور العامل - العقد #{contract.Id}");
            await SafeNotify(contract.EngineerId, "تأكيد حضور", $"تم تأكيد حضور العامل - العقد #{contract.Id}");
            await SafeNotify(contract.WorkerId, "تحويل مبلغ", $"تم تحويل مبلغ العقد #{contract.Id}");

            return contract;
        }

        public async Task<Models.Contract> ApplyPenaltyAsync(int contractId, string actorUserId)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId)
                ?? throw new KeyNotFoundException("العقد غير موجود");

            if (contract.Status != "active")
                throw new InvalidOperationException("العقد غير نشط");

            // Only contractor or engineer can apply penalty
            if (actorUserId != contract.ContractorId && actorUserId != contract.EngineerId)
                throw new UnauthorizedAccessException("فقط المقاول أو المهندس يمكنه تطبيق الشرط الجزائي");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var contractorWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contract.ContractorId);
                var engineerWallet = await _context.UserWallets.FirstOrDefaultAsync(uw => uw.UserId == contract.EngineerId);

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
                        UserId = contract.ContractorId,
                        Type = "penalty_distribution",
                        Amount = contractorShare,
                        Description = $"توزيع الشرط الجزائي من العقد #{contract.Id}",
                        ContractId = contract.Id
                    },
                    new WalletTransaction
                    {
                        UserId = contract.EngineerId,
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
                    TriggeredBy = actorUserId,
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

            await SafeNotify(contract.ContractorId, "شرط جزائي", $"تم تطبيق الشرط الجزائي على العقد #{contract.Id}");
            await SafeNotify(contract.EngineerId, "شرط جزائي", $"تم تطبيق الشرط الجزائي على العقد #{contract.Id}");
            await SafeNotify(contract.WorkerId, "شرط جزائي", $"تم تطبيق الشرط الجزائي على العقد #{contract.Id}");

            return contract;
        }

        /// <summary>
        /// Fire-and-forget notification with error swallowing
        /// </summary>
        private async Task SafeNotify(string userId, string title, string body)
        {
            try
            {
                await _notificationService.SendNotificationToUser(userId, title, body);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send notification to {UserId}: {Title}", userId, title);
            }
        }
    }
}
