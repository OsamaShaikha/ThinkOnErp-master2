using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed partial class LeaveRepository : ILeaveRepository
{
    public async Task<T> ExecuteForLeaveEmployeeAsync<T>(string employeeCode, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            if (!await _context.Employees.AsNoTracking().AnyAsync(e => e.EmployeeCode == employeeCode, cancellationToken))
                throw new KeyNotFoundException("Employee not found in the current HR scope.");
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = "SELECT 1 FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE = :employeeCode FOR UPDATE";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "employeeCode";
            parameter.Value = employeeCode;
            command.Parameters.Add(parameter);
            if (await command.ExecuteScalarAsync(cancellationToken) == null)
                throw new KeyNotFoundException("Employee not found in the current HR scope.");
            foreach (var entry in _context.ChangeTracker.Entries<LeaveBalance>().Where(e => e.Entity.EmployeeCode == employeeCode).ToList())
                await entry.ReloadAsync(cancellationToken);
            var result = await action();
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }
            throw;
        }
    }

    public Task EnsureLeaveCycleOpenAsync(string employeeCode, string leaveTypeCode, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        => EnsureLeaveCycleOpenCoreAsync(employeeCode, leaveTypeCode, startDate, endDate, null, cancellationToken);

    private async Task EnsureLeaveCycleOpenCoreAsync(string employeeCode, string leaveTypeCode, DateTime startDate, DateTime endDate, long? exceptCloseId, CancellationToken cancellationToken)
    {
        // Writers lock employee then type. Close locks its employees in order,
        // then all covered types. This also protects employees absent from a draft.
        await LockLeaveTypesAsync(leaveTypeCode, cancellationToken);
        var start = startDate.Date;
        var end = endDate.Date;
        var closed = await _context.LeaveYearCloses.AsNoTracking().AnyAsync(c => c.Status == "POSTED" &&
            (!exceptCloseId.HasValue || c.Id != exceptCloseId.Value) && c.FromYear == start.Year &&
            (c.LeaveTypeCode == "*" || c.LeaveTypeCode == leaveTypeCode), cancellationToken);
        closed |= await _context.LeaveYearCloseLines.AsNoTracking().AnyAsync(l =>
            l.EmployeeCode == employeeCode && l.LeaveTypeCode == leaveTypeCode &&
            l.Close!.Status == "POSTED" && (!exceptCloseId.HasValue || l.CloseId != exceptCloseId.Value) &&
            ((l.SourceCycleStartDate.HasValue && l.SourceCycleEndDate.HasValue && l.SourceCycleStartDate <= end && l.SourceCycleEndDate >= start) ||
             ((!l.SourceCycleStartDate.HasValue || !l.SourceCycleEndDate.HasValue) && l.SourceYear >= start.Year && l.SourceYear <= end.Year)), cancellationToken);
        if (closed)
            throw new InvalidOperationException("This leave cycle is closed. Unpost its close through the authorized workflow before changing it.");
    }

    private async Task LockLeaveTypesAsync(string leaveTypeCode, CancellationToken cancellationToken)
    {
        // EF InMemory has neither transactions nor database locks. Functional HTTP tests
        // still exercise the cycle checks below; Oracle runs the lock query as designed.
        if (!_context.Database.IsRelational()) return;
        if (_context.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Leave-cycle checks must run inside the employee write transaction.");
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction.GetDbTransaction();
        command.CommandText = "SELECT LEAVE_TYPE_CODE FROM HR_LEAVE_TYPE WHERE (:typeCode = '*' OR LEAVE_TYPE_CODE = :selectedTypeCode) ORDER BY LEAVE_TYPE_CODE FOR UPDATE";
        foreach (var name in new[] { "typeCode", "selectedTypeCode" })
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = leaveTypeCode;
            command.Parameters.Add(parameter);
        }
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var found = false;
        while (await reader.ReadAsync(cancellationToken)) found = true;
        if (!found && leaveTypeCode != "*") throw new KeyNotFoundException("Leave type not found.");
    }

    private static string NormalizeCloseExcessAction(string? action)
    {
        var normalized = string.IsNullOrWhiteSpace(action) ? "EXPIRE" : action.Trim().ToUpperInvariant();
        if (normalized is not ("EXPIRE" or "REQUIRE_USE" or "CASHOUT"))
            throw new InvalidOperationException("The current leave-type year-end excess action is invalid; correct its policy before posting the close.");
        return normalized;
    }

    public Task<LeaveRequest?> GetLeaveRequestSnapshotAsync(long id, CancellationToken cancellationToken = default)
        => _context.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<bool> HasConflictingLeaveAsync(string employeeCode, DateTime startDate, DateTime endDate,
        long? excludeRequestId = null, CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        var existingLeave = await _context.LeaveRequests.AsNoTracking().AnyAsync(r =>
            r.EmployeeCode == employeeCode &&
            (!excludeRequestId.HasValue || r.Id != excludeRequestId.Value) &&
            (r.Status == "PENDING" || r.Status == "APPROVED") &&
            r.StartDate <= end && r.EndDate >= start,
            cancellationToken);
        if (existingLeave)
            return true;

        return await _context.ShortLeaveRequests.AsNoTracking().AnyAsync(r =>
            r.EmployeeCode == employeeCode &&
            r.Status == ShortLeaveStatus.Approved &&
            r.PermissionDate >= start && r.PermissionDate <= end,
            cancellationToken);
    }
    private readonly OracleDbContext _context;

    public LeaveRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LeaveType>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveTypes.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(t => t.IsActive);
        }
        return await query.OrderBy(t => t.LeaveTypeCode).ToListAsync(cancellationToken);
    }

    public async Task<LeaveType?> GetLeaveTypeByCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveTypes.FirstOrDefaultAsync(t => t.LeaveTypeCode == leaveTypeCode, cancellationToken);
    }

    public async Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default)
    {
        await _context.LeaveTypes.AddAsync(leaveType, cancellationToken);
    }

    public Task UpdateLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default)
    {
        _context.LeaveTypes.Update(leaveType);
        return Task.CompletedTask;
    }

    public Task DeleteLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default)
    {
        leaveType.IsActive = false;
        _context.LeaveTypes.Update(leaveType);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<LeaveRequest> Items, int TotalCount)> GetLeaveRequestsPagedAsync(
        string? employeeCode = null,
        string? leaveTypeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(r => r.EmployeeCode == employeeCode);
        }

        if (!string.IsNullOrWhiteSpace(leaveTypeCode))
        {
            query = query.Where(r => r.LeaveTypeCode == leaveTypeCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.EndDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.StartDate <= toDate.Value.Date);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreationDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<LeaveRequest?> GetLeaveRequestByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var request = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request != null) await _context.Entry(request).ReloadAsync(cancellationToken);
        return request;
    }

    public async Task AddLeaveRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default)
    {
        await _context.LeaveRequests.AddAsync(request, cancellationToken);
    }

    public Task UpdateLeaveRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default)
    {
        _context.LeaveRequests.Update(request);
        return Task.CompletedTask;
    }

    public async Task<LeaveBalance?> GetLeaveBalanceAsync(string employeeCode, string leaveTypeCode, int year, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveBalances
            .Include(b => b.LeaveType)
            .FirstOrDefaultAsync(b => b.EmployeeCode == employeeCode && b.LeaveTypeCode == leaveTypeCode && b.YearNo == year, cancellationToken);
    }

    public async Task<LeaveBalance?> GetLeaveBalanceByCycleAsync(string employeeCode, string leaveTypeCode, DateTime cycleStartDate, CancellationToken cancellationToken = default)
    {
        var start = cycleStartDate.Date;
        var balance = await _context.LeaveBalances.Include(b => b.LeaveType).FirstOrDefaultAsync(b =>
            b.EmployeeCode == employeeCode && b.LeaveTypeCode == leaveTypeCode && b.CycleStartDate == start, cancellationToken);
        if (balance != null || start.Month != 1 || start.Day != 1) return balance;

        // Historical rows predate exact cycle boundaries and are interpreted as calendar-year rows.
        return await _context.LeaveBalances.Include(b => b.LeaveType).FirstOrDefaultAsync(b =>
            b.EmployeeCode == employeeCode && b.LeaveTypeCode == leaveTypeCode && b.YearNo == start.Year && b.CycleStartDate == null, cancellationToken);
    }

    public async Task<decimal> GetPendingLeaveDaysAsync(string employeeCode, string leaveTypeCode, DateTime cycleStartDate,
        DateTime cycleEndDate, long? exceptRequestId = null, CancellationToken cancellationToken = default)
    {
        var start = cycleStartDate.Date;
        var end = cycleEndDate.Date;
        var pending = await _context.LeaveRequests.AsNoTracking()
            .Where(r => r.EmployeeCode == employeeCode && r.LeaveTypeCode == leaveTypeCode && r.Status == "PENDING" &&
                (!exceptRequestId.HasValue || r.Id != exceptRequestId.Value) && r.StartDate <= end && r.EndDate >= start)
            .Select(r => (decimal?)r.DaysRequested).SumAsync(cancellationToken);
        return pending ?? 0m;
    }

    public async Task<bool> IsLeaveCycleClosedAsync(string employeeCode, string leaveTypeCode, DateTime cycleStartDate,
        DateTime cycleEndDate, CancellationToken cancellationToken = default)
    {
        var start = cycleStartDate.Date;
        var end = cycleEndDate.Date;
        return await _context.LeaveYearCloses.AsNoTracking().AnyAsync(c => c.Status == "POSTED" &&
            c.FromYear == start.Year && (c.LeaveTypeCode == "*" || c.LeaveTypeCode == leaveTypeCode), cancellationToken) ||
            await _context.LeaveYearCloseLines.AsNoTracking().AnyAsync(l =>
                l.EmployeeCode == employeeCode && l.LeaveTypeCode == leaveTypeCode && l.Close!.Status == "POSTED" &&
                ((l.SourceCycleStartDate.HasValue && l.SourceCycleEndDate.HasValue && l.SourceCycleStartDate <= end && l.SourceCycleEndDate >= start) ||
                 ((!l.SourceCycleStartDate.HasValue || !l.SourceCycleEndDate.HasValue) && l.SourceYear >= start.Year && l.SourceYear <= end.Year)), cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveBalance>> GetEmployeeBalancesAsync(string employeeCode, int year, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveBalances
            .Include(b => b.LeaveType)
            .Where(b => b.EmployeeCode == employeeCode && b.YearNo == year)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveBalance>> GetLeaveBalancesByYearAsync(int year, string? leaveTypeCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveBalances
            .Include(b => b.LeaveType)
            .Include(b => b.Employee)
            .Where(b => b.YearNo == year);

        if (!string.IsNullOrWhiteSpace(leaveTypeCode))
        {
            query = query.Where(b => b.LeaveTypeCode == leaveTypeCode);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddLeaveBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default)
    {
        await _context.LeaveBalances.AddAsync(balance, cancellationToken);
    }

    public Task UpdateLeaveBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default)
    {
        _context.LeaveBalances.Update(balance);
        return Task.CompletedTask;
    }

    public async Task<decimal> ApplyAccrualTargetAsync(LeaveBalance target, string user, CancellationToken cancellationToken = default)
    {
        var settings = await GetHrLeaveSettingsAsync(cancellationToken);
        return await ApplyAccrualTargetAsync(target, settings.LeaveYearStartMonth, settings.LeaveYearStartDay, user, cancellationToken);
    }

    public Task<decimal> ApplyAccrualTargetAsync(LeaveBalance target, int expectedLeaveYearStartMonth, int expectedLeaveYearStartDay, string user, CancellationToken cancellationToken = default)
        => ExecuteForLeaveEmployeeAsync(target.EmployeeCode,
            () => ApplyAccrualTargetCoreAsync(target, expectedLeaveYearStartMonth, expectedLeaveYearStartDay, user, cancellationToken), cancellationToken);

    private async Task<decimal> ApplyAccrualTargetCoreAsync(LeaveBalance target, int expectedLeaveYearStartMonth, int expectedLeaveYearStartDay, string user, CancellationToken cancellationToken)
    {
        var currentSettings = await GetHrLeaveSettingsAsync(cancellationToken);
        if (currentSettings.LeaveYearStartMonth != expectedLeaveYearStartMonth || currentSettings.LeaveYearStartDay != expectedLeaveYearStartDay)
            throw new InvalidOperationException("Leave-year settings changed while accrual was being calculated. Rerun accrual against the current settings.");

        var cycleStart = target.CycleStartDate?.Date ?? new DateTime(target.YearNo, 1, 1);
        await EnsureLeaveCycleOpenAsync(target.EmployeeCode, target.LeaveTypeCode, cycleStart,
            target.CycleEndDate ?? new DateTime(target.YearNo, 12, 31), cancellationToken);
        var balance = await _context.LeaveBalances.FirstOrDefaultAsync(b =>
            b.EmployeeCode == target.EmployeeCode &&
            b.LeaveTypeCode == target.LeaveTypeCode &&
            (b.CycleStartDate == cycleStart || (cycleStart.Month == 1 && cycleStart.Day == 1 && b.CycleStartDate == null && b.YearNo == target.YearNo)), cancellationToken);
        if (balance != null) await _context.Entry(balance).ReloadAsync(cancellationToken);
        decimal added;
        var beforeAccrual = LeaveBalanceState.From(balance);
        if (balance == null)
        {
            target.CreationUser = user;
            target.CreationDate = DateTime.UtcNow;
            await _context.LeaveBalances.AddAsync(target, cancellationToken);
            added = target.AccruedDays;
        }
        else
        {
            added = Math.Max(0m, target.AccruedDays - balance.AccruedDays);
            if (added > 0m)
            {
                balance.AccruedDays = target.AccruedDays;
                balance.CycleStartDate = target.CycleStartDate;
                balance.CycleEndDate = target.CycleEndDate;
                balance.UpdateUser = user;
                balance.UpdateDate = DateTime.UtcNow;
            }
        }

        if (added > 0)
            await RecordMovementAsync(balance ?? target, beforeAccrual, "ACCRUAL",
                Key(System.Text.Json.JsonSerializer.Serialize(new { Kind = "ACCRUAL", target.EmployeeCode, target.LeaveTypeCode, Cycle = cycleStart.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    Target = target.AccruedDays.ToString("G29", System.Globalization.CultureInfo.InvariantCulture) })),
                "ACCRUAL_TARGET", "Accrual to the configured cycle target", user, cancellationToken: cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return added;
    }

    public async Task<HrLeaveSettings> GetHrLeaveSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.HrLeaveSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, cancellationToken)
            ?? new HrLeaveSettings { Id = 1 };
    }

    public Task<bool> HasLeaveBalancesAsync(CancellationToken cancellationToken = default)
        => _context.LeaveBalances.AnyAsync(cancellationToken);

    public async Task SaveHrLeaveSettingsAsync(HrLeaveSettings settings, int expectedLeaveYearStartMonth, int expectedLeaveYearStartDay, CancellationToken cancellationToken = default)
    {
        settings.Id = 1;
        await using var owned = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            if (_context.Database.IsOracle())
            {
                foreach (var entry in _context.ChangeTracker.Entries<HrLeaveSettings>().Where(e => e.Entity.Id == 1).ToArray())
                    entry.State = EntityState.Detached;
                await HrLeaveSettingsMutationLock.LockAsync(_context, settings.UpdateUser ?? "SYSTEM", cancellationToken);
            }

            var current = await _context.HrLeaveSettings.FirstOrDefaultAsync(s => s.Id == 1, cancellationToken);
            var currentMonth = current?.LeaveYearStartMonth ?? 1;
            var currentDay = current?.LeaveYearStartDay ?? 1;
            if (currentMonth != expectedLeaveYearStartMonth || currentDay != expectedLeaveYearStartDay)
                throw new InvalidOperationException("Leave-year settings changed while this update was being prepared. Reload settings and retry.");
            if ((settings.LeaveYearStartMonth != currentMonth || settings.LeaveYearStartDay != currentDay) &&
                _context.Database.IsOracle())
            {
                await using var lockEmployees = _context.Database.GetDbConnection().CreateCommand();
                lockEmployees.Transaction = _context.Database.CurrentTransaction!.GetDbTransaction();
                lockEmployees.CommandText = "LOCK TABLE HR_EMPLOYEE IN EXCLUSIVE MODE";
                await lockEmployees.ExecuteNonQueryAsync(cancellationToken);
                await using var lockBalances = _context.Database.GetDbConnection().CreateCommand();
                lockBalances.Transaction = _context.Database.CurrentTransaction!.GetDbTransaction();
                lockBalances.CommandText = "LOCK TABLE HR_LEAVE_BALANCE IN EXCLUSIVE MODE";
                await lockBalances.ExecuteNonQueryAsync(cancellationToken);
            }
            if ((settings.LeaveYearStartMonth != currentMonth || settings.LeaveYearStartDay != currentDay) &&
                await _context.LeaveBalances.AnyAsync(cancellationToken))
                throw new InvalidOperationException("The leave-year start cannot be changed while balances exist; migrate the balances first.");

            if (current == null)
            {
                settings.CreationUser = settings.UpdateUser ?? "SYSTEM";
                settings.CreationDate = DateTime.UtcNow;
                await _context.HrLeaveSettings.AddAsync(settings, cancellationToken);
            }
            else
            {
                current.LeaveYearStartMonth = settings.LeaveYearStartMonth;
                current.LeaveYearStartDay = settings.LeaveYearStartDay;
                current.BalanceDisplayFormat = settings.BalanceDisplayFormat;
                current.StandardWorkdayHours = settings.StandardWorkdayHours;
                current.AutomaticAccrualEnabled = settings.AutomaticAccrualEnabled;
                current.AccrualTimeZoneId = settings.AccrualTimeZoneId;
                current.EligibleCitizenshipCountryCode = settings.EligibleCitizenshipCountryCode;
                current.LeaveCashoutWageBasisCode = settings.LeaveCashoutWageBasisCode;
                current.LeaveCashoutBasisComponentCode = settings.LeaveCashoutBasisComponentCode;
                current.LeaveCashoutDayDivisor = settings.LeaveCashoutDayDivisor;
                current.LeaveCashoutPayComponentCode = settings.LeaveCashoutPayComponentCode;
                current.UpdateUser = settings.UpdateUser;
                current.UpdateDate = settings.UpdateDate;
            }
            await _context.SaveChangesAsync(cancellationToken);
            if (owned != null) await owned.CommitAsync(cancellationToken);
        }
        catch
        {
            if (owned != null)
            {
                await owned.RollbackAsync(CancellationToken.None);
                foreach (var entry in _context.ChangeTracker.Entries<HrLeaveSettings>().Where(e => e.Entity.Id == 1).ToArray())
                    entry.State = EntityState.Detached;
            }
            throw;
        }
    }

    public async Task<LeaveYearClose?> GetLeaveYearCloseAsync(long id, CancellationToken cancellationToken = default)
        => await _context.LeaveYearCloses.AsNoTracking().Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<PayrollPeriod?> GetPayrollPeriodForLeaveCashoutAsync(string payPeriod, CancellationToken cancellationToken = default)
        => await _context.PayrollPeriods.AsNoTracking().SingleOrDefaultAsync(p => p.PeriodCode == payPeriod, cancellationToken);

    public async Task<LeaveCashoutSettlement?> GetLeaveCashoutSettlementAsync(long id, CancellationToken cancellationToken = default)
        => await _context.LeaveCashoutSettlements.AsNoTracking().Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<LeaveCashoutSettlement?> GetLeaveCashoutSettlementByKeyAsync(string key, CancellationToken cancellationToken = default)
        => await _context.LeaveCashoutSettlements.AsNoTracking().Include(s => s.Lines).FirstOrDefaultAsync(s => s.SettlementKey == key, cancellationToken);

    public async Task<LeaveCashoutSettlement> SaveLeaveCashoutSettlementDraftAsync(LeaveCashoutSettlement settlement, CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            throw new InvalidOperationException("Leave cashout drafting requires a relational provider with row locks.");
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            var active = _context.Database.CurrentTransaction!;
            await using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = active.GetDbTransaction();
                command.CommandText = "SELECT STATUS FROM HR_LEAVE_YEAR_CLOSE WHERE ID=:closeId FOR UPDATE";
                var parameter = command.CreateParameter(); parameter.ParameterName = "closeId"; parameter.Value = settlement.LeaveYearCloseId;
                command.Parameters.Add(parameter);
                if (Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) != "DRAFT")
                    throw new InvalidOperationException("The linked leave-year close is no longer a draft; refresh the cashout draft.");
            }

            // A concurrent retry may have passed the service-level lookup before waiting on the close lock.
            // Recheck the idempotency key under that lock so it returns the committed draft instead of
            // failing the source-line duplicate guard.
            var existing = await _context.LeaveCashoutSettlements.Include(batch => batch.Lines)
                .SingleOrDefaultAsync(batch => batch.SettlementKey == settlement.SettlementKey, cancellationToken);
            if (existing != null)
            {
                if (!string.Equals(existing.PayloadHash, settlement.PayloadHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("The settlement key is already used for a different close, pay period, or currency.");
                if (ownsTransaction) await transaction!.CommitAsync(cancellationToken);
                return existing;
            }

            var sourceLineIds = settlement.Lines.Select(l => l.LeaveYearCloseLineId).ToArray();
            var sourceSnapshots = await _context.LeaveYearCloseLines.AsNoTracking()
                .Where(line => sourceLineIds.Contains(line.Id) && line.CloseId == settlement.LeaveYearCloseId)
                .Select(line => new { line.Id, line.EmployeeCode, line.LeaveTypeCode, line.CashoutDays }).ToListAsync(cancellationToken);
            if (sourceSnapshots.Count != settlement.Lines.Count || settlement.Lines.Any(line =>
                    !sourceSnapshots.Any(source => source.Id == line.LeaveYearCloseLineId && source.EmployeeCode == line.EmployeeCode &&
                        source.LeaveTypeCode == line.LeaveTypeCode && source.CashoutDays == line.CashoutDays)))
                throw new InvalidOperationException("Cashout source lines changed after preview; refresh the settlement draft.");
            var duplicate = await _context.LeaveCashoutSettlementLines.AnyAsync(line => sourceLineIds.Contains(line.LeaveYearCloseLineId) &&
                _context.LeaveCashoutSettlements.Any(batch => batch.Id == line.SettlementId && (batch.Status == "DRAFT" || batch.Status == "POSTED")), cancellationToken);
            if (duplicate) throw new InvalidOperationException("One or more leave-close lines already belong to an active cashout settlement.");

            await _context.LeaveCashoutSettlements.AddAsync(settlement, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            if (ownsTransaction) await transaction!.CommitAsync(cancellationToken);
            return settlement;
        }
        catch
        {
            if (ownsTransaction) await transaction!.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task<LeaveCashoutSettlement?> PostLeaveCashoutSettlementAsync(long id, string user, CancellationToken cancellationToken = default)
        => ExecuteCashoutSettlementTransitionAsync(id, ["DRAFT", "POSTED"], async (settlement, period, closeStatus) =>
        {
            if (settlement.Status == "POSTED") return settlement;
            if (settlement.Status != "DRAFT") throw new InvalidOperationException("Only a draft cashout settlement can be posted.");
            if (closeStatus != "DRAFT") throw new InvalidOperationException("Prepare and approve cashout before posting its leave-year close.");
            if (period.Status != "OPEN") throw new InvalidOperationException("Cashout settlement can only post into an open payroll period.");
            if (settlement.Lines.Count == 0 || settlement.Lines.Any(line => line.PayrollAdjustmentId.HasValue))
                throw new InvalidOperationException("The cashout draft is empty or already has payroll adjustment links.");
            if (settlement.Lines.Any(line => line.CashoutAmount <= 0m || line.CashoutAmount > 99999999999999.9999m))
                throw new InvalidOperationException("Every cashout line must have a positive amount within payroll adjustment precision.");
            if (!await _context.SalaryComponents.AsNoTracking().AnyAsync(component => component.ComponentCode == settlement.PayComponentCode && component.IsActive &&
                    (component.ComponentType == "ALLOWANCE" || component.ComponentType == "EARNING"), cancellationToken))
                throw new InvalidOperationException("The configured cashout payroll component is no longer an active earning component.");

            foreach (var line in settlement.Lines.OrderBy(line => line.Id))
            {
                var adjustment = new PayrollAdjustment
                {
                    CompanyId = period.CompanyId,
                    EmployeeCode = line.EmployeeCode,
                    PayPeriod = settlement.PayPeriod,
                    ComponentCode = settlement.PayComponentCode,
                    AdjustmentType = "EARNING",
                    Amount = line.CashoutAmount,
                    Description = $"Leave cashout {settlement.SettlementKey}; {line.LeaveTypeCode}; {line.CashoutDays:0.####} days",
                    Status = "PENDING",
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                };
                await _context.PayrollAdjustments.AddAsync(adjustment, cancellationToken);
                line.PayrollAdjustment = adjustment;
            }
            settlement.Status = "POSTED";
            settlement.PostedBy = user;
            settlement.PostedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return settlement;
        }, cancellationToken);

    public Task<LeaveCashoutSettlement?> ReverseLeaveCashoutSettlementAsync(long id, string reason, string user, CancellationToken cancellationToken = default)
        => ExecuteCashoutSettlementTransitionAsync(id, ["DRAFT", "POSTED"], async (settlement, period, closeStatus) =>
        {
            if (settlement.Status == "REVERSED") return settlement;
            if (settlement.Status == "DRAFT")
            {
                settlement.Status = "REVERSED";
                settlement.ReversedBy = user;
                settlement.ReversedDate = DateTime.UtcNow;
                settlement.ReversalReason = reason;
                await _context.SaveChangesAsync(cancellationToken);
                return settlement;
            }
            if (settlement.Status != "POSTED") throw new InvalidOperationException("Only a draft or posted cashout settlement can be reversed.");
            if (period.Status != "OPEN") throw new InvalidOperationException("Cashout settlement reversal requires an open payroll period.");
            foreach (var line in settlement.Lines)
            {
                var adjustment = line.PayrollAdjustment;
                if (adjustment == null) throw new InvalidOperationException("A posted cashout line is missing its payroll adjustment link.");
                if (adjustment.PayrollRunLineId.HasValue || adjustment.Status == "PROCESSED")
                    throw new InvalidOperationException("A cashout adjustment has already been consumed by payroll; reverse the dependent payroll first.");
                var payrollAlreadyCalculated = await _context.PayrollRuns.AsNoTracking().AnyAsync(run => run.PayPeriod == settlement.PayPeriod &&
                    run.Status != "CANCELLED" && run.Lines.Any(payrollLine => payrollLine.EmployeeCode == line.EmployeeCode), cancellationToken);
                if (payrollAlreadyCalculated)
                    throw new InvalidOperationException("Cancel the calculated payroll run before reversing its cashout settlement.");
                if (adjustment.Status is "PENDING" or "APPROVED")
                {
                    adjustment.Status = "REJECTED";
                    adjustment.UpdateUser = user;
                    adjustment.UpdateDate = DateTime.UtcNow;
                }
                else if (adjustment.Status != "REJECTED")
                    throw new InvalidOperationException($"Cashout payroll adjustment state '{adjustment.Status}' cannot be reversed.");
            }
            settlement.Status = "REVERSED";
            settlement.ReversedBy = user;
            settlement.ReversedDate = DateTime.UtcNow;
            settlement.ReversalReason = reason;
            await _context.SaveChangesAsync(cancellationToken);
            return settlement;
        }, cancellationToken);

    private async Task<LeaveCashoutSettlement?> ExecuteCashoutSettlementTransitionAsync(long id, string[] allowedCloseStatuses,
        Func<LeaveCashoutSettlement, PayrollPeriod, string, Task<LeaveCashoutSettlement>> transition, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Cashout settlement posting and reversal require an owned relational transaction.");
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var connection = _context.Database.GetDbConnection();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT ID FROM HR_LEAVE_CASHOUT WHERE ID=:recordId FOR UPDATE";
                var parameter = command.CreateParameter(); parameter.ParameterName = "recordId"; parameter.Value = id;
                command.Parameters.Add(parameter);
                if (await command.ExecuteScalarAsync(cancellationToken) == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }
            }

            var settlement = await _context.LeaveCashoutSettlements.Include(s => s.Lines)
                .ThenInclude(line => line.PayrollAdjustment).SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (settlement == null) { await transaction.RollbackAsync(cancellationToken); return null; }
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT ID FROM HR_PAYROLL_PERIOD WHERE PERIOD_CODE=:periodCode ORDER BY ID FOR UPDATE";
                var parameter = command.CreateParameter(); parameter.ParameterName = "periodCode"; parameter.Value = settlement.PayPeriod;
                command.Parameters.Add(parameter);
                var found = false;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) found = true;
                if (!found) throw new InvalidOperationException("The payroll period must exist before a cashout transition.");
            }
            var period = await _context.PayrollPeriods.SingleOrDefaultAsync(p => p.PeriodCode == settlement.PayPeriod, cancellationToken)
                ?? throw new InvalidOperationException("The payroll period is missing from the current company scope.");
            string? closeStatus;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT STATUS FROM HR_LEAVE_YEAR_CLOSE WHERE ID=:closeId FOR UPDATE";
                var parameter = command.CreateParameter(); parameter.ParameterName = "closeId"; parameter.Value = settlement.LeaveYearCloseId;
                command.Parameters.Add(parameter);
                closeStatus = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? string.Empty;
                if (!allowedCloseStatuses.Contains(closeStatus, StringComparer.Ordinal))
                    throw new InvalidOperationException("The source leave-year close changed state; refresh the cashout settlement.");
            }
            var result = await transition(settlement, period, closeStatus);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<LeaveYearClose?> GetLeaveYearCloseByKeyAsync(string closeKey, CancellationToken cancellationToken = default)
        => await _context.LeaveYearCloses.Include(c => c.Lines).FirstOrDefaultAsync(c => c.CloseKey == closeKey, cancellationToken);

    public async Task<LeaveYearClose> SaveLeaveYearCloseDraftAsync(LeaveYearClose close, CancellationToken cancellationToken = default)
    {
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        try
        {
            var activeTransaction = _context.Database.CurrentTransaction!;
            var connection = _context.Database.GetDbConnection();
            if (close.Id > 0)
            {
                await using var lockClose = connection.CreateCommand();
                lockClose.Transaction = activeTransaction.GetDbTransaction();
                lockClose.CommandText = "SELECT STATUS FROM HR_LEAVE_YEAR_CLOSE WHERE ID = :recordId FOR UPDATE";
                var recordId = lockClose.CreateParameter();
                recordId.ParameterName = "recordId";
                recordId.Value = close.Id;
                lockClose.Parameters.Add(recordId);
                if (await lockClose.ExecuteScalarAsync(cancellationToken) is null)
                    throw new KeyNotFoundException($"Leave-year close '{close.Id}' was not found.");
            }

            // A leave-type row is the stable serialization key, including before a close header exists.
            // Header refresh takes the close row first, matching Post/Unpost lock order.
            await LockLeaveTypesAsync(close.LeaveTypeCode, cancellationToken);

            var existing = close.Id > 0
                ? await _context.LeaveYearCloses.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == close.Id, cancellationToken)
                : await _context.LeaveYearCloses.Include(c => c.Lines).FirstOrDefaultAsync(c => c.CloseKey == close.CloseKey, cancellationToken);

            if (close.Id <= 0 && existing != null)
            {
                // Two first-time drafts can both read a missing key before either saves.
                // The type lock makes the second writer observe and return the first result.
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
                return existing;
            }

            if (existing == null)
            {
                await _context.LeaveYearCloses.AddAsync(close, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
                return close;
            }

            await _context.Entry(existing).ReloadAsync(cancellationToken);
            await _context.Entry(existing).Collection(c => c.Lines).LoadAsync(cancellationToken);
            if (existing.Status is not ("DRAFT" or "UNPOSTED"))
                throw new InvalidOperationException("Only a draft or unposted close can be refreshed.");
            if (await _context.LeaveCashoutSettlements.AnyAsync(settlement => settlement.LeaveYearCloseId == existing.Id && settlement.Status != "REVERSED", cancellationToken))
                throw new InvalidOperationException("Reverse or cancel linked cashout settlements before refreshing the leave-year close.");

            _context.LeaveYearCloseLines.RemoveRange(existing.Lines);
            existing.Lines.Clear();
            foreach (var line in close.Lines)
            {
                line.CloseId = existing.Id;
                existing.Lines.Add(line);
            }
            existing.Status = "DRAFT";
            existing.PostedBy = null;
            existing.PostedDate = null;
            existing.UnpostedBy = null;
            existing.UnpostedDate = null;
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return existing;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }
            throw;
        }
    }

    public Task<LeaveYearClose?> PostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default)
        => ApplyLeaveYearCloseAsync(id, user, reverse: false, cancellationToken);

    public Task<LeaveYearClose?> UnpostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default)
        => ApplyLeaveYearCloseAsync(id, user, reverse: true, cancellationToken);

    private async Task<LeaveYearClose?> ApplyLeaveYearCloseAsync(long id, string user, bool reverse, CancellationToken cancellationToken)
    {
        await using var transaction = _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
        var activeTransaction = _context.Database.CurrentTransaction!;
        var connection = _context.Database.GetDbConnection();
        async Task LockRow(string table, long rowId)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = activeTransaction.GetDbTransaction();
            command.CommandText = $"SELECT 1 FROM {table} WHERE ID = :recordId FOR UPDATE";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "recordId";
            parameter.Value = rowId;
            command.Parameters.Add(parameter);
            if (await command.ExecuteScalarAsync(cancellationToken) is null)
                throw new KeyNotFoundException($"{table} row '{rowId}' was not found.");
        }

        await LockRow("HR_LEAVE_YEAR_CLOSE", id);
        // Preview/read operations may have tracked an older draft in this scope.
        foreach (var entry in _context.ChangeTracker.Entries<LeaveYearCloseLine>().Where(e => e.Entity.CloseId == id).ToList())
            entry.State = EntityState.Detached;
        foreach (var entry in _context.ChangeTracker.Entries<LeaveYearClose>().Where(e => e.Entity.Id == id).ToList())
            entry.State = EntityState.Detached;
        var close = await _context.LeaveYearCloses.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (close == null) return null;
        if ((!reverse && close.Status == "POSTED") || (reverse && close.Status == "UNPOSTED"))
        {
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return close;
        }
        if (reverse ? close.Status != "POSTED" : close.Status is not ("DRAFT" or "UNPOSTED"))
            throw new InvalidOperationException(reverse ? "Only a posted leave-year close can be unposted." : "Only a draft or unposted leave-year close can be posted.");
        var cashoutSourceLines = close.Lines.Where(line => line.CashoutDays > 0m).ToDictionary(line => line.Id);
        if (reverse)
        {
            if (await _context.LeaveCashoutSettlements.AsNoTracking().AnyAsync(settlement => settlement.LeaveYearCloseId == id && settlement.Status != "REVERSED", cancellationToken))
                throw new InvalidOperationException("Reverse or cancel all linked cashout settlements before unposting this leave-year close.");
        }
        else if (cashoutSourceLines.Count > 0)
        {
            var pendingDraft = await _context.LeaveCashoutSettlements.AsNoTracking().AnyAsync(settlement => settlement.LeaveYearCloseId == id && settlement.Status == "DRAFT", cancellationToken);
            if (pendingDraft) throw new InvalidOperationException("Post or reverse each cashout settlement draft before posting the leave-year close.");
            var cashoutLines = await _context.LeaveCashoutSettlementLines
                .Include(line => line.Settlement).Include(line => line.PayrollAdjustment)
                .Where(line => line.Settlement!.LeaveYearCloseId == id && line.Settlement.Status == "POSTED")
                .ToListAsync(cancellationToken);
            var linkedRunLineIds = cashoutLines.Where(line => line.PayrollAdjustment?.PayrollRunLineId.HasValue == true)
                .Select(line => line.PayrollAdjustment!.PayrollRunLineId!.Value).Distinct().ToArray();
            var approvedRunLines = await _context.PayrollRunLines.AsNoTracking()
                .Where(runLine => linkedRunLineIds.Contains(runLine.Id) && runLine.PayrollRun!.Status == "APPROVED")
                .Select(runLine => new { runLine.Id, runLine.EmployeeCode, runLine.PayrollRun!.PayPeriod })
                .ToListAsync(cancellationToken);
            if (cashoutLines.Count != cashoutSourceLines.Count || cashoutLines.Any(line =>
                    !cashoutSourceLines.TryGetValue(line.LeaveYearCloseLineId, out var source) || source.CashoutDays != line.CashoutDays ||
                    line.PayrollAdjustment == null || line.PayrollAdjustment.Status != "APPROVED" ||
                    line.PayrollAdjustment.ComponentCode != line.Settlement!.PayComponentCode || line.PayrollAdjustment.Amount != line.CashoutAmount ||
                    line.PayrollAdjustment.PayPeriod != line.Settlement.PayPeriod || !line.PayrollAdjustment.PayrollRunLineId.HasValue ||
                    !approvedRunLines.Any(runLine => runLine.Id == line.PayrollAdjustment.PayrollRunLineId && runLine.EmployeeCode == line.EmployeeCode && runLine.PayPeriod == line.Settlement.PayPeriod)))
                throw new InvalidOperationException("Every cashout day must have a posted settlement and an approved payroll adjustment linked to an approved payroll run before posting the leave-year close.");
            foreach (var settlement in cashoutLines.Select(line => line.Settlement!).DistinctBy(settlement => settlement.Id))
            {
                var period = await _context.PayrollPeriods.SingleOrDefaultAsync(p => p.PeriodCode == settlement.PayPeriod, cancellationToken);
                if (period == null || period.Status != "OPEN")
                    throw new InvalidOperationException($"Cashout settlement '{settlement.SettlementKey}' is not linked to an open payroll period.");
            }
        }

        foreach (var employeeCode in close.Lines.Select(l => l.EmployeeCode).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = activeTransaction.GetDbTransaction();
            command.CommandText = "SELECT 1 FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE = :employeeCode FOR UPDATE";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "employeeCode";
            parameter.Value = employeeCode;
            command.Parameters.Add(parameter);
            if (await command.ExecuteScalarAsync(cancellationToken) is null)
                throw new InvalidOperationException($"Employee '{employeeCode}' no longer exists.");
        }

        await LockLeaveTypesAsync(close.LeaveTypeCode, cancellationToken);
        if (!reverse && await _context.LeaveYearCloses.AsNoTracking().AnyAsync(c => c.Id != id && c.Status == "POSTED" &&
            c.FromYear == close.FromYear && (c.LeaveTypeCode == "*" || close.LeaveTypeCode == "*" || c.LeaveTypeCode == close.LeaveTypeCode), cancellationToken))
            throw new InvalidOperationException("An overlapping leave-year close is already posted. Unpost it before posting another close for this year/type.");
        if (!reverse && close.Lines.Count > 0)
        {
            var closeTypeCodes = close.Lines.Select(line => line.LeaveTypeCode).Distinct(StringComparer.Ordinal).ToArray();
            var currentLeaveTypes = await _context.LeaveTypes.AsNoTracking()
                .Where(type => closeTypeCodes.Contains(type.LeaveTypeCode))
                .ToDictionaryAsync(type => type.LeaveTypeCode, StringComparer.Ordinal, cancellationToken);
            foreach (var line in close.Lines)
            {
                if (!currentLeaveTypes.TryGetValue(line.LeaveTypeCode, out var currentType) || !currentType.TracksBalance)
                    throw new InvalidOperationException($"Leave type '{line.LeaveTypeCode}' no longer tracks a balance; rebuild the leave-year close draft.");

                var currentAction = NormalizeCloseExcessAction(currentType.YearEndExcessAction);
                var policyChanged = line.TracksBalanceSnapshot.HasValue && line.TracksBalanceSnapshot.Value != currentType.TracksBalance
                    || line.CarryForwardAllowedSnapshot.HasValue && line.CarryForwardAllowedSnapshot.Value != currentType.CarryForwardAllowed
                    || line.CarryForwardCapDaysSnapshot.HasValue && line.CarryForwardCapDaysSnapshot.Value != currentType.CarryForwardCapDays
                    || line.YearEndExcessActionSnapshot != null && line.YearEndExcessActionSnapshot != currentAction;
                if (policyChanged)
                    throw new InvalidOperationException($"Leave-year policy for '{line.LeaveTypeCode}' changed after the close draft. Rebuild the close draft before posting.");

                var unused = line.SourceAccruedBefore + line.SourceCarriedBefore - line.SourceUsedDays;
                var carried = currentType.CarryForwardAllowed
                    ? Math.Min(unused, currentType.CarryForwardCapDays > 0m ? currentType.CarryForwardCapDays : unused)
                    : 0m;
                var excess = unused - carried;
                if (currentAction == "REQUIRE_USE" && excess > 0m)
                    throw new InvalidOperationException($"{line.EmployeeCode}/{line.LeaveTypeCode} has {excess:F4} days that must be used before this leave-year can close.");
                var expired = currentAction == "EXPIRE" ? excess : 0m;
                var cashout = currentAction == "CASHOUT" ? excess : 0m;
                if (line.UnusedDays != unused || line.CarriedDays != carried || line.ExpiredDays != expired || line.CashoutDays != cashout)
                    throw new InvalidOperationException($"Leave-year policy results for '{line.EmployeeCode}/{line.LeaveTypeCode}' changed after the close draft. Rebuild the close draft before posting.");
            }
        }
        var newTargets = new List<(LeaveYearCloseLine Line, LeaveBalance Target)>();
        var movementOperation = Guid.NewGuid().ToString("N");
        foreach (var line in close.Lines)
        {
            var source = await _context.LeaveBalances.FirstOrDefaultAsync(b => b.Id == line.SourceBalanceId, cancellationToken)
                ?? throw new InvalidOperationException($"Source balance {line.SourceBalanceId} no longer exists.");
            await _context.Entry(source).ReloadAsync(cancellationToken);
            var sourceBefore = LeaveBalanceState.From(source);
            // Employee locks serialize this check with grants and requests. Exclude
            // this close itself so its authorized unpost can restore the source.
            await EnsureLeaveCycleOpenCoreAsync(line.EmployeeCode, line.LeaveTypeCode,
                line.SourceCycleStartDate ?? new DateTime(line.SourceYear, 1, 1),
                line.SourceCycleEndDate ?? new DateTime(line.SourceYear, 12, 31), id, cancellationToken);
            await EnsureLeaveCycleOpenCoreAsync(line.EmployeeCode, line.LeaveTypeCode,
                line.TargetCycleStartDate, line.TargetCycleEndDate, id, cancellationToken);
            if (!reverse)
            {
                var sourceCycleStart = line.SourceCycleStartDate ?? new DateTime(line.SourceYear, 1, 1);
                var sourceCycleEnd = line.SourceCycleEndDate ?? new DateTime(line.SourceYear, 12, 31);
                if (await _context.LeaveRequests.AnyAsync(r => r.EmployeeCode == line.EmployeeCode &&
                    r.LeaveTypeCode == line.LeaveTypeCode && r.Status == "PENDING" &&
                    r.StartDate <= sourceCycleEnd && r.EndDate >= sourceCycleStart, cancellationToken))
                    throw new InvalidOperationException($"Pending leave requests exist for {line.EmployeeCode}/{line.LeaveTypeCode}; resolve them before posting the close.");

                if (source.AccruedDays != line.SourceAccruedBefore || source.UsedDays != line.SourceUsedDays || source.CarriedForwardDays != line.SourceCarriedBefore)
                    throw new InvalidOperationException("A source balance changed after the close draft. Rebuild the draft before posting.");

                LeaveBalance? target;
                if (line.TargetBalanceId is > 0)
                    target = await _context.LeaveBalances.FirstOrDefaultAsync(b => b.Id == line.TargetBalanceId, cancellationToken);
                else
                    target = await GetLeaveBalanceByCycleAsync(line.EmployeeCode, line.LeaveTypeCode, line.TargetCycleStartDate, cancellationToken);
                if (target != null) await _context.Entry(target).ReloadAsync(cancellationToken);
                var targetBefore = LeaveBalanceState.From(target);
                if (line.TargetBalanceId is > 0)
                {
                    if (target == null || target.AccruedDays != line.TargetAccruedBefore || target.UsedDays != line.TargetUsedBefore || target.CarriedForwardDays != line.TargetCarriedBefore)
                        throw new InvalidOperationException("A target balance changed after the close draft. Rebuild the draft before posting.");
                }
                else if (target != null)
                {
                    throw new InvalidOperationException("A target balance was created after the close draft. Rebuild the draft before posting.");
                }

                if (target == null && line.CarriedDays > 0m)
                {
                    target = new LeaveBalance
                    {
                        EmployeeCode = line.EmployeeCode,
                        LeaveTypeCode = line.LeaveTypeCode,
                        YearNo = line.TargetYear,
                        CycleStartDate = line.TargetCycleStartDate,
                        CycleEndDate = line.TargetCycleEndDate,
                        AccruedDays = 0m,
                        UsedDays = 0m,
                        CarriedForwardDays = line.CarriedDays,
                        CreationUser = user,
                        CreationDate = DateTime.UtcNow
                    };
                    await _context.LeaveBalances.AddAsync(target, cancellationToken);
                    newTargets.Add((line, target));
                }
                else if (target != null)
                {
                    target.CycleStartDate ??= line.TargetCycleStartDate;
                    target.CycleEndDate ??= line.TargetCycleEndDate;
                    target.CarriedForwardDays = line.TargetCarriedBefore + line.CarriedDays;
                    target.UpdateUser = user;
                    target.UpdateDate = DateTime.UtcNow;
                }

                source.AccruedDays = source.UsedDays;
                source.CarriedForwardDays = 0m;
                source.UpdateUser = user;
                source.UpdateDate = DateTime.UtcNow;
                if (target != null)
                    await RecordCloseChangeAsync(target, targetBefore, close, false, movementOperation, "CARRY_FORWARD", user, 0, line.CarriedDays, cancellationToken);
            }
            else
            {
                if (source.AccruedDays != line.SourceUsedDays || source.UsedDays != line.SourceUsedDays || source.CarriedForwardDays != 0m)
                    throw new InvalidOperationException("The source balance has changed since posting; reverse dependent transactions first.");
                var target = line.TargetBalanceId is > 0
                    ? await _context.LeaveBalances.FirstOrDefaultAsync(b => b.Id == line.TargetBalanceId, cancellationToken)
                    : await GetLeaveBalanceByCycleAsync(line.EmployeeCode, line.LeaveTypeCode, line.TargetCycleStartDate, cancellationToken);
                if (target != null) await _context.Entry(target).ReloadAsync(cancellationToken);
                var targetBefore = LeaveBalanceState.From(target);
                if (line.TargetBalanceId is > 0)
                {
                    if (target == null || target.AccruedDays != line.TargetAccruedBefore || target.UsedDays != line.TargetUsedBefore || target.CarriedForwardDays != line.TargetCarriedBefore + line.CarriedDays)
                        throw new InvalidOperationException("The new-cycle balance has changed since posting. Reverse its dependent transactions before unposting.");
                }
                else if (target != null)
                {
                    throw new InvalidOperationException("A new-cycle balance was created after posting. Reverse its dependent transactions before unposting.");
                }

                source.AccruedDays = line.SourceAccruedBefore;
                source.CarriedForwardDays = line.SourceCarriedBefore;
                source.UpdateUser = user;
                source.UpdateDate = DateTime.UtcNow;
                if (target != null)
                {
                    target.CycleStartDate ??= line.TargetCycleStartDate;
                    target.CycleEndDate ??= line.TargetCycleEndDate;
                    target.CarriedForwardDays = line.TargetCarriedBefore;
                    target.UpdateUser = user;
                    target.UpdateDate = DateTime.UtcNow;
                }
                if (target != null)
                    await RecordCloseChangeAsync(target, targetBefore, close, true, movementOperation, "CARRY_FORWARD", user, 0, line.CarriedDays, cancellationToken);
            }
            await RecordCloseChangeAsync(source, sourceBefore, close, reverse, movementOperation, "CLOSE_OUT", user, line.ExpiredDays, line.CarriedDays, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        foreach (var (line, target) in newTargets) line.TargetBalanceId = target.Id;
        close.Status = reverse ? "UNPOSTED" : "POSTED";
        if (reverse)
        {
            close.UnpostedBy = user;
            close.UnpostedDate = DateTime.UtcNow;
        }
        else
        {
            close.PostedBy = user;
            close.PostedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return close;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }
            throw;
        }
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetApprovedUnpaidLeavesInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Where(r => r.EmployeeCode == employeeCode &&
                        r.Status == "APPROVED" &&
                        r.LeaveType != null &&
                        !r.LeaveType.IsPaid &&
                        r.StartDate <= periodEnd &&
                        r.EndDate >= periodStart)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasPendingLeaveRequestsAsync(string employeeCode, string leaveTypeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
        => _context.LeaveRequests.AnyAsync(r => r.EmployeeCode == employeeCode && r.LeaveTypeCode == leaveTypeCode &&
            r.Status == "PENDING" && r.StartDate <= periodEnd && r.EndDate >= periodStart, cancellationToken);

    public async Task<IReadOnlyList<LeavePolicy>> GetActiveLeavePoliciesAsync(CancellationToken cancellationToken = default, DateTime? effectiveDate = null)
    {
        var date = (effectiveDate ?? DateTime.UtcNow).Date;
        return await _context.LeavePolicies
            .Include(p => p.LeaveType)
            .Where(p => p.IsActive && p.EffectiveFrom <= date && (!p.EffectiveTo.HasValue || p.EffectiveTo.Value >= date))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeavePolicy>> GetLeavePolicyVersionsAsync(string leaveTypeCode, CancellationToken cancellationToken = default)
        => await _context.LeavePolicies.Include(p => p.LeaveType).Where(p => p.LeaveTypeCode == leaveTypeCode)
            .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.Id).ToListAsync(cancellationToken);

    public async Task<LeavePolicy?> GetLeavePolicyByTypeCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default, DateTime? effectiveDate = null)
    {
        var date = (effectiveDate ?? DateTime.UtcNow).Date;
        var matches = await _context.LeavePolicies
            .Include(p => p.LeaveType)
            .Where(p => p.LeaveTypeCode == leaveTypeCode && p.EffectiveFrom <= date && (!p.EffectiveTo.HasValue || p.EffectiveTo.Value >= date))
            .OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.Id)
            .Take(2).ToListAsync(cancellationToken);
        if (matches.Count > 1)
            throw new InvalidOperationException($"Overlapping leave policy versions exist for '{leaveTypeCode}' on {date:yyyy-MM-dd}.");
        return matches.SingleOrDefault();
    }

    public async Task<T> ExecuteForLeavePolicyAsync<T>(string leaveTypeCode, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Leave policy version changes must start their own transaction.");
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT LEAVE_TYPE_CODE FROM HR_LEAVE_TYPE WHERE LEAVE_TYPE_CODE=:leaveTypeCode FOR UPDATE";
            var parameter = command.CreateParameter(); parameter.ParameterName = "leaveTypeCode"; parameter.Value = leaveTypeCode;
            command.Parameters.Add(parameter);
            if (await command.ExecuteScalarAsync(cancellationToken) == null)
                throw new KeyNotFoundException($"Leave type '{leaveTypeCode}' was not found.");
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task AddLeavePolicyAsync(LeavePolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.LeavePolicies.AddAsync(policy, cancellationToken);
    }

    public Task UpdateLeavePolicyAsync(LeavePolicy policy, CancellationToken cancellationToken = default)
    {
        _context.LeavePolicies.Update(policy);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
