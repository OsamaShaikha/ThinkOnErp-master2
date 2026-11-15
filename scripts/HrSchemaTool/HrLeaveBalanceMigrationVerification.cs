using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrLeaveBalanceMigrationVerification
{
    public static async Task Verify(string connectionString, List<string> report)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connectionString,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
        await using var context = new OracleDbContext(options);
        await context.Database.OpenConnectionAsync();
        await using (var schema = context.Database.GetDbConnection().CreateCommand())
        {
            schema.CommandText = "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE";
            await schema.ExecuteNonQueryAsync();
        }
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var repository = new LeaveRepository(context);
            var settings = await repository.GetHrLeaveSettingsAsync();
            var settingsWereStored = await context.HrLeaveSettings.AsNoTracking().AnyAsync(s => s.Id == 1);
            var originals = await repository.GetAllLeaveBalancesForMigrationAsync();
            if (originals.Count == 0) throw new InvalidOperationException("Migration workflow verification requires existing balance rows.");
            if (await context.LeaveRequests.AnyAsync(r => r.Status == "PENDING") || await context.LeaveYearCloses.AnyAsync(c => c.Status == "POSTED"))
                throw new InvalidOperationException("Migration workflow verification refuses existing pending leave or posted close rows.");

            var sourceMap = originals.ToDictionary(b => b.Id);
            var targetMonth = settings.LeaveYearStartMonth == 7 ? 4 : 7;
            const int targetDay = 1;
            var migrationKey = $"VERIFY-{Guid.NewGuid():N}";
            var migration = new LeaveBalanceMigration
            {
                MigrationKey = migrationKey,
                EffectiveDate = DateTime.UtcNow.Date,
                SourceYearStartMonth = settings.LeaveYearStartMonth,
                SourceYearStartDay = settings.LeaveYearStartDay,
                TargetYearStartMonth = targetMonth,
                TargetYearStartDay = targetDay,
                PayloadHash = LeaveMovementKey.Hash(migrationKey),
                Status = "DRAFT",
                CreationUser = "HR_SCHEMA_VERIFY",
                CreationDate = DateTime.UtcNow
            };

            foreach (var source in originals.OrderBy(b => b.EmployeeCode, StringComparer.Ordinal).ThenBy(b => b.LeaveTypeCode, StringComparer.Ordinal).ThenBy(b => b.Id))
            {
                var sourceStart = source.CycleStartDate?.Date ?? new DateTime(source.YearNo, 1, 1);
                var sourceEnd = source.CycleEndDate?.Date ?? new DateTime(source.YearNo, 12, 31);
                var targetStart = GetCycleStart(sourceStart, targetMonth, targetDay);
                var targetEnd = GetCycleEnd(targetStart, targetMonth, targetDay);
                var target = await repository.GetLeaveBalanceByCycleAsync(source.EmployeeCode, source.LeaveTypeCode, targetStart);
                migration.Lines.Add(new LeaveBalanceMigrationLine
                {
                    SourceBalanceId = source.Id,
                    TargetBalanceId = target?.Id,
                    EmployeeCode = source.EmployeeCode,
                    LeaveTypeCode = source.LeaveTypeCode,
                    SourceCycleStartDate = sourceStart,
                    SourceCycleEndDate = sourceEnd,
                    SourceYearNo = source.YearNo,
                    SourceCycleStartWasNull = !source.CycleStartDate.HasValue,
                    SourceCycleEndWasNull = !source.CycleEndDate.HasValue,
                    SourceAccruedDays = source.AccruedDays,
                    SourceUsedDays = source.UsedDays,
                    SourceCarriedDays = source.CarriedForwardDays,
                    TargetCycleStartDate = targetStart,
                    TargetCycleEndDate = targetEnd,
                    AllocatedAccruedDays = source.AccruedDays,
                    AllocatedUsedDays = source.UsedDays,
                    AllocatedCarriedDays = source.CarriedForwardDays,
                    TargetAccruedBefore = target?.AccruedDays ?? 0m,
                    TargetUsedBefore = target?.UsedDays ?? 0m,
                    TargetCarriedBefore = target?.CarriedForwardDays ?? 0m
                });
            }
            await context.LeaveBalanceMigrations.AddAsync(migration);
            await context.SaveChangesAsync();

            var posted = await repository.ApplyLeaveBalanceMigrationAsync(migration.Id, "HR_SCHEMA_VERIFY", reverse: false)
                ?? throw new InvalidOperationException("Migration workflow did not find its draft.");
            if (posted.Status != "POSTED") throw new InvalidOperationException("Migration workflow did not post the reviewed batch.");
            var postSettings = await repository.GetHrLeaveSettingsAsync();
            if (postSettings.LeaveYearStartMonth != targetMonth || postSettings.LeaveYearStartDay != targetDay)
                throw new InvalidOperationException("Posting the migration did not update tenant leave-year settings.");
            var sourceIds = originals.Select(b => b.Id).ToHashSet();
            foreach (var group in posted.Lines.GroupBy(l => (l.EmployeeCode, l.LeaveTypeCode, l.TargetCycleStartDate)))
            {
                var targetId = group.Select(l => l.TargetBalanceId).Distinct().SingleOrDefault()
                    ?? throw new InvalidOperationException("Posted migration did not persist a target balance link.");
                var targetBalance = await context.LeaveBalances.SingleAsync(b => b.Id == targetId);
                var prior = sourceIds.Contains(targetId) ? new LeaveBalanceState(0, 0, 0)
                    : new LeaveBalanceState(group.First().TargetAccruedBefore, group.First().TargetUsedBefore, group.First().TargetCarriedBefore);
                var expected = new LeaveBalanceState(prior.Accrued + group.Sum(l => l.AllocatedAccruedDays),
                    prior.Used + group.Sum(l => l.AllocatedUsedDays), prior.Carried + group.Sum(l => l.AllocatedCarriedDays));
                if (LeaveBalanceState.From(targetBalance) != expected)
                    throw new InvalidOperationException("Posted migration did not transfer allocated balance components to the reviewed target cycle.");
            }

            var reversed = await repository.ApplyLeaveBalanceMigrationAsync(migration.Id, "HR_SCHEMA_VERIFY", reverse: true)
                ?? throw new InvalidOperationException("Migration workflow did not find its posted batch for reversal.");
            if (reversed.Status != "REVERSED") throw new InvalidOperationException("Migration workflow did not reverse the posted batch.");
            var restoredSettings = await repository.GetHrLeaveSettingsAsync();
            if (restoredSettings.LeaveYearStartMonth != settings.LeaveYearStartMonth || restoredSettings.LeaveYearStartDay != settings.LeaveYearStartDay)
                throw new InvalidOperationException("Reversing the migration did not restore the source leave-year settings.");
            foreach (var original in originals)
            {
                var restored = await context.LeaveBalances.SingleAsync(b => b.Id == original.Id);
                var expectedStart = original.CycleStartDate?.Date;
                var expectedEnd = original.CycleEndDate?.Date;
                if (restored.YearNo != original.YearNo || restored.CycleStartDate?.Date != expectedStart || restored.CycleEndDate?.Date != expectedEnd ||
                    restored.AccruedDays != original.AccruedDays || restored.UsedDays != original.UsedDays || restored.CarriedForwardDays != original.CarriedForwardDays)
                    throw new InvalidOperationException("Migration reversal did not exactly restore a source balance.");
            }

            await transaction.RollbackAsync();
            context.ChangeTracker.Clear();
            if (await context.LeaveBalanceMigrations.AnyAsync(m => m.MigrationKey == migrationKey))
                throw new InvalidOperationException("Migration verification fixture survived rollback.");
            if (await context.HrLeaveSettings.AsNoTracking().AnyAsync(s => s.Id == 1) != settingsWereStored)
                throw new InvalidOperationException("Migration verification rollback changed the tenant settings-row state.");
            foreach (var original in originals)
            {
                var afterRollback = await context.LeaveBalances.AsNoTracking().SingleAsync(b => b.Id == original.Id);
                if (afterRollback.YearNo != original.YearNo || afterRollback.CycleStartDate?.Date != original.CycleStartDate?.Date ||
                    afterRollback.CycleEndDate?.Date != original.CycleEndDate?.Date || afterRollback.AccruedDays != original.AccruedDays ||
                    afterRollback.UsedDays != original.UsedDays || afterRollback.CarriedForwardDays != original.CarriedForwardDays)
                    throw new InvalidOperationException("Migration verification rollback changed a source balance.");
            }
            report.Add($"Leave balance migration workflow: posted/reversed {originals.Count} source balance rows in an outer Oracle transaction; settings and every balance component restored; fixture absent after rollback.");
        }
        catch
        {
            await transaction.RollbackAsync();
            context.ChangeTracker.Clear();
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

    private static DateTime GetCycleEnd(DateTime start, int month, int day)
    {
        var nextYear = start.Year + 1;
        var next = new DateTime(nextYear, month, Math.Min(day, DateTime.DaysInMonth(nextYear, month)));
        return next.AddDays(-1);
    }
}
