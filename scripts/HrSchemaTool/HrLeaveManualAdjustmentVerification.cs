using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrLeaveManualAdjustmentVerification
{
    public static async Task Verify(OracleConnection connection, string connectionString, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var token = Guid.NewGuid().ToString("N");
        var typeCode = "VERIFY_MANUAL_" + token[..12].ToUpperInvariant();
        var employee = await db.Employees.AsNoTracking().OrderBy(e => e.EmployeeCode).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("A DEV_TEMPLATE employee is required for the rollback-only manual adjustment verification.");
        try
        {
            db.LeaveTypes.Add(new LeaveType { LeaveTypeCode = typeCode, NameLocal = "Manual balance verification",
                NameEn = "Manual balance verification", TracksBalance = true, IsActive = true, CreationUser = "HR_VERIFY" });
            await db.SaveChangesAsync();

            var repository = new LeaveRepository(db);
            var service = new LeaveService(repository, new EmployeeRepository(db));
            var oversizedRejected = false;
            try
            {
                await service.GrantOrAdjustBalanceAsync(new GrantLeaveBalanceDto(employee.EmployeeCode, typeCode, 9020,
                    100_000_000m, "MANUAL_GRANT", "Storage bound verification", "manual-" + token + "-large"), "HR_VERIFY");
            }
            catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "Days") { oversizedRejected = true; }
            if (!oversizedRejected) throw new InvalidOperationException("Manual grant exceeded NUMBER(12,4) storage precision.");

            var negativeCreateRejected = false;
            try
            {
                await service.GrantOrAdjustBalanceAsync(new GrantLeaveBalanceDto(employee.EmployeeCode, typeCode, 9020,
                    -1m, "CORRECTION", "Negative fixture must not create a balance", "manual-" + token + "-negative"), "HR_VERIFY");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("cannot create a balance", StringComparison.OrdinalIgnoreCase))
            { negativeCreateRejected = true; }
            if (!negativeCreateRejected || await db.LeaveBalances.AnyAsync(b => b.EmployeeCode == employee.EmployeeCode && b.LeaveTypeCode == typeCode))
                throw new InvalidOperationException("Negative manual correction created a missing balance.");

            var grantDto = new GrantLeaveBalanceDto(employee.EmployeeCode, typeCode, 9020, 5m,
                "COMPENSATORY", "Oracle manual balance verification grant", "manual-" + token + "-grant");
            await service.GrantOrAdjustBalanceAsync(grantDto, "HR_VERIFY");
            var original = await db.Set<LeaveBalanceMovement>().SingleAsync(m => m.Source == "MANUAL:" + grantDto.IdempotencyKey);
            var balance = await db.LeaveBalances.SingleAsync(b => b.Id == original.BalanceId);
            if (original.Kind != "COMPENSATORY" || original.Reason != grantDto.Reason || balance.AccruedDays != 5m)
                throw new InvalidOperationException("Manual grant did not record its adjustment type, reason and balance effect.");

            var pendingRequest = new LeaveRequest { EmployeeCode = employee.EmployeeCode, LeaveTypeCode = typeCode,
                StartDate = balance.CycleStartDate!.Value.AddDays(10), EndDate = balance.CycleStartDate.Value.AddDays(11),
                DaysRequested = 2m, Status = "PENDING", CreationUser = "HR_VERIFY" };
            db.LeaveRequests.Add(pendingRequest);
            await db.SaveChangesAsync();
            var pendingReductionRejected = false;
            try
            {
                await service.GrantOrAdjustBalanceAsync(new GrantLeaveBalanceDto(employee.EmployeeCode, typeCode, 9020,
                    -4m, "CORRECTION", "Cannot invalidate pending reservation", "manual-" + token + "-pending"), "HR_VERIFY");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("pending requests", StringComparison.OrdinalIgnoreCase))
            { pendingReductionRejected = true; }
            if (!pendingReductionRejected || balance.AccruedDays != 5m ||
                await db.Set<LeaveBalanceMovement>().AnyAsync(m => m.Source == "MANUAL:manual-" + token + "-pending"))
                throw new InvalidOperationException("Manual correction invalidated a pending leave reservation.");

            db.LeaveRequests.Remove(pendingRequest);
            await db.SaveChangesAsync();
            var reversalDto = new ReverseLeaveBalanceMovementDto("Oracle verification reversal");
            await service.ReverseManualBalanceMovementAsync(employee.EmployeeCode, original.Id, reversalDto, "HR_VERIFY");
            var replay = await service.ReverseManualBalanceMovementAsync(employee.EmployeeCode, original.Id, reversalDto, "HR_VERIFY");
            var reversal = await db.Set<LeaveBalanceMovement>().SingleAsync(m => m.ReversesMovementId == original.Id);
            if (balance.AccruedDays != 0m || reversal.Kind != "REVERSAL" || reversal.AccruedDelta != -original.AccruedDelta || replay.AccruedDays != 0m)
                throw new InvalidOperationException("Manual adjustment reversal failed to restore the balance or replay idempotently.");

            var changedReasonRejected = false;
            try
            {
                await service.ReverseManualBalanceMovementAsync(employee.EmployeeCode, original.Id,
                    new ReverseLeaveBalanceMovementDto("Changed retry reason"), "HR_VERIFY");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("different reason", StringComparison.OrdinalIgnoreCase))
            { changedReasonRejected = true; }
            if (!changedReasonRejected) throw new InvalidOperationException("Manual reversal accepted a changed retry reason.");

            report.Add("Oracle manual leave balance: NUMBER(12,4) bound, negative-create rejection, reason/type ledger entry, pending-reservation protection, linked reversal, idempotent replay and changed-reason rejection passed in rollback-only transaction.");
        }
        finally
        {
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
        }

        if (await db.LeaveTypes.AnyAsync(t => t.LeaveTypeCode == typeCode) ||
            await db.LeaveBalances.AnyAsync(b => b.EmployeeCode == employee.EmployeeCode && b.LeaveTypeCode == typeCode) ||
            await db.Set<LeaveBalanceMovement>().AnyAsync(m => m.EmployeeCode == employee.EmployeeCode && m.LeaveTypeCode == typeCode) ||
            await db.LeaveRequests.AnyAsync(r => r.EmployeeCode == employee.EmployeeCode && r.LeaveTypeCode == typeCode))
            throw new InvalidOperationException("Manual balance verification rollback left fixture rows in DEV_TEMPLATE.");
        report.Add("Oracle manual leave-balance rollback verified: no synthetic leave type, balance, movement or request retained.");

        await VerifyConcurrentRetryAsync(connectionString, report);
    }

    private static async Task VerifyConcurrentRetryAsync(string connectionString, List<string> report)
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
        var token = Guid.NewGuid().ToString("N");
        var typeCode = "VERIFY_RACE_" + token[..12].ToUpperInvariant();
        var employeeCode = await dbOne.Employees.AsNoTracking().OrderBy(e => e.EmployeeCode).Select(e => e.EmployeeCode).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Concurrent manual adjustment verification requires an existing DEV_TEMPLATE employee.");
        dbOne.LeaveTypes.Add(new LeaveType { LeaveTypeCode = typeCode, NameLocal = "Manual concurrency verification",
            NameEn = "Manual concurrency verification", TracksBalance = true, IsActive = true, CreationUser = "HR_VERIFY" });
        await dbOne.SaveChangesAsync(); // fixture definition is committed separately so both sessions can resolve it.

        await using var transactionOne = await dbOne.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await using var transactionTwo = await dbTwo.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var dto = new GrantLeaveBalanceDto(employeeCode, typeCode, 9021, 3.5m,
                "MANUAL_GRANT", "Concurrent idempotent grant", "race-" + token);
            var serviceOne = new LeaveService(new LeaveRepository(dbOne), new EmployeeRepository(dbOne));
            var serviceTwo = new LeaveService(new LeaveRepository(dbTwo), new EmployeeRepository(dbTwo));
            await serviceOne.GrantOrAdjustBalanceAsync(dto, "HR_VERIFY");

            var second = serviceTwo.GrantOrAdjustBalanceAsync(dto, "HR_VERIFY");
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            var secondWaitedForEmployeeLock = !second.IsCompleted;
            await transactionOne.RollbackAsync();
            var replay = await second.WaitAsync(TimeSpan.FromSeconds(15));
            var savedMovementCount = await dbTwo.Set<LeaveBalanceMovement>().CountAsync(m => m.Source == "MANUAL:" + dto.IdempotencyKey);
            var savedBalance = await dbTwo.LeaveBalances.SingleOrDefaultAsync(b => b.EmployeeCode == employeeCode && b.LeaveTypeCode == typeCode);
            if (!secondWaitedForEmployeeLock || savedMovementCount != 1 || savedBalance?.AccruedDays != 3.5m || replay.AccruedDays != 3.5m)
                throw new InvalidOperationException("Concurrent manual adjustment did not serialize and apply exactly once after the first session rolled back.");

            await transactionTwo.RollbackAsync();
        }
        finally
        {
            try { await transactionOne.RollbackAsync(); } catch { }
            try { await transactionTwo.RollbackAsync(); } catch { }
            await transactionOne.DisposeAsync();
            await transactionTwo.DisposeAsync();
            await using var cleanupConnection = new OracleConnection(safeConnectionString);
            await cleanupConnection.OpenAsync();
            await SetTemplateSchemaAsync(cleanupConnection);
            await using var cleanup = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(cleanupConnection,
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
            if (await cleanup.LeaveBalances.AnyAsync(b => b.EmployeeCode == employeeCode && b.LeaveTypeCode == typeCode) ||
                await cleanup.Set<LeaveBalanceMovement>().AnyAsync(m => m.EmployeeCode == employeeCode && m.LeaveTypeCode == typeCode))
                throw new InvalidOperationException("Concurrent manual adjustment rollback left balance or immutable movements behind.");
            var fixtureType = await cleanup.LeaveTypes.SingleOrDefaultAsync(t => t.LeaveTypeCode == typeCode);
            if (fixtureType != null)
            {
                cleanup.LeaveTypes.Remove(fixtureType);
                await cleanup.SaveChangesAsync();
            }
        }
        report.Add("Oracle concurrent manual balance retry: independent session waited on employee lock, applied one ledger movement after first rollback, and both rollback/cleanup checks passed.");
        report.Add("Oracle concurrent manual balance cleanup verified: no test balance/movement remains and the synthetic leave type was removed.");
    }

    private static async Task SetTemplateSchemaAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA = DEV_TEMPLATE";
        await command.ExecuteNonQueryAsync();
    }
}
