using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed partial class LeaveRepository
{
    public Task<LeaveBalanceMovement?> GetMovementByKeyAsync(string operationKey, CancellationToken cancellationToken = default)
        => _context.Set<LeaveBalanceMovement>().AsNoTracking().FirstOrDefaultAsync(m => m.OperationKey == operationKey, cancellationToken);

    public Task<LeaveBalanceMovement?> GetMovementByIdAsync(long movementId, CancellationToken cancellationToken = default)
        => _context.Set<LeaveBalanceMovement>().AsNoTracking().FirstOrDefaultAsync(m => m.Id == movementId, cancellationToken);

    public Task<bool> HasMovementReversalAsync(long movementId, CancellationToken cancellationToken = default)
        => _context.Set<LeaveBalanceMovement>().AsNoTracking().AnyAsync(m => m.ReversesMovementId == movementId, cancellationToken);

    public Task<bool> HasActiveMovementAfterAsync(long balanceId, long sequenceNo, CancellationToken cancellationToken = default)
        => _context.Set<LeaveBalanceMovement>().AsNoTracking().AnyAsync(m => m.BalanceId == balanceId && m.SequenceNo > sequenceNo &&
            !m.ReversesMovementId.HasValue && !_context.Set<LeaveBalanceMovement>().Any(reversal => reversal.ReversesMovementId == m.Id), cancellationToken);

    public async Task<(IReadOnlyList<LeaveBalanceMovement> Items, int Total)> GetMovementsAsync(string employeeCode,
        string? leaveTypeCode, DateTime? cycleStart, int page, int size, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<LeaveBalanceMovement>().AsNoTracking().Where(m => m.EmployeeCode == employeeCode);
        if (leaveTypeCode != null) query = query.Where(m => m.LeaveTypeCode == leaveTypeCode);
        if (cycleStart.HasValue) query = query.Where(m => m.CycleStartDate == cycleStart.Value.Date);
        return (await query.OrderByDescending(m => m.Id).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken), await query.CountAsync(cancellationToken));
    }

    public async Task RecordMovementAsync(LeaveBalance balance, LeaveBalanceState before, string kind, string operationKey,
        string source, string reason, string actor, string? payloadHash = null, decimal expiredDays = 0, decimal transferredDays = 0,
        long? reversesId = null, CancellationToken cancellationToken = default)
    {
        // Non-relational providers used by functional tests cannot open transactions;
        // relational writes must keep balance and movement atomic.
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Balance and movement must share the employee write transaction.");
        if (await GetMovementByKeyAsync(operationKey, cancellationToken) != null ||
            _context.Set<LeaveBalanceMovement>().Local.Any(m => m.OperationKey == operationKey))
            throw new InvalidOperationException("This leave movement was already applied; replay its original operation instead.");
        var cycle = balance.CycleStartDate ?? new DateTime(balance.YearNo, 1, 1);
        var latest = _context.ChangeTracker.Entries<LeaveBalanceMovement>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).Where(m => m.EmployeeCode == balance.EmployeeCode &&
            m.LeaveTypeCode == balance.LeaveTypeCode && m.CycleStartDate == cycle).OrderByDescending(m => m.SequenceNo).FirstOrDefault();
        latest ??= await _context.Set<LeaveBalanceMovement>().AsNoTracking().Where(m => m.EmployeeCode == balance.EmployeeCode &&
            m.LeaveTypeCode == balance.LeaveTypeCode && m.CycleStartDate == cycle).OrderByDescending(m => m.SequenceNo).FirstOrDefaultAsync(cancellationToken);
        if (latest == null)
        {
            // An observed opening, not a reconstruction of historical transactions.
            var snapshotBalance = new LeaveBalance { Id = balance.Id, EmployeeCode = balance.EmployeeCode, LeaveTypeCode = balance.LeaveTypeCode,
                YearNo = balance.YearNo, CycleStartDate = cycle, AccruedDays = before.Accrued, UsedDays = before.Used, CarriedForwardDays = before.Carried };
            latest = LeaveBalanceMovement.Create(snapshotBalance, new(0, 0, 0), "OPENING_SNAPSHOT",
                Key(System.Text.Json.JsonSerializer.Serialize(new { Kind = "OPENING", balance.EmployeeCode, balance.LeaveTypeCode, Cycle = cycle })), "OBSERVED_BALANCE", "Balance observed when ledger tracking began; earlier history is not reconstructed.", actor, 0);
            latest.Balance = balance;
            _context.Add(latest);
        }
        else if (new LeaveBalanceState(latest.AccruedAfter, latest.UsedAfter, latest.CarriedAfter) != before)
            throw new InvalidOperationException("Balance differs from its ledger. Reconcile the unrecorded change before writing.");

        var movement = LeaveBalanceMovement.Create(balance, before, kind, operationKey, source, reason, actor, latest.SequenceNo + 1);
        movement.PayloadHash = payloadHash;
        movement.ExpiredDays = expiredDays;
        movement.TransferredDays = transferredDays;
        movement.ReversesMovementId = reversesId;
        _context.Add(movement);
    }

    internal static string Key(string source) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)));

    private async Task RecordCloseChangeAsync(LeaveBalance balance, LeaveBalanceState before, LeaveYearClose close,
        bool reverse, string operation, string kind, string user, decimal expired, decimal transferred, CancellationToken cancellationToken)
    {
        var source = $"YEAR_CLOSE:{close.Id}";
        var original = reverse ? await _context.Set<LeaveBalanceMovement>().AsNoTracking()
            .Where(m => m.BalanceId == balance.Id && m.Source == source && m.Kind != "REVERSAL" && m.Kind != "LEGACY_REVERSAL" &&
                !_context.Set<LeaveBalanceMovement>().Any(r => r.ReversesMovementId == m.Id))
            .OrderByDescending(m => m.Id).FirstOrDefaultAsync(cancellationToken) : null;
        var after = LeaveBalanceState.From(balance);
        if (reverse && original == null && await _context.Set<LeaveBalanceMovement>().AnyAsync(m => m.BalanceId == balance.Id && m.Source == source, cancellationToken))
            throw new InvalidOperationException("Close ledger was already reversed or is inconsistent; reconcile it before unposting.");
        if (original != null && await _context.Set<LeaveBalanceMovement>().AnyAsync(m => m.BalanceId == balance.Id && m.SequenceNo > original.SequenceNo &&
            !m.ReversesMovementId.HasValue && !_context.Set<LeaveBalanceMovement>().Any(r => r.ReversesMovementId == m.Id), cancellationToken))
            throw new InvalidOperationException("Reverse dependent balance movements before unposting this close.");
        if (original != null && (after.Accrued - before.Accrued != -original.AccruedDelta ||
            after.Used - before.Used != -original.UsedDelta || after.Carried - before.Carried != -original.CarriedDelta))
            throw new InvalidOperationException("Close reversal does not match the original movement.");
        await RecordMovementAsync(balance, before, reverse ? original == null ? "LEGACY_REVERSAL" : "REVERSAL" : kind,
            Key(System.Text.Json.JsonSerializer.Serialize(new { operation, balance.EmployeeCode, balance.LeaveTypeCode, balance.CycleStartDate })), source,
            reverse ? "Unpost leave-year close" : "Post leave-year close: transfer and expiry snapshot", user,
            expiredDays: reverse ? -expired : expired, transferredDays: reverse ? -transferred : transferred,
            reversesId: original?.Id, cancellationToken: cancellationToken);
    }
}
