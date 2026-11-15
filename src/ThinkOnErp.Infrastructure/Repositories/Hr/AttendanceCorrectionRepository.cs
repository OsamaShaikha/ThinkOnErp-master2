using ThinkOnErp.Infrastructure.Services.Hr;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class AttendanceCorrectionRepository : IAttendanceCorrectionRepository
{
    public Task<AttendanceCorrectionRequest?> GetCorrectionRequestSnapshotAsync(long id, CancellationToken cancellationToken = default)
        => _context.Set<AttendanceCorrectionRequest>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    private readonly OracleDbContext _context;

    public AttendanceCorrectionRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AttendanceCorrectionRequest>> GetCorrectionRequestsAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default)
    {
        var query = _context.AttendanceCorrectionRequests.AsQueryable();

        if (!string.IsNullOrEmpty(employeeCode))
            query = query.Where(r => r.EmployeeCode == employeeCode);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        return await query.OrderByDescending(r => r.AttendanceDate).ToListAsync(cancellationToken);
    }

    public async Task<AttendanceCorrectionRequest?> GetCorrectionRequestByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.AttendanceCorrectionRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (snapshot == null) return null;
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction != null)
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand(); command.Transaction = _context.Database.CurrentTransaction.GetDbTransaction();
            command.CommandText = "SELECT 1 FROM HR_ATTENDANCE_CORRECTION WHERE ID=:requestId AND EMPLOYEE_CODE=:employeeCode FOR UPDATE";
            var idParameter = command.CreateParameter(); idParameter.ParameterName = "requestId"; idParameter.Value = id; command.Parameters.Add(idParameter);
            var employeeParameter = command.CreateParameter(); employeeParameter.ParameterName = "employeeCode"; employeeParameter.Value = snapshot.EmployeeCode; command.Parameters.Add(employeeParameter);
            if (await command.ExecuteScalarAsync(cancellationToken) == null) return null;
        }
        var current = await _context.AttendanceCorrectionRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (current == null) return null;
        await _context.Entry(current).ReloadAsync(cancellationToken);
        if (current.EmployeeCode != snapshot.EmployeeCode || current.AttendanceDate.Date != snapshot.AttendanceDate.Date) throw new AttendanceCorrectionStaleException();
        return current;
    }

    public async Task AddCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        await _context.AttendanceCorrectionRequests.AddAsync(request, cancellationToken);
    }

    public Task UpdateCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        _context.AttendanceCorrectionRequests.Update(request);
        return Task.CompletedTask;
    }

    public async Task EnsureEmployeeWorkDateAsync(string employeeCode, DateTime workDate, CancellationToken cancellationToken = default)
    {
        var employee = await _context.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeCode == employeeCode, cancellationToken)
            ?? throw new KeyNotFoundException("Employee not found in the current HR scope.");
        if (employee.HireDate.Date > workDate.Date || employee.TerminationDate.HasValue && employee.TerminationDate.Value.Date < workDate.Date)
            throw new InvalidOperationException("Financial attendance date is outside the employee's hire/termination interval; review the employee and evidence first.");
    }

    public async Task<AttendanceDay?> GetAttendanceDayAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default)
    {
        var dateOnly = date.Date;
        var day = await _context.AttendanceDays.FirstOrDefaultAsync(d => d.EmployeeCode == employeeCode && d.AttendanceDate.Date == dateOnly, cancellationToken);
        // A pre-lock lookup may already have tracked this row. Refresh unchanged
        // state so waiting writers cannot approve/recalculate a cached old day.
        if (day != null && _context.Entry(day).State == EntityState.Unchanged) await _context.Entry(day).ReloadAsync(cancellationToken);
        return day;
    }

    public async Task<IReadOnlyList<AttendanceDay>> GetAttendanceDaysAsync(string employeeCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceDays
            .Where(d => d.EmployeeCode == employeeCode && d.AttendanceDate >= fromDate && d.AttendanceDate <= toDate)
            .OrderBy(d => d.AttendanceDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default)
    {
        await _context.AttendanceDays.AddAsync(day, cancellationToken);
    }

    public Task UpdateAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default)
    {
        _context.AttendanceDays.Update(day);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<RawAttendance>> GetUnprocessedRawPunchesAsync(int batchSize = 500, CancellationToken cancellationToken = default)
    {
        return await _context.RawAttendances.AsNoTracking()
            .Where(r => !r.IsProcessed)
            .OrderBy(r => r.PunchTime).ThenBy(r => r.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RawAttendance>> GetAutomaticallyProcessableRawPunchesAsync(int batchSize, DateTime retryBeforeUtc, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(batchSize), "Automatic attendance batch size must be 1..100.");
        if (retryBeforeUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Automatic attendance retry cutoff must be UTC.", nameof(retryBeforeUtc));
        return await BuildAutomaticProcessingQuery(_context, retryBeforeUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    internal static IQueryable<RawAttendance> BuildAutomaticProcessingQuery(OracleDbContext context, DateTime retryBeforeUtc)
    {
        return context.RawAttendances.AsNoTracking()
            .Where(r => !r.IsProcessed && r.ProcessingReason == null &&
                (!r.LastAttemptedAtUtc.HasValue || r.LastAttemptedAtUtc <= retryBeforeUtc))
            .OrderBy(r => r.LastAttemptedAtUtc.HasValue ? 1 : 0)
            .ThenBy(r => r.LastAttemptedAtUtc)
            .ThenBy(r => r.PunchTime)
            .ThenBy(r => r.Id);
    }

    public async Task<RawAttendance> AcceptMobileEvidenceAsync(RawAttendance proposed, Func<long, string> responseJson, CancellationToken cancellationToken = default)
    {
        if (proposed.EventId == null || proposed.EventPayloadHash == null || proposed.EventId.StartsWith("D:", StringComparison.Ordinal) || proposed.AttendanceDayId.HasValue)
            throw new ArgumentException("Mobile evidence requires its own stable key, fingerprint and no financial-day link at capture.");
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction != null && _context.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Mobile evidence capture requires ReadCommitted.");
        await using var owned = _context.Database.CurrentTransaction == null ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            await LockEvidenceEmployee(proposed.EmployeeCode, cancellationToken);
            var existing = await GetMobileEventAsync(proposed.EmployeeCode, proposed.EventId, cancellationToken);
            if (existing != null)
            {
                if (existing.EventPayloadHash != proposed.EventPayloadHash) throw new AttendanceEventConflictException();
                if (owned != null) await owned.CommitAsync(cancellationToken); return existing;
            }
            await _context.RawAttendances.AddAsync(proposed, cancellationToken); await SaveChangesAsync(cancellationToken);
            proposed.EventResponseJson = responseJson(proposed.Id); await SaveChangesAsync(cancellationToken);
            if (owned != null) await owned.CommitAsync(cancellationToken); return proposed;
        }
        catch { if (owned != null) { await owned.RollbackAsync(cancellationToken); _context.ChangeTracker.Clear(); } throw; }
    }

    public async Task RecordRawAttemptAsync(long id, string user, string? reason, CancellationToken cancellationToken = default)
    {
        await EditPendingRaw(id, async raw =>
        {
            if (raw.IsProcessed) return;
            raw.LastAttemptedAtUtc = DateTime.UtcNow; raw.LastAttemptedBy = user;
            raw.ProcessingReason = reason == null ? null : reason.Length <= 1000 ? reason : reason[..1000];
            await SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<RawAttendance> AcceptDeviceEventAsync(RawAttendance proposed, CancellationToken cancellationToken = default)
    {
        if (proposed.EventId == null || proposed.EventPayloadHash == null || !proposed.EventId.StartsWith("D:", StringComparison.Ordinal))
            throw new ArgumentException("Device events need a stable namespaced event key and payload fingerprint.");
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction != null && _context.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Device evidence intake requires ReadCommitted.");
        await using var owned = _context.Database.CurrentTransaction == null ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            await LockEvidenceEmployee(proposed.EmployeeCode, cancellationToken);
            var existing = await GetMobileEventAsync(proposed.EmployeeCode, proposed.EventId, cancellationToken);
            if (existing != null)
            {
                if (existing.EventPayloadHash != proposed.EventPayloadHash) throw new AttendanceEventConflictException();
                if (owned != null) await owned.CommitAsync(cancellationToken); return existing;
            }
            await _context.RawAttendances.AddAsync(proposed, cancellationToken); await SaveChangesAsync(cancellationToken);
            await _context.Entry(proposed).ReloadAsync(cancellationToken);
            if (owned != null) await owned.CommitAsync(cancellationToken); return proposed;
        }
        catch { if (owned != null) { await owned.RollbackAsync(cancellationToken); _context.ChangeTracker.Clear(); } throw; }
    }

    public async Task<T> ExecuteForRawWorkDateAsync<T>(string employeeCode, DateTime workDate, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational()) return await action();
        if (_context.Database.CurrentTransaction != null && _context.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Raw financial processing requires ReadCommitted.");
        await using var owned = _context.Database.CurrentTransaction == null ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            await HrEmployeePayrollLock.AcquireAsync(_context, employeeCode, new[] { new HrDateRange(workDate.Date, workDate.Date) }, cancellationToken);
            var result = await action(); if (owned != null) await owned.CommitAsync(cancellationToken); return result;
        }
        catch { if (owned != null) { await owned.RollbackAsync(cancellationToken); _context.ChangeTracker.Clear(); } throw; }
    }

    public Task<RawAttendance?> GetRawEventAsync(long id, CancellationToken cancellationToken = default)
        => _context.RawAttendances.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<RawAttendance?> LockRawEventAsync(long id, string employeeCode, CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.RawAttendances.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.EmployeeCode == employeeCode, cancellationToken);
        if (snapshot == null) return null;
        if (!_context.Database.IsRelational())
            return await _context.RawAttendances.FirstOrDefaultAsync(r => r.Id == id && r.EmployeeCode == employeeCode, cancellationToken);
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction() ?? throw new InvalidOperationException("Raw event locking requires a transaction.");
        command.CommandText = "SELECT 1 FROM HR_RAW_ATTENDANCE WHERE ID=:eventId AND EMPLOYEE_CODE=:employeeCode FOR UPDATE";
        var idParameter = command.CreateParameter(); idParameter.ParameterName = "eventId"; idParameter.Value = id; command.Parameters.Add(idParameter);
        var employeeParameter = command.CreateParameter(); employeeParameter.ParameterName = "employeeCode"; employeeParameter.Value = employeeCode; command.Parameters.Add(employeeParameter);
        if (await command.ExecuteScalarAsync(cancellationToken) == null) return null;
        var current = await _context.RawAttendances.FirstOrDefaultAsync(r => r.Id == id && r.EmployeeCode == employeeCode, cancellationToken);
        if (current != null) await _context.Entry(current).ReloadAsync(cancellationToken);
        return current;
    }

    public Task<RawAttendance> BindRawWorkDateAsync(long id, DateTime workDate, CancellationToken cancellationToken = default)
        => EditPendingRaw(id, async raw =>
        {
            if (raw.IsProcessed) return;
            if (raw.ResolvedWorkDate.HasValue && raw.ResolvedWorkDate.Value.Date != workDate.Date)
                throw new InvalidOperationException("Evidence work date differs from its previous binding; review it before processing.");
            raw.ResolvedWorkDate = workDate.Date; await SaveChangesAsync(cancellationToken);
        }, cancellationToken);

    public Task<RawAttendance> IgnoreRawEventAsync(long id, string reason, string user, CancellationToken cancellationToken = default)
        => EditPendingRaw(id, async raw =>
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) throw new ArgumentException("An ignore reason up to 1000 characters is required.");
            if (raw.IsProcessed && raw.ProcessingOutcome == "IGNORED" && raw.ProcessingReason == reason.Trim()) return;
            if (raw.IsProcessed || raw.AttendanceDayId.HasValue) throw new InvalidOperationException("Applied or terminal evidence cannot be ignored; use the attendance correction workflow.");
            raw.IsProcessed = true; raw.ProcessingOutcome = "IGNORED"; raw.ProcessingReason = reason.Trim(); raw.ProcessedBy = user; raw.ProcessedDate = DateTime.UtcNow;
            await SaveChangesAsync(cancellationToken);
        }, cancellationToken);

    private async Task<RawAttendance> EditPendingRaw(long id, Func<RawAttendance, Task> edit, CancellationToken ct)
    {
        if (_context.Database.CurrentTransaction != null && _context.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Evidence changes require ReadCommitted.");
        await using var owned = _context.Database.CurrentTransaction == null ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct) : null;
        try
        {
            var snapshot = await GetRawEventAsync(id, ct) ?? throw new KeyNotFoundException("Raw event not found in the current HR scope.");
            await LockEvidenceEmployee(snapshot.EmployeeCode, ct);
            var raw = await LockRawEventAsync(id, snapshot.EmployeeCode, ct) ?? throw new KeyNotFoundException("Raw event not found.");
            await edit(raw); if (owned != null) await owned.CommitAsync(ct); return raw;
        }
        catch { if (owned != null) { await owned.RollbackAsync(ct); _context.ChangeTracker.Clear(); } throw; }
    }

    public async Task<IReadOnlyList<RawAttendance>> GetProcessedDayEventsAsync(string employeeCode, DateTime workDate, CancellationToken cancellationToken = default)
        => await _context.RawAttendances.AsNoTracking().Where(r => r.EmployeeCode == employeeCode && r.IsProcessed && r.ResolvedWorkDate == workDate.Date && r.ProcessingOutcome == "APPLIED")
            .OrderBy(r => r.PunchTime).ThenBy(r => r.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RawAttendance>> GetEarlierPendingEventsAsync(string employeeCode, DateTime punchTime, long id, CancellationToken cancellationToken = default)
    {
        var from = punchTime.Date > DateTime.MinValue.AddDays(3) ? punchTime.Date.AddDays(-3) : DateTime.MinValue;
        return await _context.RawAttendances.AsNoTracking().Where(r => r.EmployeeCode == employeeCode && !r.IsProcessed && r.EventId != null && r.PunchTime >= from &&
            (r.PunchTime < punchTime || r.PunchTime == punchTime && r.Id < id)).OrderBy(r => r.PunchTime).ThenBy(r => r.Id).Take(1001).ToListAsync(cancellationToken);
    }

    public Task<bool> HasApprovedCorrectionAsync(string employeeCode, DateTime workDate, CancellationToken cancellationToken = default)
        => _context.AttendanceCorrectionRequests.AsNoTracking().AnyAsync(r => r.EmployeeCode == employeeCode && r.AttendanceDate.Date == workDate.Date && r.Status == "APPROVED", cancellationToken);

    public Task UpdateRawAttendanceAsync(RawAttendance raw, CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(raw);
        if (entry.State == EntityState.Detached) throw new InvalidOperationException("Raw processing must update a locked tracked event.");
        foreach (var property in new[] { nameof(RawAttendance.IsProcessed), nameof(RawAttendance.ResolvedWorkDate), nameof(RawAttendance.AttendanceDayId),
                     nameof(RawAttendance.ProcessingOutcome), nameof(RawAttendance.ProcessingReason), nameof(RawAttendance.ProcessedBy), nameof(RawAttendance.ProcessedDate), nameof(RawAttendance.LastAttemptedAtUtc), nameof(RawAttendance.LastAttemptedBy) })
            entry.Property(property).IsModified = true;
        return Task.CompletedTask;
    }

    private async Task LockEvidenceEmployee(string employeeCode, CancellationToken cancellationToken)
    {
            var employee = await _context.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeCode == employeeCode, cancellationToken)
                ?? throw new KeyNotFoundException("Employee not found in the current HR scope.");
            if (_context.HrScopeEnabled)
            {
                var company = employee.BranchId.HasValue ? await _context.SysBranches.Where(b => b.Id == employee.BranchId).Select(b => b.CompanyId).SingleOrDefaultAsync(cancellationToken) : _context.HrScopeCompanyId;
                if (company != _context.HrScopeCompanyId || company <= 0) throw new InvalidOperationException("Employee company is outside the current HR scope.");
            }
            if (!_context.Database.IsRelational()) return;
            await using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = _context.Database.CurrentTransaction!.GetDbTransaction();
                command.CommandText = "SELECT 1 FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE=:employeeCode FOR UPDATE";
                var parameter = command.CreateParameter(); parameter.ParameterName = "employeeCode"; parameter.Value = employeeCode; command.Parameters.Add(parameter);
                if (await command.ExecuteScalarAsync(cancellationToken) == null) throw new KeyNotFoundException("Employee not found.");
            }
            if (!await _context.Employees.AsNoTracking().AnyAsync(e => e.EmployeeCode == employeeCode && e.BranchId == employee.BranchId, cancellationToken))
                throw new InvalidOperationException("Employee scope changed while acquiring the evidence lock; retry.");
    }

    public async Task<AttendanceCorrectionRequest> RejectCorrectionAsync(long id, string reason, string user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) throw new ArgumentException("A rejection reason up to 1000 characters is required.");
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction != null && _context.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Correction review requires ReadCommitted.");
        await using var owned = _context.Database.CurrentTransaction == null ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            var snapshot = await GetCorrectionRequestSnapshotAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Correction not found in the current HR scope.");
            await LockEvidenceEmployee(snapshot.EmployeeCode, cancellationToken);
            var request = await GetCorrectionRequestByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Correction not found.");
            if (request.Status != "PENDING") throw new InvalidOperationException("Only a pending correction can be rejected.");
            request.Status = "REJECTED"; request.RejectionReason = reason.Trim(); request.ApprovedBy = user; request.ApprovalDate = DateTime.UtcNow;
            request.UpdateUser = user; request.UpdateDate = DateTime.UtcNow;
            await SaveChangesAsync(cancellationToken); if (owned != null) await owned.CommitAsync(cancellationToken); return request;
        }
        catch { if (owned != null) { await owned.RollbackAsync(cancellationToken); _context.ChangeTracker.Clear(); } throw; }
    }

    public async Task<AttendanceCorrectionRequest> SubmitCorrectionAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        AttendanceCorrectionRules.ValidateInput(request);
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction != null && _context.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Correction submission requires ReadCommitted.");
        await using var owned = _context.Database.CurrentTransaction == null ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            await LockEvidenceEmployee(request.EmployeeCode, cancellationToken);
            var day = await GetAttendanceDayAsync(request.EmployeeCode, request.AttendanceDate, cancellationToken);
            AttendanceCorrectionRules.CaptureOriginal(request, day);
            await _context.AttendanceCorrectionRequests.AddAsync(request, cancellationToken); await SaveChangesAsync(cancellationToken);
            await _context.Entry(request).ReloadAsync(cancellationToken);
            if (owned != null) await owned.CommitAsync(cancellationToken); return request;
        }
        catch { if (owned != null) { await owned.RollbackAsync(cancellationToken); _context.ChangeTracker.Clear(); } throw; }
    }

    public Task<RawAttendance?> GetMobileEventAsync(string employeeCode, string eventId, CancellationToken cancellationToken = default)
        => _context.RawAttendances.AsNoTracking().SingleOrDefaultAsync(r => r.EmployeeCode == employeeCode && r.EventId == eventId, cancellationToken);

    public async Task AddRawAttendanceAsync(RawAttendance raw, CancellationToken cancellationToken = default)
    {
        await _context.RawAttendances.AddAsync(raw, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is Oracle.ManagedDataAccess.Client.OracleException { Number: 1 } oracle &&
            oracle.Message.Contains("UX_HR_MOBILE_EVENT", StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("A concurrent mobile event was accepted. Replay the same event ID.", ex); }
    }
}
