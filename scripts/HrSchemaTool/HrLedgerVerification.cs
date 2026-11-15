using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrLedgerVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        var foreignInvalidTriggers = new List<string>();
        await using (var triggerCheck = connection.CreateCommand())
        {
            triggerCheck.CommandText = """
                SELECT t.OWNER,t.TRIGGER_NAME FROM ALL_TRIGGERS t
                JOIN ALL_OBJECTS o ON o.OWNER=t.OWNER AND o.OBJECT_NAME=t.TRIGGER_NAME AND o.OBJECT_TYPE='TRIGGER'
                WHERE t.TABLE_OWNER='DEV_TEMPLATE' AND t.TABLE_NAME='HR_LEAVE_MOVEMENT'
                  AND t.OWNER<>'DEV_TEMPLATE' AND t.STATUS='ENABLED'
                  AND (o.STATUS<>'VALID' OR EXISTS (SELECT 1 FROM ALL_ERRORS e
                      WHERE e.OWNER=t.OWNER AND e.NAME=t.TRIGGER_NAME AND e.TYPE='TRIGGER'))
                ORDER BY t.OWNER,t.TRIGGER_NAME
                """;
            await using var reader = await triggerCheck.ExecuteReaderAsync();
            while (await reader.ReadAsync()) foreignInvalidTriggers.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
        }
        if (foreignInvalidTriggers.Count > 0)
        {
            report.Add("Ledger verification refused before DML: enabled invalid triggers owned outside DEV_TEMPLATE are attached to DEV_TEMPLATE.HR_LEAVE_MOVEMENT: " + string.Join(", ", foreignInvalidTriggers) + ".");
            throw new InvalidOperationException("Repair or remove the reviewed cross-schema trigger artifacts before retrying the leave-ledger verifier.");
        }

        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var repository = new LeaveRepository(db);
        await using (var sessionCommand = connection.CreateCommand())
        {
            sessionCommand.CommandText = "SELECT SYS_CONTEXT('USERENV','SESSION_USER'),SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL";
            await using var sessionReader = await sessionCommand.ExecuteReaderAsync();
            if (await sessionReader.ReadAsync())
                report.Add($"Leave ledger verifier Oracle session: user={sessionReader.GetString(0)}, current-schema={sessionReader.GetString(1)}.");
        }
        var movementEntity = db.Model.FindEntityType(typeof(LeaveBalanceMovement));
        report.Add($"Leave ledger verifier EF mapping: {movementEntity?.GetSchema() ?? "<current schema>"}.{movementEntity?.GetTableName() ?? "<unmapped>"}.");
        var seed = await db.LeaveBalances.AsNoTracking().FirstAsync();
        var start = new DateTime(9010, 1, 1);
        var token = Guid.NewGuid().ToString("N");
        var closeId = 0L;
        var balanceId = 0L;
        var targetId = 0L;
        var dependentCloseId = 0L;
        var manualTypeCode = "VERIFY_MANUAL_" + token[..12].ToUpperInvariant();
        var stage = "load existing balance fixture";
        try
        {
            stage = "seed ledger grant/use and close drafts";
            await repository.ExecuteForLeaveEmployeeAsync(seed.EmployeeCode, async () =>
            {
                await repository.EnsureLeaveCycleOpenAsync(seed.EmployeeCode, seed.LeaveTypeCode, start, start.AddYears(1).AddDays(-1));
                if (await db.LeaveBalances.AnyAsync(b => b.EmployeeCode == seed.EmployeeCode && b.LeaveTypeCode == seed.LeaveTypeCode && (b.YearNo == 9010 || b.YearNo == 9011)))
                    throw new InvalidOperationException("Reserved ledger-test cycles are occupied.");
                var closeType = await db.LeaveTypes.SingleAsync(type => type.LeaveTypeCode == seed.LeaveTypeCode);
                // Keep the verifier's close outcome deterministic even when the tenant's
                // configured policy changes. This policy edit lives only in the outer
                // rollback transaction and is captured by the draft snapshots below.
                closeType.TracksBalance = true;
                closeType.CarryForwardAllowed = true;
                closeType.CarryForwardCapDays = 3m;
                closeType.YearEndExcessAction = "EXPIRE";
                var balance = new LeaveBalance { EmployeeCode = seed.EmployeeCode, LeaveTypeCode = seed.LeaveTypeCode,
                    YearNo = 9010, CycleStartDate = start, CycleEndDate = start.AddYears(1).AddDays(-1), AccruedDays = 10, CreationUser = "HR_VERIFY" };
                db.LeaveBalances.Add(balance);
                var key = LeaveMovementKey.Hash(token + ":grant");
                await repository.RecordMovementAsync(balance, new(0, 0, 0), "MANUAL_GRANT", key, "VERIFY:" + token, "Test grant", "HR_VERIFY");
                await db.SaveChangesAsync();
                balanceId = balance.Id;
                var duplicateBlocked = false;
                try { await repository.RecordMovementAsync(balance, new(0, 0, 0), "MANUAL_GRANT", key, "VERIFY:" + token, "Test retry", "HR_VERIFY"); }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("This leave movement was already applied")) { duplicateBlocked = true; }
                if (!duplicateBlocked) throw new InvalidOperationException("Duplicate ledger key was accepted.");

                var before = LeaveBalanceState.From(balance);
                balance.UsedDays = 2;
                await repository.RecordMovementAsync(balance, before, "USAGE", LeaveMovementKey.Hash(token + ":use"), "VERIFY:" + token, "Test usage", "HR_VERIFY");
                await db.SaveChangesAsync();
                var close = new LeaveYearClose { CloseKey = "VERIFY_LEDGER:" + token, FromYear = 9010, ToYear = 9011,
                    LeaveTypeCode = seed.LeaveTypeCode, CreationUser = "HR_VERIFY" };
                close.Lines.Add(new LeaveYearCloseLine { EmployeeCode = seed.EmployeeCode, LeaveTypeCode = seed.LeaveTypeCode,
                    SourceBalanceId = balance.Id, SourceYear = 9010, TargetYear = 9011,
                    SourceCycleStartDate = start, SourceCycleEndDate = balance.CycleEndDate,
                    TargetCycleStartDate = start.AddYears(1), TargetCycleEndDate = start.AddYears(2).AddDays(-1),
                    SourceAccruedBefore = 10, SourceUsedDays = 2, UnusedDays = 8, CarriedDays = 3, ExpiredDays = 5,
                    TracksBalanceSnapshot = closeType.TracksBalance,
                    CarryForwardAllowedSnapshot = closeType.CarryForwardAllowed,
                    CarryForwardCapDaysSnapshot = closeType.CarryForwardCapDays,
                    YearEndExcessActionSnapshot = closeType.YearEndExcessAction });
                db.LeaveYearCloses.Add(close);
                await db.SaveChangesAsync();
                closeId = close.Id;
                return true;
            });

            stage = "post primary leave-year close";
            var post = await repository.PostLeaveYearCloseAsync(closeId, "HR_VERIFY") ?? throw new InvalidOperationException("Close missing.");
            targetId = post.Lines.Single().TargetBalanceId ?? throw new InvalidOperationException("Missing carry target.");
            var count = await db.Set<LeaveBalanceMovement>().CountAsync(m => m.BalanceId == balanceId || m.BalanceId == targetId);
            await repository.PostLeaveYearCloseAsync(closeId, "HR_VERIFY");
            if (count != 6 || count != await db.Set<LeaveBalanceMovement>().CountAsync(m => m.BalanceId == balanceId || m.BalanceId == targetId))
                throw new InvalidOperationException("Posting retry duplicated movements.");
            var dependent = new LeaveYearClose { CloseKey = "VERIFY_DEPENDENT:" + token, FromYear = 9011, ToYear = 9012,
                LeaveTypeCode = seed.LeaveTypeCode, CreationUser = "HR_VERIFY" };
            dependent.Lines.Add(new LeaveYearCloseLine { EmployeeCode = seed.EmployeeCode, LeaveTypeCode = seed.LeaveTypeCode,
                SourceBalanceId = targetId, SourceYear = 9011, TargetYear = 9012, SourceCycleStartDate = start.AddYears(1),
                SourceCycleEndDate = start.AddYears(2).AddDays(-1), TargetCycleStartDate = start.AddYears(2), TargetCycleEndDate = start.AddYears(3).AddDays(-1),
                SourceCarriedBefore = 3, UnusedDays = 3, CarriedDays = 3, ExpiredDays = 0,
                TracksBalanceSnapshot = true, CarryForwardAllowedSnapshot = true,
                CarryForwardCapDaysSnapshot = 3m, YearEndExcessActionSnapshot = "EXPIRE" });
            db.LeaveYearCloses.Add(dependent);
            await db.SaveChangesAsync();
            dependentCloseId = dependent.Id;
            stage = "post dependent leave-year close and verify reversal guard";
            await repository.PostLeaveYearCloseAsync(dependentCloseId, "HR_VERIFY");
            var dependencyBlocked = false;
            try { await repository.UnpostLeaveYearCloseAsync(closeId, "HR_VERIFY"); }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("This leave cycle is closed.")) { dependencyBlocked = true; }
            if (!dependencyBlocked) throw new InvalidOperationException("Dependent close did not block reversal.");
            await repository.UnpostLeaveYearCloseAsync(dependentCloseId, "HR_VERIFY");
            await repository.UnpostLeaveYearCloseAsync(closeId, "HR_VERIFY");
            await repository.UnpostLeaveYearCloseAsync(closeId, "HR_VERIFY");
            await repository.PostLeaveYearCloseAsync(closeId, "HR_VERIFY");
            await repository.UnpostLeaveYearCloseAsync(closeId, "HR_VERIFY");

            var entries = await db.Set<LeaveBalanceMovement>().AsNoTracking().Where(m => m.BalanceId == balanceId || m.BalanceId == targetId).ToListAsync();
            if (entries.Count != 14 || entries.Count(m => m.ReversesMovementId.HasValue) != 5 || entries.Sum(m => m.ExpiredDays) != 0)
                throw new InvalidOperationException("Close reversal history is incomplete or duplicated.");
            foreach (var sql in new[] { "UPDATE DEV_TEMPLATE.HR_LEAVE_MOVEMENT SET REASON='VERIFY_REJECTED' WHERE ID=:recordId", "DELETE FROM DEV_TEMPLATE.HR_LEAVE_MOVEMENT WHERE ID=:recordId" })
            {
                stage = "verify append-only ledger trigger";
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = sql;
                var parameter = command.CreateParameter();
                parameter.ParameterName = "recordId";
                parameter.Value = entries[0].Id;
                command.Parameters.Add(parameter);
                var protectedHistory = false;
                try { await command.ExecuteNonQueryAsync(); }
                catch (OracleException ex) when (ex.Number == 20071) { protectedHistory = true; }
                if (!protectedHistory) throw new InvalidOperationException("Oracle did not reject ledger mutation.");
            }
            foreach (var group in entries.GroupBy(m => m.BalanceId))
            {
                var last = group.OrderBy(m => m.SequenceNo).Last();
                if (group.Sum(m => m.AccruedDelta) != last.AccruedAfter || group.Sum(m => m.UsedDelta) != last.UsedAfter || group.Sum(m => m.CarriedDelta) != last.CarriedAfter)
                    throw new InvalidOperationException("Ledger running totals do not reconcile.");
                foreach (var reverse in group.Where(m => m.ReversesMovementId.HasValue))
                {
                    var original = group.Single(m => m.Id == reverse.ReversesMovementId);
                    if (reverse.AccruedDelta != -original.AccruedDelta || reverse.CarriedDelta != -original.CarriedDelta || reverse.UsedDelta != -original.UsedDelta)
                        throw new InvalidOperationException("Linked reversal is not the exact inverse.");
                }
            }

            db.LeaveTypes.Add(new LeaveType { LeaveTypeCode = manualTypeCode, NameLocal = "Manual adjustment verification",
                NameEn = "Manual adjustment verification", TracksBalance = true, IsActive = true, CreationUser = "HR_VERIFY" });
            stage = "manual balance adjustment workflow";
            await db.SaveChangesAsync();
            var leaveService = new LeaveService(repository, new EmployeeRepository(db));
            var negativeCreateRejected = false;
            try
            {
                await leaveService.GrantOrAdjustBalanceAsync(new GrantLeaveBalanceDto(seed.EmployeeCode, manualTypeCode, 9020,
                    -1m, "CORRECTION", "Negative fixture must not create a balance", "manual-" + token + "-negative"), "HR_VERIFY");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("cannot create a balance", StringComparison.OrdinalIgnoreCase))
            { negativeCreateRejected = true; }
            if (!negativeCreateRejected || await db.LeaveBalances.AnyAsync(b => b.EmployeeCode == seed.EmployeeCode && b.LeaveTypeCode == manualTypeCode))
                throw new InvalidOperationException("Negative manual correction created a missing balance.");

            var grantDto = new GrantLeaveBalanceDto(seed.EmployeeCode, manualTypeCode, 9020, 5m,
                "COMPENSATORY", "Oracle manual balance verification grant", "manual-" + token + "-grant");
            await leaveService.GrantOrAdjustBalanceAsync(grantDto, "HR_VERIFY");
            var manualMovement = await db.Set<LeaveBalanceMovement>().SingleAsync(m => m.Source == "MANUAL:" + grantDto.IdempotencyKey);
            var manualBalance = await db.LeaveBalances.SingleAsync(b => b.Id == manualMovement.BalanceId);
            if (manualMovement.Kind != "COMPENSATORY" || manualMovement.Reason != grantDto.Reason || manualBalance.AccruedDays != 5m)
                throw new InvalidOperationException("Manual grant did not record its adjustment type, reason and balance effect.");

            var pendingRequest = new LeaveRequest { EmployeeCode = seed.EmployeeCode, LeaveTypeCode = manualTypeCode,
                StartDate = manualBalance.CycleStartDate!.Value.AddDays(10), EndDate = manualBalance.CycleStartDate.Value.AddDays(11),
                DaysRequested = 2m, Status = "PENDING", CreationUser = "HR_VERIFY" };
            db.LeaveRequests.Add(pendingRequest);
            await db.SaveChangesAsync();
            var pendingReductionRejected = false;
            try
            {
                await leaveService.GrantOrAdjustBalanceAsync(new GrantLeaveBalanceDto(seed.EmployeeCode, manualTypeCode, 9020,
                    -4m, "CORRECTION", "Cannot invalidate pending reservation", "manual-" + token + "-pending"), "HR_VERIFY");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("pending requests", StringComparison.OrdinalIgnoreCase))
            { pendingReductionRejected = true; }
            if (!pendingReductionRejected || manualBalance.AccruedDays != 5m || await db.Set<LeaveBalanceMovement>().AnyAsync(m => m.Source == "MANUAL:manual-" + token + "-pending"))
                throw new InvalidOperationException("Manual correction invalidated a pending leave reservation.");

            db.LeaveRequests.Remove(pendingRequest);
            await db.SaveChangesAsync();
            var reversalDto = new ReverseLeaveBalanceMovementDto("Oracle verification reversal");
            await leaveService.ReverseManualBalanceMovementAsync(seed.EmployeeCode, manualMovement.Id, reversalDto, "HR_VERIFY");
            var replay = await leaveService.ReverseManualBalanceMovementAsync(seed.EmployeeCode, manualMovement.Id, reversalDto, "HR_VERIFY");
            var manualReversal = await db.Set<LeaveBalanceMovement>().SingleAsync(m => m.ReversesMovementId == manualMovement.Id);
            if (manualBalance.AccruedDays != 0m || manualReversal.Kind != "REVERSAL" || manualReversal.AccruedDelta != -manualMovement.AccruedDelta ||
                replay.AccruedDays != 0m)
                throw new InvalidOperationException("Manual adjustment reversal failed to restore the balance or replay idempotently.");
            var changedReasonRejected = false;
            try
            {
                await leaveService.ReverseManualBalanceMovementAsync(seed.EmployeeCode, manualMovement.Id,
                    new ReverseLeaveBalanceMovementDto("Changed retry reason"), "HR_VERIFY");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("different reason", StringComparison.OrdinalIgnoreCase))
            { changedReasonRejected = true; }
            if (!changedReasonRejected) throw new InvalidOperationException("Manual reversal accepted a changed retry reason.");

            db.HrScopeEnabled = true;
            db.HrScopeAllBranches = true;
            db.HrScopeEmployeeCode = "__LEDGER_TEST_NO_EMPLOYEE__";
            if (await db.Set<LeaveBalanceMovement>().AnyAsync()) throw new InvalidOperationException("Ledger leaked through self-service scope.");
            db.HrScopeEnabled = false;
            db.HrScopeEmployeeCode = null;
            report.Add("Oracle ledger: grant/usage, transfer+expiry, idempotent post/unpost, dependent close reversal, repost, five linked reversals and running totals passed; manual adjustment reason/type, negative creation guard, pending reservation guard, linked reversal, idempotent retry and changed-reason rejection passed; self-service isolation and SQL UPDATE/DELETE protection passed.");
        }
        catch (OracleException ex)
        {
            report.Add($"Oracle ledger verifier failed during '{stage}': ORA-{ex.Number:D5}: {ex.Message}");
            throw;
        }
        finally { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); }
        if (await db.LeaveBalances.AnyAsync(b => b.Id == balanceId || b.Id == targetId) ||
            await db.Set<LeaveBalanceMovement>().AnyAsync(m => m.BalanceId == balanceId || m.BalanceId == targetId) ||
            await db.LeaveYearCloses.AnyAsync(c => c.Id == closeId || c.Id == dependentCloseId) ||
            await db.LeaveTypes.AnyAsync(t => t.LeaveTypeCode == manualTypeCode))
            throw new InvalidOperationException("Ledger-test rollback failed.");
        report.Add("Oracle ledger rollback verified: no test balances, movements or close retained. Concurrent sessions not tested.");
    }
}
