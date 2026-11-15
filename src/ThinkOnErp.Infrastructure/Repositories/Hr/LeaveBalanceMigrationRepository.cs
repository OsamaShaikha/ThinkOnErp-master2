using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed partial class LeaveRepository
{
    public async Task<IReadOnlyList<LeaveBalance>> GetAllLeaveBalancesForMigrationAsync(CancellationToken cancellationToken = default)
        => await _context.LeaveBalances.AsNoTracking().OrderBy(b => b.EmployeeCode).ThenBy(b => b.LeaveTypeCode)
            .ThenBy(b => b.CycleStartDate).ThenBy(b => b.Id).ToListAsync(cancellationToken);

    public Task<LeaveBalanceMigration?> GetLeaveBalanceMigrationAsync(long id, CancellationToken cancellationToken = default)
        => _context.LeaveBalanceMigrations.AsNoTracking().Include(m => m.Lines)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<LeaveBalanceMigration?> GetLeaveBalanceMigrationByKeyAsync(string key, CancellationToken cancellationToken = default)
        => _context.LeaveBalanceMigrations.AsNoTracking().Include(m => m.Lines)
            .FirstOrDefaultAsync(m => m.MigrationKey == key, cancellationToken);

    public async Task<LeaveBalanceMigration> SaveLeaveBalanceMigrationDraftAsync(LeaveBalanceMigration migration, CancellationToken cancellationToken = default)
    {
        if (migration.Id != 0 || migration.Status != "DRAFT" || migration.Lines.Count == 0)
            throw new InvalidOperationException("A new migration draft with at least one reviewed allocation is required.");
        await _context.LeaveBalanceMigrations.AddAsync(migration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return migration;
    }

    public async Task<LeaveBalanceMigration?> ApplyLeaveBalanceMigrationAsync(long id, string user, bool reverse, CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            throw new InvalidOperationException("Leave-balance migration posting requires a relational transaction provider.");
        await using var owned = _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            var transaction = _context.Database.CurrentTransaction!;
            var connection = _context.Database.GetDbConnection();
            await using (var lockBatch = connection.CreateCommand())
            {
                lockBatch.Transaction = transaction.GetDbTransaction();
                lockBatch.CommandText = "SELECT ID FROM HR_LEAVE_BALANCE_MIGRATION WHERE ID=:recordId FOR UPDATE";
                var p = lockBatch.CreateParameter(); p.ParameterName = "recordId"; p.Value = id; lockBatch.Parameters.Add(p);
                if (await lockBatch.ExecuteScalarAsync(cancellationToken) == null) return null;
            }

            foreach (var entry in _context.ChangeTracker.Entries<LeaveBalanceMigrationLine>().Where(e => e.Entity.MigrationId == id).ToList())
                entry.State = EntityState.Detached;
            foreach (var entry in _context.ChangeTracker.Entries<LeaveBalanceMigration>().Where(e => e.Entity.Id == id).ToList())
                entry.State = EntityState.Detached;
            var migration = await _context.LeaveBalanceMigrations.Include(m => m.Lines)
                .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (migration == null) return null;
            if ((!reverse && migration.Status == "POSTED") || (reverse && migration.Status == "REVERSED"))
            {
                if (owned != null) await owned.CommitAsync(cancellationToken);
                return migration;
            }
            if (reverse ? migration.Status != "POSTED" : migration.Status != "DRAFT")
                throw new InvalidOperationException(reverse ? "Only a posted migration can be reversed." : "Only a draft migration can be posted.");

            if (_context.Database.IsOracle())
                await HrLeaveSettingsMutationLock.LockAsync(_context, user, cancellationToken);
            var settings = await _context.HrLeaveSettings.FirstOrDefaultAsync(s => s.Id == 1, cancellationToken);
            var currentMonth = settings?.LeaveYearStartMonth ?? 1;
            var currentDay = settings?.LeaveYearStartDay ?? 1;
            var expectedMonth = reverse ? migration.TargetYearStartMonth : migration.SourceYearStartMonth;
            var expectedDay = reverse ? migration.TargetYearStartDay : migration.SourceYearStartDay;
            if (currentMonth != expectedMonth || currentDay != expectedDay)
                throw new InvalidOperationException("The company leave-year setting changed after this migration draft. Rebuild the migration against the current setting.");

            var sourceIds = migration.Lines.Select(l => l.SourceBalanceId).Distinct().OrderBy(x => x).ToArray();
            var employees = migration.Lines.Select(l => l.EmployeeCode).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            foreach (var employeeCode in employees)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT 1 FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE=:employeeCode FOR UPDATE";
                var parameter = command.CreateParameter(); parameter.ParameterName = "employeeCode"; parameter.Value = employeeCode; command.Parameters.Add(parameter);
                if (await command.ExecuteScalarAsync(cancellationToken) == null)
                    throw new InvalidOperationException($"Employee '{employeeCode}' no longer exists.");
            }
            foreach (var leaveTypeCode in migration.Lines.Select(l => l.LeaveTypeCode).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
                await LockLeaveTypesAsync(leaveTypeCode, cancellationToken);

            var sources = new Dictionary<long, LeaveBalance>();
            foreach (var sourceId in sourceIds)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT ID FROM HR_LEAVE_BALANCE WHERE ID=:balanceId FOR UPDATE";
                var parameter = command.CreateParameter(); parameter.ParameterName = "balanceId"; parameter.Value = sourceId; command.Parameters.Add(parameter);
                if (await command.ExecuteScalarAsync(cancellationToken) == null)
                    throw new InvalidOperationException($"Source balance {sourceId} no longer exists.");
                var source = await _context.LeaveBalances.FirstAsync(b => b.Id == sourceId, cancellationToken);
                await _context.Entry(source).ReloadAsync(cancellationToken);
                sources.Add(sourceId, source);
            }

            var sourceSet = sourceIds.ToHashSet();
            var targetGroups = migration.Lines.GroupBy(l => (l.EmployeeCode, l.LeaveTypeCode, Start: l.TargetCycleStartDate.Date))
                .OrderBy(g => g.Key.EmployeeCode, StringComparer.Ordinal).ThenBy(g => g.Key.LeaveTypeCode, StringComparer.Ordinal).ThenBy(g => g.Key.Start).ToList();
            var targetRows = new Dictionary<(string EmployeeCode, string LeaveTypeCode, DateTime Start), LeaveBalance>();
            foreach (var group in targetGroups)
            {
                var targetIds = group.Select(l => l.TargetBalanceId).Distinct().ToArray();
                if (targetIds.Length != 1) throw new InvalidOperationException("A migration draft has inconsistent target balance snapshots.");
                var existingId = targetIds[0];
                LeaveBalance? target = existingId.HasValue
                    ? await _context.LeaveBalances.FirstOrDefaultAsync(b => b.Id == existingId.Value, cancellationToken)
                    : await GetLeaveBalanceByCycleAsync(group.Key.EmployeeCode, group.Key.LeaveTypeCode, group.Key.Start, cancellationToken);
                if (target != null)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction.GetDbTransaction();
                    command.CommandText = "SELECT ID FROM HR_LEAVE_BALANCE WHERE ID=:balanceId FOR UPDATE";
                    var parameter = command.CreateParameter(); parameter.ParameterName = "balanceId"; parameter.Value = target.Id; command.Parameters.Add(parameter);
                    await command.ExecuteScalarAsync(cancellationToken);
                    await _context.Entry(target).ReloadAsync(cancellationToken);
                }
                if (!reverse)
                {
                    if (existingId.HasValue && target == null) throw new InvalidOperationException("A target balance from the draft no longer exists.");
                    if (!existingId.HasValue && target != null) throw new InvalidOperationException("A target balance was created after the draft; rebuild the migration.");
                    var line = group.First();
                    if (target != null && !sourceSet.Contains(target.Id) &&
                        (target.AccruedDays != line.TargetAccruedBefore || target.UsedDays != line.TargetUsedBefore || target.CarriedForwardDays != line.TargetCarriedBefore))
                        throw new InvalidOperationException("A target balance changed after the draft; rebuild the migration.");
                }
                else if (target == null)
                    throw new InvalidOperationException("A target balance from the posted migration no longer exists.");
                if (target != null) targetRows[group.Key] = target;
            }

            foreach (var line in migration.Lines)
            {
                await EnsureLeaveCycleOpenCoreAsync(line.EmployeeCode, line.LeaveTypeCode, line.SourceCycleStartDate, line.SourceCycleEndDate, null, cancellationToken);
                await EnsureLeaveCycleOpenCoreAsync(line.EmployeeCode, line.LeaveTypeCode, line.TargetCycleStartDate, line.TargetCycleEndDate, null, cancellationToken);
                if (!reverse && await _context.LeaveRequests.AnyAsync(r => r.EmployeeCode == line.EmployeeCode && r.LeaveTypeCode == line.LeaveTypeCode &&
                    r.Status == "PENDING" && r.StartDate <= line.SourceCycleEndDate && r.EndDate >= line.SourceCycleStartDate, cancellationToken))
                    throw new InvalidOperationException($"Resolve pending requests for {line.EmployeeCode}/{line.LeaveTypeCode} before posting the migration.");
                if (reverse && await _context.LeaveRequests.AnyAsync(r => r.EmployeeCode == line.EmployeeCode && r.LeaveTypeCode == line.LeaveTypeCode &&
                    r.Status == "PENDING" && r.StartDate <= line.TargetCycleEndDate && r.EndDate >= line.TargetCycleStartDate, cancellationToken))
                    throw new InvalidOperationException($"Resolve pending requests for {line.EmployeeCode}/{line.LeaveTypeCode} before reversing the migration.");
                if (!reverse)
                {
                    var approved = await _context.LeaveRequests.AsNoTracking().Where(r => r.EmployeeCode == line.EmployeeCode && r.LeaveTypeCode == line.LeaveTypeCode &&
                        r.Status == "APPROVED" && r.StartDate <= line.SourceCycleEndDate && r.EndDate >= line.SourceCycleStartDate).ToListAsync(cancellationToken);
                    foreach (var request in approved)
                    {
                        var startCycle = GetCycleStart(request.StartDate, migration.TargetYearStartMonth, migration.TargetYearStartDay);
                        var endCycle = GetCycleStart(request.EndDate, migration.TargetYearStartMonth, migration.TargetYearStartDay);
                        if (startCycle != endCycle)
                            throw new InvalidOperationException($"Approved leave request {request.Id} crosses a target cycle boundary. Split or reconcile it before migration.");
                        if (!migration.Lines.Any(candidate => candidate.SourceBalanceId == line.SourceBalanceId && candidate.TargetCycleStartDate == startCycle))
                            throw new InvalidOperationException($"The reviewed allocation for source balance {line.SourceBalanceId} does not place approved request {request.Id} in its new cycle.");
                    }
                }
            }

            var migrationSource = $"LEAVE_BALANCE_MIGRATION:{migration.Id}";
            if (!reverse)
            {
                foreach (var source in sources.Values)
                {
                    var lines = migration.Lines.Where(l => l.SourceBalanceId == source.Id).ToList();
                    var line = lines[0];
                    if ((source.CycleStartDate?.Date ?? new DateTime(source.YearNo, 1, 1)) != line.SourceCycleStartDate ||
                        (source.CycleEndDate?.Date ?? new DateTime(source.YearNo, 12, 31)) != line.SourceCycleEndDate ||
                        source.YearNo != line.SourceYearNo || source.CycleStartDate.HasValue == line.SourceCycleStartWasNull ||
                        source.CycleEndDate.HasValue == line.SourceCycleEndWasNull ||
                        source.AccruedDays != line.SourceAccruedDays || source.UsedDays != line.SourceUsedDays || source.CarriedForwardDays != line.SourceCarriedDays)
                        throw new InvalidOperationException($"Source balance {source.Id} changed after the reviewed draft.");
                    if (await _context.LeaveRequests.AnyAsync(r => r.EmployeeCode == source.EmployeeCode && r.LeaveTypeCode == source.LeaveTypeCode &&
                        r.Status == "PENDING" && r.StartDate <= line.SourceCycleEndDate && r.EndDate >= line.SourceCycleStartDate, cancellationToken))
                        throw new InvalidOperationException($"Resolve pending requests for {source.EmployeeCode}/{source.LeaveTypeCode} before posting the migration.");
                }

                foreach (var source in sources.Values.OrderBy(b => b.EmployeeCode, StringComparer.Ordinal).ThenBy(b => b.LeaveTypeCode, StringComparer.Ordinal).ThenBy(b => b.Id))
                {
                    var before = LeaveBalanceState.From(source);
                    source.AccruedDays = source.UsedDays = source.CarriedForwardDays = 0m;
                    source.UpdateUser = user; source.UpdateDate = DateTime.UtcNow;
                    var key = LeaveMovementKey.Hash($"{migrationSource}:OUT:{source.Id}");
                    await RecordMovementAsync(source, before, "MIGRATION_OUT", key, migrationSource, "Reviewed leave-cycle migration out", user, cancellationToken: cancellationToken);
                }
                await _context.SaveChangesAsync(cancellationToken);
                foreach (var source in sources.Values)
                {
                    var key = LeaveMovementKey.Hash($"{migrationSource}:OUT:{source.Id}");
                    var movement = await _context.Set<LeaveBalanceMovement>().SingleAsync(m => m.OperationKey == key, cancellationToken);
                    foreach (var line in migration.Lines.Where(l => l.SourceBalanceId == source.Id)) line.SourceMovementId = movement.Id;
                }

                foreach (var group in targetGroups)
                {
                    var allocated = new LeaveBalanceState(group.Sum(l => l.AllocatedAccruedDays), group.Sum(l => l.AllocatedUsedDays), group.Sum(l => l.AllocatedCarriedDays));
                    targetRows.TryGetValue(group.Key, out var target);
                    if (target == null)
                    {
                        target = new LeaveBalance { EmployeeCode = group.Key.EmployeeCode, LeaveTypeCode = group.Key.LeaveTypeCode,
                            YearNo = group.Key.Start.Year, CycleStartDate = group.Key.Start, CycleEndDate = group.First().TargetCycleEndDate,
                            CreationUser = user, CreationDate = DateTime.UtcNow };
                        await _context.LeaveBalances.AddAsync(target, cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);
                        targetRows[group.Key] = target;
                    }
                    var before = sourceSet.Contains(target.Id) ? new LeaveBalanceState(0, 0, 0) : LeaveBalanceState.From(target);
                    target.AccruedDays = before.Accrued + allocated.Accrued;
                    target.UsedDays = before.Used + allocated.Used;
                    target.CarriedForwardDays = before.Carried + allocated.Carried;
                    target.CycleStartDate = group.Key.Start;
                    target.CycleEndDate = group.First().TargetCycleEndDate;
                    target.YearNo = group.Key.Start.Year;
                    target.UpdateUser = user; target.UpdateDate = DateTime.UtcNow;
                    var targetKey = LeaveMovementKey.Hash($"{migrationSource}:IN:{group.Key.EmployeeCode}:{group.Key.LeaveTypeCode}:{group.Key.Start:yyyyMMdd}");
                    await RecordMovementAsync(target, before, "MIGRATION_IN", targetKey, migrationSource, "Reviewed leave-cycle migration in", user, cancellationToken: cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    var movement = await _context.Set<LeaveBalanceMovement>().SingleAsync(m => m.OperationKey == targetKey, cancellationToken);
                    foreach (var line in group) { line.TargetBalanceId = target.Id; line.TargetMovementId = movement.Id; }
                }

                settings ??= new HrLeaveSettings { Id = 1, CreationUser = user, CreationDate = DateTime.UtcNow };
                settings.LeaveYearStartMonth = migration.TargetYearStartMonth;
                settings.LeaveYearStartDay = migration.TargetYearStartDay;
                settings.UpdateUser = user; settings.UpdateDate = DateTime.UtcNow;
                if (_context.Entry(settings).State == EntityState.Detached) await _context.HrLeaveSettings.AddAsync(settings, cancellationToken);
                migration.Status = "POSTED"; migration.PostedBy = user; migration.PostedDate = DateTime.UtcNow;
            }
            else
            {
                var expectedLastMovement = new Dictionary<long, long>();
                foreach (var group in targetGroups)
                {
                    var targetLine = group.First();
                    if (targetLine.TargetBalanceId is long targetBalanceId)
                        expectedLastMovement[targetBalanceId] = targetLine.TargetMovementId
                            ?? throw new InvalidOperationException("Migration target movement link is missing.");
                }
                foreach (var source in sources.Values)
                {
                    var sourceLine = migration.Lines.First(l => l.SourceBalanceId == source.Id);
                    if (!expectedLastMovement.ContainsKey(source.Id)) expectedLastMovement[source.Id] = sourceLine.SourceMovementId
                        ?? throw new InvalidOperationException("Migration source movement link is missing.");
                }
                foreach (var (balanceId, expectedMovementId) in expectedLastMovement)
                {
                    var latest = await _context.Set<LeaveBalanceMovement>().AsNoTracking().Where(m => m.BalanceId == balanceId)
                        .OrderByDescending(m => m.SequenceNo).Select(m => (long?)m.Id).FirstOrDefaultAsync(cancellationToken);
                    if (latest != expectedMovementId)
                        throw new InvalidOperationException("A dependent balance movement exists after migration. Reverse dependent changes before reversing this migration.");
                }

                foreach (var group in targetGroups.AsEnumerable().Reverse())
                {
                    var target = targetRows[group.Key];
                    var targetId = target.Id;
                    var line = group.First();
                    var prior = sourceSet.Contains(targetId)
                        ? new LeaveBalanceState(0, 0, 0)
                        : new LeaveBalanceState(line.TargetAccruedBefore, line.TargetUsedBefore, line.TargetCarriedBefore);
                    var allocation = new LeaveBalanceState(group.Sum(l => l.AllocatedAccruedDays), group.Sum(l => l.AllocatedUsedDays), group.Sum(l => l.AllocatedCarriedDays));
                    if (LeaveBalanceState.From(target) != new LeaveBalanceState(prior.Accrued + allocation.Accrued, prior.Used + allocation.Used, prior.Carried + allocation.Carried))
                        throw new InvalidOperationException("A target balance changed after migration; reverse dependent transactions before reversing the migration.");
                    var before = LeaveBalanceState.From(target);
                    target.AccruedDays = prior.Accrued; target.UsedDays = prior.Used; target.CarriedForwardDays = prior.Carried;
                    target.UpdateUser = user; target.UpdateDate = DateTime.UtcNow;
                    var targetMovementId = line.TargetMovementId ?? throw new InvalidOperationException("Migration target movement link is missing.");
                    var targetMovement = await _context.Set<LeaveBalanceMovement>().AsNoTracking().FirstAsync(m => m.Id == targetMovementId, cancellationToken);
                    await RecordMovementAsync(target, before, "REVERSAL", LeaveMovementKey.Hash($"{migrationSource}:REV:IN:{targetId}:{group.Key.Start:yyyyMMdd}"),
                        migrationSource, "Reverse reviewed leave-cycle migration in", user, reversesId: targetMovement.Id, cancellationToken: cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                foreach (var source in sources.Values.OrderByDescending(b => b.EmployeeCode, StringComparer.Ordinal).ThenByDescending(b => b.LeaveTypeCode, StringComparer.Ordinal).ThenByDescending(b => b.Id))
                {
                    var line = migration.Lines.First(l => l.SourceBalanceId == source.Id);
                    var before = LeaveBalanceState.From(source);
                    if (before != new LeaveBalanceState(0, 0, 0))
                        throw new InvalidOperationException($"Source balance {source.Id} changed after migration; reverse dependent changes first.");
                    source.AccruedDays = line.SourceAccruedDays; source.UsedDays = line.SourceUsedDays; source.CarriedForwardDays = line.SourceCarriedDays;
                    source.CycleStartDate = line.SourceCycleStartWasNull ? null : line.SourceCycleStartDate;
                    source.CycleEndDate = line.SourceCycleEndWasNull ? null : line.SourceCycleEndDate;
                    source.YearNo = line.SourceYearNo;
                    source.UpdateUser = user; source.UpdateDate = DateTime.UtcNow;
                    var sourceMovementId = line.SourceMovementId ?? throw new InvalidOperationException("Migration source movement link is missing.");
                    var sourceMovement = await _context.Set<LeaveBalanceMovement>().AsNoTracking().FirstAsync(m => m.Id == sourceMovementId, cancellationToken);
                    await RecordMovementAsync(source, before, "REVERSAL", LeaveMovementKey.Hash($"{migrationSource}:REV:OUT:{source.Id}"),
                        migrationSource, "Reverse reviewed leave-cycle migration out", user, reversesId: sourceMovement.Id, cancellationToken: cancellationToken);
                }
                settings ??= await _context.HrLeaveSettings.FirstOrDefaultAsync(s => s.Id == 1, cancellationToken);
                if (settings == null)
                    throw new InvalidOperationException("Original leave-year settings were not persisted; cannot safely reverse this migration.");
                settings.LeaveYearStartMonth = migration.SourceYearStartMonth;
                settings.LeaveYearStartDay = migration.SourceYearStartDay;
                settings.UpdateUser = user; settings.UpdateDate = DateTime.UtcNow;
                migration.Status = "REVERSED"; migration.ReversedBy = user; migration.ReversedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (owned != null) await owned.CommitAsync(cancellationToken);
            return migration;
        }
        catch
        {
            if (owned != null)
            {
                await owned.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
            }
            throw;
        }
    }

    private static DateTime GetCycleStart(DateTime date, int month, int day)
    {
        var year = date.Year;
        var boundary = new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
        if (date.Date < boundary) year--;
        return new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
    }
}
