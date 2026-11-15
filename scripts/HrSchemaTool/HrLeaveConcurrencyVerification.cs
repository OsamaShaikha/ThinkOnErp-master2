using System.Threading;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrLeaveConcurrencyVerification
{
    public static async Task Verify(string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        await using var connectionOne = new OracleConnection(safeConnectionString);
        await using var connectionTwo = new OracleConnection(safeConnectionString);
        await connectionOne.OpenAsync();
        await connectionTwo.OpenAsync();
        await SetTemplateSchemaAsync(connectionOne);
        await SetTemplateSchemaAsync(connectionTwo);

        var optionsOne = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connectionOne,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
        var optionsTwo = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connectionTwo,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
        await using var dbOne = new OracleDbContext(optionsOne);
        await using var dbTwo = new OracleDbContext(optionsTwo);
        await using var employeeLookup = connectionOne.CreateCommand();
        employeeLookup.CommandText = "SELECT EMPLOYEE_CODE FROM HR_EMPLOYEE WHERE ROWNUM = 1";
        var employeeCode = Convert.ToString(await employeeLookup.ExecuteScalarAsync());
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new InvalidOperationException("Leave concurrency verification requires an existing DEV_TEMPLATE employee.");

        await using var transactionOne = await dbOne.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await using var transactionTwo = await dbTwo.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var repositoryOne = new LeaveRepository(dbOne);
        var repositoryTwo = new LeaveRepository(dbTwo);
        var firstHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacksEnteredOutOfOrder = false;

        try
        {
            var first = repositoryOne.ExecuteForLeaveEmployeeAsync(employeeCode, async () =>
            {
                firstHasLock.TrySetResult();
                await releaseFirst.Task.WaitAsync(TimeSpan.FromSeconds(10));
                return true;
            });
            await firstHasLock.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var second = repositoryTwo.ExecuteForLeaveEmployeeAsync(employeeCode, () =>
            {
                secondHasLock.TrySetResult();
                return Task.FromResult(true);
            });
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            callbacksEnteredOutOfOrder = secondHasLock.Task.IsCompleted;

            releaseFirst.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            await transactionOne.RollbackAsync();
            await second.WaitAsync(TimeSpan.FromSeconds(10));
            await transactionTwo.RollbackAsync();

            if (callbacksEnteredOutOfOrder)
                throw new InvalidOperationException("A second Oracle session entered the employee-locked leave workflow before the first released its row lock.");
            if (!secondHasLock.Task.IsCompleted)
                throw new InvalidOperationException("The second Oracle session did not enter after the first transaction rolled back.");
            report.Add("Oracle LEV-12 employee lock: a second independent session waited for the employee row lock and entered after release; both sessions performed no DML and rolled back.");
        }
        finally
        {
            releaseFirst.TrySetResult();
            try { await transactionOne.RollbackAsync(); } catch { }
            try { await transactionTwo.RollbackAsync(); } catch { }
        }

        await VerifyConcurrentSubmissionsAsync(connectionString, report);
        await VerifyConcurrentApprovalAndCancellationAsync(connectionString, report);
        await VerifyConcurrentCancellationOfApprovedUntrackedAsync(connectionString, report);
    }

    private static async Task VerifyConcurrentCancellationOfApprovedUntrackedAsync(string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        await using var connectionSetup = new OracleConnection(safeConnectionString);
        await using var connectionOne = new OracleConnection(safeConnectionString);
        await using var connectionTwo = new OracleConnection(safeConnectionString);
        await connectionSetup.OpenAsync();
        await connectionOne.OpenAsync();
        await connectionTwo.OpenAsync();
        await SetTemplateSchemaAsync(connectionSetup);
        await SetTemplateSchemaAsync(connectionOne);
        await SetTemplateSchemaAsync(connectionTwo);

        static DbContextOptions<OracleDbContext> Options(OracleConnection connection) =>
            new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;

        await using var setupDb = new OracleDbContext(Options(connectionSetup));
        await using var dbOne = new OracleDbContext(Options(connectionOne));
        await using var dbTwo = new OracleDbContext(Options(connectionTwo));
        var employeeCode = await setupDb.Employees.AsNoTracking()
            .Where(e => e.BranchId.HasValue && setupDb.SysBranches.Any(b => b.Id == e.BranchId && b.CompanyId.HasValue))
            .Select(e => e.EmployeeCode).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new InvalidOperationException("Approved leave cancellation concurrency verification requires a DEV_TEMPLATE employee assigned to a company branch.");

        var token = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var leaveTypeCode = "LVA" + token[..12];
        var marker = "HR_LEAVE_APPROVED_CANCEL_CONCURRENCY_" + token[..12];
        var requestId = 0L;
        var date = DateTime.UtcNow.Date.AddDays(2);

        try
        {
            setupDb.LeaveTypes.Add(new LeaveType
            {
                LeaveTypeCode = leaveTypeCode,
                NameLocal = "تحقق إلغاء إجازة معتمدة",
                NameEn = "Temporary approved leave cancellation concurrency verification",
                IsPaid = true,
                TracksBalance = false,
                IsStatutory = false,
                MaxDaysPerYear = 1m,
                CarryForwardAllowed = false,
                CarryForwardCapDays = 0m,
                YearEndExcessAction = "EXPIRE",
                RequiresDocumentation = false,
                IsActive = true,
                CreationUser = marker,
                CreationDate = DateTime.UtcNow
            });
            var request = new LeaveRequest
            {
                EmployeeCode = employeeCode,
                LeaveTypeCode = leaveTypeCode,
                StartDate = date,
                EndDate = date,
                DaysRequested = 1m,
                Status = "APPROVED",
                Reason = marker,
                ApprovedBy = marker,
                ApprovalDate = DateTime.UtcNow,
                CreationUser = marker,
                CreationDate = DateTime.UtcNow
            };
            setupDb.LeaveRequests.Add(request);
            await setupDb.SaveChangesAsync();
            requestId = request.Id;

            var serviceOne = new LeaveService(new LeaveRepository(dbOne), new EmployeeRepository(dbOne), new HrUnitOfWork(dbOne));
            var serviceTwo = new LeaveService(new LeaveRepository(dbTwo), new EmployeeRepository(dbTwo), new HrUnitOfWork(dbTwo));
            async Task<(ThinkOnErp.Application.DTOs.Hr.LeaveRequestDto? Result, Exception? Error, string Reason)> CancelAsync(
                ILeaveService service, string reason)
            {
                try
                {
                    return (await service.CancelLeaveRequestAsync(employeeCode, requestId,
                        new CancelLeaveRequestDto(reason), marker), null, reason);
                }
                catch (Exception exception) { return (null, exception, reason); }
            }

            var reasons = new[] { "Concurrent approved cancellation A", "Concurrent approved cancellation B" };
            var outcomes = await Task.WhenAll(CancelAsync(serviceOne, reasons[0]), CancelAsync(serviceTwo, reasons[1]));
            var successful = outcomes.Where(x => x.Result?.Status == "CANCELLED").ToArray();
            var stale = outcomes.Where(x => x.Error is InvalidOperationException).ToArray();
            if (successful.Length != 1 || stale.Length != 1 || outcomes.Any(x => x.Error != null && x.Error is not InvalidOperationException))
                throw new InvalidOperationException("Concurrent Oracle cancellation of an approved untracked leave did not serialize to one cancellation and one stale-state rejection.");

            var finalRequest = await setupDb.LeaveRequests.AsNoTracking().SingleAsync(r => r.Id == requestId);
            var result = successful[0].Result!;
            if (finalRequest.Status != "CANCELLED" || finalRequest.RejectionReason != successful[0].Reason ||
                result.CancelledBy != marker || result.CancellationDate == null || result.CancellationReason != successful[0].Reason)
                throw new InvalidOperationException("The approved leave cancellation did not persist one consistent status, actor, date, and reason.");
            if (await setupDb.LeaveBalances.AsNoTracking().AnyAsync(b => b.EmployeeCode == employeeCode && b.LeaveTypeCode == leaveTypeCode) ||
                await setupDb.Set<LeaveBalanceMovement>().AsNoTracking().AnyAsync(m => m.EmployeeCode == employeeCode && m.LeaveTypeCode == leaveTypeCode))
                throw new InvalidOperationException("The untracked leave cancellation unexpectedly created or changed balance ledger data.");

            report.Add("Oracle LEV-12/14 approved-untracked cancellation concurrency: two independent real LeaveService sessions raced to cancel one APPROVED request; exactly one cancellation persisted with its actor/date/reason and the other saw a stale state. No leave balance or ledger movement was created. Temporary request/type fixtures were removed and verified absent; tracked USAGE reversal is still untested here.");
        }
        finally
        {
            await using var cleanupTransaction = await setupDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await setupDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_LEAVE_REQUEST WHERE ID = {requestId} AND CREATION_USER = {marker}");
            await setupDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_LEAVE_TYPE WHERE LEAVE_TYPE_CODE = {leaveTypeCode} AND CREATION_USER = {marker}");
            await cleanupTransaction.CommitAsync();
            setupDb.ChangeTracker.Clear();
            if (await setupDb.LeaveRequests.IgnoreQueryFilters().AsNoTracking().AnyAsync(r => r.Id == requestId && r.CreationUser == marker) ||
                await setupDb.LeaveTypes.IgnoreQueryFilters().AsNoTracking().AnyAsync(t => t.LeaveTypeCode == leaveTypeCode && t.CreationUser == marker))
                throw new InvalidOperationException($"Approved leave cancellation concurrency verifier cleanup failed for marker {marker}.");
        }
    }

    private static async Task VerifyConcurrentApprovalAndCancellationAsync(string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        await using var connectionSetup = new OracleConnection(safeConnectionString);
        await using var connectionOne = new OracleConnection(safeConnectionString);
        await using var connectionTwo = new OracleConnection(safeConnectionString);
        await connectionSetup.OpenAsync();
        await connectionOne.OpenAsync();
        await connectionTwo.OpenAsync();
        await SetTemplateSchemaAsync(connectionSetup);
        await SetTemplateSchemaAsync(connectionOne);
        await SetTemplateSchemaAsync(connectionTwo);

        static DbContextOptions<OracleDbContext> Options(OracleConnection connection) =>
            new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;

        await using var setupDb = new OracleDbContext(Options(connectionSetup));
        await using var dbOne = new OracleDbContext(Options(connectionOne));
        await using var dbTwo = new OracleDbContext(Options(connectionTwo));
        var employeeCode = await setupDb.Employees.AsNoTracking()
            .Where(e => e.BranchId.HasValue && setupDb.SysBranches.Any(b => b.Id == e.BranchId && b.CompanyId.HasValue))
            .Select(e => e.EmployeeCode).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new InvalidOperationException("Leave decision concurrency verification requires a DEV_TEMPLATE employee assigned to a company branch.");

        var token = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var leaveTypeCode = "LVD" + token[..12];
        var marker = "HR_LEAVE_DECISION_CONCURRENCY_" + token[..12];
        var requestId = 0L;
        var today = DateTime.UtcNow.Date;
        var start = today.AddDays(2);
        var end = start;

        try
        {
            setupDb.LeaveTypes.Add(new LeaveType
            {
                LeaveTypeCode = leaveTypeCode,
                NameLocal = "تحقق تنافس قرار الإجازة",
                NameEn = "Temporary leave decision concurrency verification",
                IsPaid = true,
                TracksBalance = false,
                IsStatutory = false,
                MaxDaysPerYear = 1m,
                CarryForwardAllowed = false,
                CarryForwardCapDays = 0m,
                YearEndExcessAction = "EXPIRE",
                RequiresDocumentation = false,
                IsActive = true,
                CreationUser = marker,
                CreationDate = DateTime.UtcNow
            });
            var request = new LeaveRequest
            {
                EmployeeCode = employeeCode,
                LeaveTypeCode = leaveTypeCode,
                StartDate = start,
                EndDate = end,
                DaysRequested = 1m,
                Status = "PENDING",
                Reason = marker,
                CreationUser = marker,
                CreationDate = DateTime.UtcNow
            };
            setupDb.LeaveRequests.Add(request);
            await setupDb.SaveChangesAsync();
            requestId = request.Id;

            var serviceOne = new LeaveService(new LeaveRepository(dbOne), new EmployeeRepository(dbOne), new HrUnitOfWork(dbOne));
            var serviceTwo = new LeaveService(new LeaveRepository(dbTwo), new EmployeeRepository(dbTwo), new HrUnitOfWork(dbTwo));
            async Task<(string? Status, Exception? Error)> ApproveAsync()
            {
                try
                {
                    var result = await serviceOne.ProcessLeaveRequestAsync(requestId,
                        new ProcessLeaveRequestDto(true, null), marker);
                    return (result.Status, null);
                }
                catch (Exception exception) { return (null, exception); }
            }
            async Task<(string? Status, Exception? Error)> CancelAsync()
            {
                try
                {
                    var result = await serviceTwo.CancelLeaveRequestAsync(employeeCode, requestId,
                        new CancelLeaveRequestDto("Concurrent Oracle pending decision verification"), marker);
                    return (result.Status, null);
                }
                catch (Exception exception) { return (null, exception); }
            }

            var outcomes = await Task.WhenAll(ApproveAsync(), CancelAsync());
            var finalRequest = await setupDb.LeaveRequests.AsNoTracking().SingleAsync(r => r.Id == requestId);
            var successfulDecisions = outcomes.Count(x => x.Status is "APPROVED" or "CANCELLED");
            var rejectedStaleDecisions = outcomes.Count(x => x.Error is InvalidOperationException);
            if (finalRequest.Status != "CANCELLED" || successfulDecisions is < 1 or > 2 ||
                rejectedStaleDecisions != 2 - successfulDecisions ||
                outcomes.Any(x => x.Error != null && x.Error is not InvalidOperationException))
                throw new InvalidOperationException("Concurrent Oracle approval/cancellation did not serialize to a valid terminal decision.");

            report.Add($"Oracle LEV-12/14 pending decision concurrency: independent real LeaveService sessions raced approval against cancellation; final state was CANCELLED with {successfulDecisions} successful serialized decision(s) and {rejectedStaleDecisions} stale-state rejection(s). The temporary untracked leave type creates no balance movements; request/type fixtures are removed and verified absent.");
        }
        finally
        {
            await using var cleanupTransaction = await setupDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await setupDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_LEAVE_REQUEST WHERE ID = {requestId} AND CREATION_USER = {marker}");
            await setupDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_LEAVE_TYPE WHERE LEAVE_TYPE_CODE = {leaveTypeCode} AND CREATION_USER = {marker}");
            await cleanupTransaction.CommitAsync();
            setupDb.ChangeTracker.Clear();
            if (await setupDb.LeaveRequests.IgnoreQueryFilters().AsNoTracking().AnyAsync(r => r.Id == requestId && r.CreationUser == marker) ||
                await setupDb.LeaveTypes.IgnoreQueryFilters().AsNoTracking().AnyAsync(t => t.LeaveTypeCode == leaveTypeCode && t.CreationUser == marker))
                throw new InvalidOperationException($"Leave decision concurrency verifier cleanup failed for marker {marker}.");
        }
    }

    private static async Task VerifyConcurrentSubmissionsAsync(string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        await using var connectionSetup = new OracleConnection(safeConnectionString);
        await using var connectionOne = new OracleConnection(safeConnectionString);
        await using var connectionTwo = new OracleConnection(safeConnectionString);
        await connectionSetup.OpenAsync();
        await connectionOne.OpenAsync();
        await connectionTwo.OpenAsync();
        await SetTemplateSchemaAsync(connectionSetup);
        await SetTemplateSchemaAsync(connectionOne);
        await SetTemplateSchemaAsync(connectionTwo);

        static DbContextOptions<OracleDbContext> Options(OracleConnection connection) =>
            new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;

        await using var setupDb = new OracleDbContext(Options(connectionSetup));
        await using var dbOne = new OracleDbContext(Options(connectionOne));
        await using var dbTwo = new OracleDbContext(Options(connectionTwo));
        var employeeCode = await setupDb.Employees.AsNoTracking()
            .Where(e => e.BranchId.HasValue && setupDb.SysBranches.Any(b => b.Id == e.BranchId && b.CompanyId.HasValue))
            .Select(e => e.EmployeeCode).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new InvalidOperationException("Leave request concurrency verification requires a DEV_TEMPLATE employee assigned to a company branch.");

        var token = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var leaveTypeCode = "LVC" + token[..12];
        var marker = "HR_LEAVE_CONCURRENCY_" + token[..12];
        var today = DateTime.UtcNow.Date;
        var cycleStart = new DateTime(today.Year, 1, 1);
        var cycleEnd = new DateTime(today.Year, 12, 31);
        var requestDate = today.AddDays(2);
        var requestDays = 0.6000m;
        var firstRequestId = 0L;

        try
        {
            setupDb.LeaveTypes.Add(new LeaveType
            {
                LeaveTypeCode = leaveTypeCode,
                NameLocal = "تحقق تزامن الإجازة",
                NameEn = "Temporary leave concurrency verification",
                IsPaid = true,
                TracksBalance = true,
                IsStatutory = false,
                MaxDaysPerYear = 1m,
                CarryForwardAllowed = false,
                CarryForwardCapDays = 0m,
                YearEndExcessAction = "EXPIRE",
                RequiresDocumentation = false,
                IsActive = true,
                CreationUser = marker,
                CreationDate = DateTime.UtcNow
            });
            setupDb.LeaveBalances.Add(new LeaveBalance
            {
                EmployeeCode = employeeCode,
                LeaveTypeCode = leaveTypeCode,
                YearNo = today.Year,
                CycleStartDate = cycleStart,
                CycleEndDate = cycleEnd,
                AccruedDays = 1m,
                UsedDays = 0m,
                CarriedForwardDays = 0m,
                CreationUser = marker,
                CreationDate = DateTime.UtcNow
            });
            await setupDb.SaveChangesAsync();

            var serviceOne = new LeaveService(new LeaveRepository(dbOne), new EmployeeRepository(dbOne), new HrUnitOfWork(dbOne));
            var serviceTwo = new LeaveService(new LeaveRepository(dbTwo), new EmployeeRepository(dbTwo), new HrUnitOfWork(dbTwo));
            var dto = new SubmitLeaveRequestDto(employeeCode, leaveTypeCode, requestDate, requestDate, requestDays, marker, null);

            async Task<(long? Id, Exception? Error)> SubmitAsync(ILeaveService service)
            {
                try
                {
                    var result = await service.SubmitLeaveRequestAsync(dto, marker, CancellationToken.None);
                    return (result.Id, null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            var results = await Task.WhenAll(SubmitAsync(serviceOne), SubmitAsync(serviceTwo));
            var successes = results.Where(x => x.Id.HasValue).ToArray();
            var failures = results.Where(x => x.Error != null).ToArray();
            if (successes.Length != 1 || failures.Length != 1 || failures[0].Error is not InvalidOperationException rejected ||
                !rejected.Message.Contains("Insufficient leave balance", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Concurrent Oracle leave requests did not serialize into one accepted request and one insufficient-balance rejection.");
            firstRequestId = successes[0].Id!.Value;

            var persisted = await setupDb.LeaveRequests.AsNoTracking()
                .Where(r => r.EmployeeCode == employeeCode && r.LeaveTypeCode == leaveTypeCode && r.CreationUser == marker)
                .ToListAsync();
            if (persisted.Count != 1 || persisted[0].Id != firstRequestId || persisted[0].Status != "PENDING" ||
                persisted.Sum(r => r.DaysRequested) > 1m)
                throw new InvalidOperationException("Concurrent Oracle leave submission left an invalid pending balance reservation.");

            async Task<(ThinkOnErp.Application.DTOs.Hr.LeaveRequestDto? Result, Exception? Error)> CancelAsync(ILeaveService service)
            {
                try
                {
                    return (await service.CancelLeaveRequestAsync(employeeCode, firstRequestId,
                        new CancelLeaveRequestDto("Concurrent Oracle cancellation verification"), marker), null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            var decisions = await Task.WhenAll(CancelAsync(serviceOne), CancelAsync(serviceTwo));
            var cancelledResults = decisions.Where(x => x.Result?.Status == "CANCELLED").ToArray();
            var rejectedDuplicates = decisions.Where(x => x.Error is InvalidOperationException).ToArray();
            if (cancelledResults.Length != 1 || rejectedDuplicates.Length != 1)
                throw new InvalidOperationException("Concurrent Oracle cancellations did not serialize into one cancellation and one stale-state rejection.");

            var finalRequest = await setupDb.LeaveRequests.AsNoTracking().SingleAsync(r => r.Id == firstRequestId);
            var finalBalance = await setupDb.LeaveBalances.AsNoTracking()
                .SingleAsync(b => b.EmployeeCode == employeeCode && b.LeaveTypeCode == leaveTypeCode && b.CreationUser == marker);
            var movements = await setupDb.Set<LeaveBalanceMovement>().AsNoTracking()
                .Where(m => m.BalanceId == finalBalance.Id).OrderBy(m => m.SequenceNo).ToListAsync();
            if (finalRequest.Status != "CANCELLED" || finalBalance.UsedDays != 0m || movements.Count != 0)
                throw new InvalidOperationException("Concurrent Oracle cancellation left a non-cancelled request, a balance change, or an unexpected ledger movement.");

            report.Add("Oracle LEV-12/14 leave concurrency: two independent sessions called the real LeaveService. Competing submissions for a temporary one-day balance produced one PENDING request and one insufficient-balance rejection. Simultaneous cancellation of that PENDING request produced one CANCELLED result and one stale-state rejection, leaving used balance zero and no ledger movements. All marked fixtures are removed and verified absent; concurrent cancellation of an APPROVED request is not covered because its append-only usage/reversal ledger requires an isolated rollback schema.");
        }
        finally
        {
            await using var cleanupTransaction = await setupDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await setupDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_LEAVE_REQUEST WHERE LEAVE_TYPE_CODE = {leaveTypeCode} AND CREATION_USER = {marker}");
            await setupDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_LEAVE_BALANCE WHERE EMPLOYEE_CODE = {employeeCode} AND LEAVE_TYPE_CODE = {leaveTypeCode} AND CREATION_USER = {marker}");
            await setupDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_LEAVE_TYPE WHERE LEAVE_TYPE_CODE = {leaveTypeCode} AND CREATION_USER = {marker}");
            await cleanupTransaction.CommitAsync();
            setupDb.ChangeTracker.Clear();
            if (await setupDb.LeaveRequests.IgnoreQueryFilters().AsNoTracking().AnyAsync(r => r.LeaveTypeCode == leaveTypeCode && r.CreationUser == marker) ||
                await setupDb.LeaveBalances.IgnoreQueryFilters().AsNoTracking().AnyAsync(b => b.EmployeeCode == employeeCode && b.LeaveTypeCode == leaveTypeCode && b.CreationUser == marker) ||
                await setupDb.LeaveTypes.IgnoreQueryFilters().AsNoTracking().AnyAsync(t => t.LeaveTypeCode == leaveTypeCode && t.CreationUser == marker))
                throw new InvalidOperationException($"Leave concurrency verifier cleanup failed for marker {marker}; temporary request ID was {firstRequestId}.");
        }
    }

    private static async Task SetTemplateSchemaAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA = DEV_TEMPLATE";
        await command.ExecuteNonQueryAsync();
    }
}

