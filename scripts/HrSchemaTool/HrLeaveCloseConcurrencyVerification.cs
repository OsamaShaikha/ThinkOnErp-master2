using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrLeaveCloseConcurrencyVerification
{
    private const int FromYear = 9997;
    private const int ToYear = 9998;
    private const int OverlapFromYear = 9995;
    private const int OverlapToYear = 9996;

    public static async Task Verify(string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        await using var connectionOne = new OracleConnection(safeConnectionString);
        await using var connectionTwo = new OracleConnection(safeConnectionString);
        await connectionOne.OpenAsync();
        await connectionTwo.OpenAsync();
        await SetTemplateSchemaAsync(connectionOne);
        await SetTemplateSchemaAsync(connectionTwo);

        var leaveTypeCode = Convert.ToString(await ReadOneAsync(connectionOne, "SELECT LEAVE_TYPE_CODE FROM HR_LEAVE_TYPE WHERE ROWNUM = 1"));
        if (string.IsNullOrWhiteSpace(leaveTypeCode))
            throw new InvalidOperationException("Leave-close concurrency verification requires a DEV_TEMPLATE leave type.");
        var closeKey = $"{FromYear}:{ToYear}:{leaveTypeCode}";
        var existingCount = Convert.ToInt32(await ReadOneAsync(connectionOne,
            "SELECT COUNT(*) FROM HR_LEAVE_YEAR_CLOSE WHERE CLOSE_KEY = :closeKey", ("closeKey", closeKey)));
        if (existingCount != 0)
            throw new InvalidOperationException($"Reserved leave-close fixture '{closeKey}' is already occupied.");

        var optionsOne = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connectionOne,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
        var optionsTwo = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connectionTwo,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
        await using var dbOne = new OracleDbContext(optionsOne);
        await using var dbTwo = new OracleDbContext(optionsTwo);
        await using var transactionOne = await dbOne.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await using var transactionTwo = await dbTwo.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

        var first = new LeaveYearClose
        {
            CloseKey = closeKey, FromYear = FromYear, ToYear = ToYear, LeaveTypeCode = leaveTypeCode,
            Status = "DRAFT", CreationUser = "HR_CLOSE_CONCURRENCY_TEST"
        };
        var second = new LeaveYearClose
        {
            CloseKey = closeKey, FromYear = FromYear, ToYear = ToYear, LeaveTypeCode = leaveTypeCode,
            Status = "DRAFT", CreationUser = "HR_CLOSE_CONCURRENCY_TEST"
        };

        try
        {
            var repositoryOne = new LeaveRepository(dbOne);
            var repositoryTwo = new LeaveRepository(dbTwo);
            var firstSaved = await repositoryOne.SaveLeaveYearCloseDraftAsync(first);
            var secondTask = repositoryTwo.SaveLeaveYearCloseDraftAsync(second);
            await Task.Delay(TimeSpan.FromMilliseconds(350));
            var secondEnteredBeforeRelease = secondTask.IsCompleted;

            await transactionOne.RollbackAsync();
            var secondSaved = await secondTask.WaitAsync(TimeSpan.FromSeconds(10));
            await transactionTwo.RollbackAsync();

            if (secondEnteredBeforeRelease)
                throw new InvalidOperationException("A second concurrent leave-close draft bypassed the locked leave-type serialization row.");
            if (firstSaved.Id <= 0 || secondSaved.Id <= 0)
                throw new InvalidOperationException("Concurrent leave-close draft saves did not return persisted identities.");

            var remainingCount = Convert.ToInt32(await ReadOneAsync(connectionOne,
                "SELECT COUNT(*) FROM HR_LEAVE_YEAR_CLOSE WHERE CLOSE_KEY = :closeKey", ("closeKey", closeKey)));
            if (remainingCount != 0)
                throw new InvalidOperationException("The rolled-back leave-close concurrency fixture remained in DEV_TEMPLATE.");

            report.Add("Oracle LEV-18 concurrent first draft: two independent sessions serialized on the leave-type row; first rollback allowed the second to create the same close key; both transactions rolled back and fixture count returned to zero.");
        }
        finally
        {
            try { await transactionOne.RollbackAsync(); } catch { }
            try { await transactionTwo.RollbackAsync(); } catch { }
        }

        await VerifyWildcardAndSpecificPostConcurrencyAsync(safeConnectionString, leaveTypeCode, report);
        await VerifyPolicyChangeRejectedAsync(safeConnectionString, report);
        await VerifyPostUnpostWorkflowAsync(safeConnectionString, report);
        await VerifyConcurrentPostAfterRollbackAsync(safeConnectionString, report);
        await VerifyPostFailureAtomicityAsync(safeConnectionString, report);
    }

    private static async Task VerifyPostFailureAtomicityAsync(string connectionString, List<string> report)
    {
        long closeId = 0;
        long sourceId = 0;
        long? targetId = null;
        string? closeKey = null;
        string? closeSource = null;
        var sourceBefore = (Accrued: 0m, Used: 0m, Carried: 0m);
        var targetBefore = (Accrued: 0m, Used: 0m, Carried: 0m);
        await using (var seedConnection = new OracleConnection(connectionString))
        {
            await seedConnection.OpenAsync();
            await SetTemplateSchemaAsync(seedConnection);
            await using var seedDb = CreateContext(seedConnection);
            await using var seedTransaction = await seedDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var candidates = await seedDb.LeaveBalances.Include(balance => balance.LeaveType)
                    .Where(balance => balance.LeaveType!.TracksBalance)
                    .OrderBy(balance => balance.EmployeeCode).ThenBy(balance => balance.LeaveTypeCode).ThenBy(balance => balance.YearNo)
                    .ToListAsync();
                LeaveYearClose? fixture = null;
                foreach (var source in candidates)
                {
                    var policy = source.LeaveType!;
                    var sourceStart = (source.CycleStartDate ?? new DateTime(source.YearNo, 1, 1)).Date;
                    var sourceEnd = (source.CycleEndDate ?? new DateTime(source.YearNo, 12, 31)).Date;
                    var targetStart = sourceEnd.AddDays(1);
                    var targetEnd = targetStart.AddYears(1).AddDays(-1);
                    var unused = source.AccruedDays + source.CarriedForwardDays - source.UsedDays;
                    if (unused < 0m) continue;
                    var action = policy.YearEndExcessAction.Trim().ToUpperInvariant();
                    if (action is not ("EXPIRE" or "REQUIRE_USE" or "CASHOUT")) continue;
                    var carried = policy.CarryForwardAllowed
                        ? Math.Min(unused, policy.CarryForwardCapDays > 0m ? policy.CarryForwardCapDays : unused)
                        : 0m;
                    var excess = unused - carried;
                    if (action == "REQUIRE_USE" && excess > 0m || action == "CASHOUT" && excess > 0m) continue;
                    if (await seedDb.LeaveRequests.AnyAsync(request => request.EmployeeCode == source.EmployeeCode &&
                        request.LeaveTypeCode == source.LeaveTypeCode && request.Status == "PENDING" &&
                        request.StartDate <= sourceEnd && request.EndDate >= sourceStart)) continue;
                    if (await seedDb.LeaveYearCloses.AsNoTracking().AnyAsync(close => close.Status == "POSTED" &&
                        close.FromYear == source.YearNo && (close.LeaveTypeCode == "*" || close.LeaveTypeCode == source.LeaveTypeCode))) continue;

                    closeKey = $"VERIFY_POST_FAILURE_{Guid.NewGuid():N}";
                    sourceId = source.Id;
                    sourceBefore = (source.AccruedDays, source.UsedDays, source.CarriedForwardDays);
                    var target = await seedDb.LeaveBalances.FirstOrDefaultAsync(balance => balance.EmployeeCode == source.EmployeeCode &&
                        balance.LeaveTypeCode == source.LeaveTypeCode && balance.CycleStartDate == targetStart);
                    targetId = target?.Id;
                    targetBefore = (target?.AccruedDays ?? 0m, target?.UsedDays ?? 0m, target?.CarriedForwardDays ?? 0m);
                    fixture = new LeaveYearClose
                    {
                        CloseKey = closeKey,
                        FromYear = source.YearNo,
                        ToYear = targetStart.Year,
                        LeaveTypeCode = source.LeaveTypeCode,
                        Status = "DRAFT",
                        CreationUser = "HR_CLOSE_FAILURE_TEST",
                        Lines =
                        [
                            new LeaveYearCloseLine
                            {
                                EmployeeCode = source.EmployeeCode,
                                LeaveTypeCode = source.LeaveTypeCode,
                                SourceYear = source.YearNo,
                                TargetYear = targetStart.Year,
                                SourceBalanceId = source.Id,
                                TargetBalanceId = target?.Id,
                                SourceCycleStartDate = sourceStart,
                                SourceCycleEndDate = sourceEnd,
                                TargetCycleStartDate = targetStart,
                                TargetCycleEndDate = targetEnd,
                                SourceAccruedBefore = source.AccruedDays,
                                SourceUsedDays = source.UsedDays,
                                SourceCarriedBefore = source.CarriedForwardDays,
                                TargetAccruedBefore = targetBefore.Accrued,
                                TargetUsedBefore = targetBefore.Used,
                                TargetCarriedBefore = targetBefore.Carried,
                                UnusedDays = unused,
                                CarriedDays = carried,
                                ExpiredDays = action == "EXPIRE" ? excess : 0m,
                                CashoutDays = 0m,
                                TracksBalanceSnapshot = policy.TracksBalance,
                                CarryForwardAllowedSnapshot = policy.CarryForwardAllowed,
                                CarryForwardCapDaysSnapshot = policy.CarryForwardCapDays,
                                YearEndExcessActionSnapshot = action
                            }
                        ]
                    };
                    seedDb.LeaveYearCloses.Add(fixture);
                    await seedDb.SaveChangesAsync();
                    closeId = fixture.Id;
                    closeSource = $"YEAR_CLOSE:{closeId}";
                    break;
                }
                if (fixture == null || closeId <= 0 || closeKey == null || closeSource == null)
                    throw new InvalidOperationException("No eligible tracked leave balance can safely exercise post failure atomicity.");
                await seedTransaction.CommitAsync();
            }
            catch
            {
                await seedTransaction.RollbackAsync();
                throw;
            }
        }

        var safe = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        try
        {
            await using (var failingConnection = new OracleConnection(safe))
            {
                await failingConnection.OpenAsync();
                await SetTemplateSchemaAsync(failingConnection);
                var options = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(failingConnection,
                    o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
                    .AddInterceptors(new ThrowOnSecondSaveChangesInterceptor()).Options;
                await using var failingDb = new OracleDbContext(options);
                var injectedFailureObserved = false;
                try
                {
                    await new LeaveRepository(failingDb).PostLeaveYearCloseAsync(closeId, "HR_CLOSE_FAILURE_TEST");
                }
                catch (InvalidOperationException ex) when (ex.Message == ThrowOnSecondSaveChangesInterceptor.FailureMessage)
                {
                    injectedFailureObserved = true;
                }
                if (!injectedFailureObserved)
                    throw new InvalidOperationException("The Oracle post failure fixture did not fail at its second SaveChanges call.");
            }

            await using (var checkConnection = new OracleConnection(safe))
            {
                await checkConnection.OpenAsync();
                await SetTemplateSchemaAsync(checkConnection);
                await using var checkDb = CreateContext(checkConnection);
                var draft = await checkDb.LeaveYearCloses.AsNoTracking().SingleAsync(close => close.Id == closeId);
                var source = await checkDb.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == sourceId);
                var target = targetId.HasValue
                    ? await checkDb.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == targetId.Value)
                    : null;
                if (draft.Status != "DRAFT" || source.AccruedDays != sourceBefore.Accrued || source.UsedDays != sourceBefore.Used ||
                    source.CarriedForwardDays != sourceBefore.Carried || target != null && (target.AccruedDays != targetBefore.Accrued ||
                    target.UsedDays != targetBefore.Used || target.CarriedForwardDays != targetBefore.Carried) ||
                    await checkDb.Set<LeaveBalanceMovement>().AsNoTracking().AnyAsync(movement => movement.Source == closeSource))
                    throw new InvalidOperationException("Injected post failure did not atomically restore the DRAFT, source/target balances, and ledger.");

                await using var retryTransaction = await checkDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                try
                {
                    var retried = await new LeaveRepository(checkDb).PostLeaveYearCloseAsync(closeId, "HR_CLOSE_FAILURE_TEST");
                    if (retried?.Status != "POSTED") throw new InvalidOperationException("Oracle close retry did not post after the injected rollback.");
                    var movementCount = await checkDb.Set<LeaveBalanceMovement>().AsNoTracking()
                        .CountAsync(movement => movement.Source == closeSource);
                    if (movementCount < 1) throw new InvalidOperationException("Oracle retry did not record the close movement.");
                    await retryTransaction.RollbackAsync();
                }
                catch
                {
                    await retryTransaction.RollbackAsync();
                    throw;
                }
            }

            await using var verifyConnection = new OracleConnection(safe);
            await verifyConnection.OpenAsync();
            await SetTemplateSchemaAsync(verifyConnection);
            await using var verifyDb = CreateContext(verifyConnection);
            var finalClose = await verifyDb.LeaveYearCloses.AsNoTracking().SingleAsync(close => close.Id == closeId);
            var finalSource = await verifyDb.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == sourceId);
            if (finalClose.Status != "DRAFT" || finalSource.AccruedDays != sourceBefore.Accrued ||
                finalSource.UsedDays != sourceBefore.Used || finalSource.CarriedForwardDays != sourceBefore.Carried ||
                await verifyDb.Set<LeaveBalanceMovement>().AsNoTracking().AnyAsync(movement => movement.Source == closeSource))
                throw new InvalidOperationException("Oracle retry verification left a posted fixture, altered balance, or ledger movement behind.");

            await using var cleanupTransaction = await verifyConnection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await using var cleanup = verifyConnection.CreateCommand();
            cleanup.Transaction = (OracleTransaction)cleanupTransaction;
            cleanup.CommandText = "DELETE FROM HR_LEAVE_YEAR_CLOSE WHERE ID = :recordId AND STATUS = 'DRAFT'";
            cleanup.Parameters.Add("recordId", OracleDbType.Int64, closeId, System.Data.ParameterDirection.Input);
            await cleanup.ExecuteNonQueryAsync();
            await cleanupTransaction.CommitAsync();
            report.Add("Oracle LEV-18 failure atomicity: injected failure on the second SaveChanges (after balance/ledger flush) rolled back status, balances, and movements; retry posted in a rollback-only transaction; final DRAFT fixture was deleted.");
        }
        finally
        {
            try
            {
                await using var cleanupConnection = new OracleConnection(safe);
                await cleanupConnection.OpenAsync();
                await SetTemplateSchemaAsync(cleanupConnection);
                await using var cleanupTransaction = await cleanupConnection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                await using var cleanup = cleanupConnection.CreateCommand();
                cleanup.Transaction = (OracleTransaction)cleanupTransaction;
                cleanup.CommandText = "DELETE FROM HR_LEAVE_YEAR_CLOSE WHERE ID = :recordId AND STATUS = 'DRAFT'";
                cleanup.Parameters.Add("recordId", OracleDbType.Int64, closeId, System.Data.ParameterDirection.Input);
                await cleanup.ExecuteNonQueryAsync();
                await cleanupTransaction.CommitAsync();
            }
            catch
            {
                throw new InvalidOperationException($"Could not remove DEV_TEMPLATE post failure fixture {closeId}; inspect only that reserved HR close row.");
            }
        }
    }

    private static async Task VerifyConcurrentPostAfterRollbackAsync(string connectionString, List<string> report)
    {
        long closeId = 0;
        long sourceId = 0;
        string? closeKey = null;
        string? closeSource = null;
        var sourceBefore = (Accrued: 0m, Used: 0m, Carried: 0m);
        await using (var seedConnection = new OracleConnection(connectionString))
        {
            await seedConnection.OpenAsync();
            await SetTemplateSchemaAsync(seedConnection);
            await using var seedDb = CreateContext(seedConnection);
            await using var seedTransaction = await seedDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var candidates = await seedDb.LeaveBalances.Include(balance => balance.LeaveType)
                    .Where(balance => balance.LeaveType!.TracksBalance)
                    .OrderBy(balance => balance.EmployeeCode).ThenBy(balance => balance.LeaveTypeCode).ThenBy(balance => balance.YearNo)
                    .ToListAsync();
                LeaveYearClose? fixture = null;
                foreach (var source in candidates)
                {
                    var policy = source.LeaveType!;
                    var sourceStart = (source.CycleStartDate ?? new DateTime(source.YearNo, 1, 1)).Date;
                    var sourceEnd = (source.CycleEndDate ?? new DateTime(source.YearNo, 12, 31)).Date;
                    var targetStart = sourceEnd.AddDays(1);
                    var targetEnd = targetStart.AddYears(1).AddDays(-1);
                    var unused = source.AccruedDays + source.CarriedForwardDays - source.UsedDays;
                    if (unused < 0m) continue;
                    var action = policy.YearEndExcessAction.Trim().ToUpperInvariant();
                    if (action is not ("EXPIRE" or "REQUIRE_USE" or "CASHOUT")) continue;
                    var carried = policy.CarryForwardAllowed
                        ? Math.Min(unused, policy.CarryForwardCapDays > 0m ? policy.CarryForwardCapDays : unused)
                        : 0m;
                    var excess = unused - carried;
                    if (action == "REQUIRE_USE" && excess > 0m || action == "CASHOUT" && excess > 0m) continue;
                    if (await seedDb.LeaveRequests.AnyAsync(request => request.EmployeeCode == source.EmployeeCode &&
                        request.LeaveTypeCode == source.LeaveTypeCode && request.Status == "PENDING" &&
                        request.StartDate <= sourceEnd && request.EndDate >= sourceStart)) continue;
                    if (await seedDb.LeaveYearCloses.AsNoTracking().AnyAsync(close => close.Status == "POSTED" &&
                        close.FromYear == source.YearNo && (close.LeaveTypeCode == "*" || close.LeaveTypeCode == source.LeaveTypeCode))) continue;

                    closeKey = $"VERIFY_PARALLEL_POST_{Guid.NewGuid():N}";
                    sourceId = source.Id;
                    sourceBefore = (source.AccruedDays, source.UsedDays, source.CarriedForwardDays);
                    var target = await seedDb.LeaveBalances.FirstOrDefaultAsync(balance => balance.EmployeeCode == source.EmployeeCode &&
                        balance.LeaveTypeCode == source.LeaveTypeCode && balance.CycleStartDate == targetStart);
                    fixture = new LeaveYearClose
                    {
                        CloseKey = closeKey,
                        FromYear = source.YearNo,
                        ToYear = targetStart.Year,
                        LeaveTypeCode = source.LeaveTypeCode,
                        Status = "DRAFT",
                        CreationUser = "HR_CLOSE_PARALLEL_POST_TEST",
                        Lines =
                        [
                            new LeaveYearCloseLine
                            {
                                EmployeeCode = source.EmployeeCode,
                                LeaveTypeCode = source.LeaveTypeCode,
                                SourceYear = source.YearNo,
                                TargetYear = targetStart.Year,
                                SourceBalanceId = source.Id,
                                TargetBalanceId = target?.Id,
                                SourceCycleStartDate = sourceStart,
                                SourceCycleEndDate = sourceEnd,
                                TargetCycleStartDate = targetStart,
                                TargetCycleEndDate = targetEnd,
                                SourceAccruedBefore = source.AccruedDays,
                                SourceUsedDays = source.UsedDays,
                                SourceCarriedBefore = source.CarriedForwardDays,
                                TargetAccruedBefore = target?.AccruedDays ?? 0m,
                                TargetUsedBefore = target?.UsedDays ?? 0m,
                                TargetCarriedBefore = target?.CarriedForwardDays ?? 0m,
                                UnusedDays = unused,
                                CarriedDays = carried,
                                ExpiredDays = action == "EXPIRE" ? excess : 0m,
                                CashoutDays = 0m,
                                TracksBalanceSnapshot = policy.TracksBalance,
                                CarryForwardAllowedSnapshot = policy.CarryForwardAllowed,
                                CarryForwardCapDaysSnapshot = policy.CarryForwardCapDays,
                                YearEndExcessActionSnapshot = action
                            }
                        ]
                    };
                    seedDb.LeaveYearCloses.Add(fixture);
                    await seedDb.SaveChangesAsync();
                    closeId = fixture.Id;
                    closeSource = $"YEAR_CLOSE:{closeId}";
                    break;
                }

                if (fixture == null || closeId <= 0 || closeKey == null || closeSource == null)
                    throw new InvalidOperationException("No eligible tracked leave balance can safely exercise concurrent post serialization.");
                await seedTransaction.CommitAsync();
            }
            catch
            {
                await seedTransaction.RollbackAsync();
                throw;
            }
        }

        var safe = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        await using var connectionOne = new OracleConnection(safe);
        await using var connectionTwo = new OracleConnection(safe);
        await connectionOne.OpenAsync();
        await connectionTwo.OpenAsync();
        await SetTemplateSchemaAsync(connectionOne);
        await SetTemplateSchemaAsync(connectionTwo);
        await using var dbOne = CreateContext(connectionOne);
        await using var dbTwo = CreateContext(connectionTwo);
        await using var transactionOne = await dbOne.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await using var transactionTwo = await dbTwo.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var first = await new LeaveRepository(dbOne).PostLeaveYearCloseAsync(closeId, "HR_CLOSE_PARALLEL_POST_TEST")
                ?? throw new InvalidOperationException("First concurrent leave-close post could not load its fixture.");
            if (first.Status != "POSTED") throw new InvalidOperationException("First concurrent leave-close post did not reach POSTED.");

            var secondTask = new LeaveRepository(dbTwo).PostLeaveYearCloseAsync(closeId, "HR_CLOSE_PARALLEL_POST_TEST");
            await Task.Delay(TimeSpan.FromMilliseconds(350));
            var secondWaitedForCloseLock = !secondTask.IsCompleted;
            await transactionOne.RollbackAsync();
            var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(10))
                ?? throw new InvalidOperationException("Second concurrent leave-close post could not reload its fixture after the first rollback.");
            if (!secondWaitedForCloseLock || second.Status != "POSTED")
                throw new InvalidOperationException("Concurrent close post did not serialize on the close header or the retry did not post after the first rollback.");

            var postedSource = await dbTwo.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == sourceId);
            if (postedSource.AccruedDays != postedSource.UsedDays || postedSource.CarriedForwardDays != 0m)
                throw new InvalidOperationException("The serialized concurrent post did not apply exactly one source close-out.");
            if (!await dbTwo.Set<LeaveBalanceMovement>().AsNoTracking().AnyAsync(movement => movement.Source == closeSource && movement.Kind == "CLOSE_OUT"))
                throw new InvalidOperationException("The serialized concurrent post did not record its close-out ledger movement.");
            await transactionTwo.RollbackAsync();

            await using var verifyConnection = new OracleConnection(safe);
            await verifyConnection.OpenAsync();
            await SetTemplateSchemaAsync(verifyConnection);
            if (Convert.ToInt32(await ReadOneAsync(verifyConnection,
                    "SELECT COUNT(*) FROM HR_LEAVE_YEAR_CLOSE WHERE ID = :recordId AND STATUS = 'DRAFT'", ("recordId", closeId))) != 1)
                throw new InvalidOperationException("Concurrent leave-close fixture was not restored to DRAFT after both outer transactions rolled back.");
            if (Convert.ToInt32(await ReadOneAsync(verifyConnection, "SELECT COUNT(*) FROM HR_LEAVE_MOVEMENT WHERE SOURCE_REF = :source", ("source", closeSource))) != 0)
                throw new InvalidOperationException("Concurrent leave-close post rollback left ledger movements behind.");
            await using var verifyDb = CreateContext(verifyConnection);
            var restoredSource = await verifyDb.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == sourceId);
            if (restoredSource.AccruedDays != sourceBefore.Accrued || restoredSource.UsedDays != sourceBefore.Used ||
                restoredSource.CarriedForwardDays != sourceBefore.Carried)
                throw new InvalidOperationException("Concurrent leave-close post rollback did not restore the source balance.");

            await using var cleanupTransaction = await verifyConnection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await using var cleanup = verifyConnection.CreateCommand();
            cleanup.Transaction = (OracleTransaction)cleanupTransaction;
            cleanup.CommandText = "DELETE FROM HR_LEAVE_YEAR_CLOSE WHERE ID = :recordId AND STATUS = 'DRAFT'";
            cleanup.Parameters.Add("recordId", OracleDbType.Int64, closeId, System.Data.ParameterDirection.Input);
            await cleanup.ExecuteNonQueryAsync();
            await cleanupTransaction.CommitAsync();
            if (Convert.ToInt32(await ReadOneAsync(verifyConnection, "SELECT COUNT(*) FROM HR_LEAVE_YEAR_CLOSE WHERE ID = :recordId", ("recordId", closeId))) != 0)
                throw new InvalidOperationException("Concurrent leave-close draft fixture was not deleted after rollback verification.");
            report.Add("Oracle LEV-18 concurrent post: two independent sessions serialized on one draft; after the first transaction rolled back, the waiting post applied exactly one balance/ledger change; both transactions rolled back and the DRAFT fixture was deleted.");
        }
        finally
        {
            try { await transactionOne.RollbackAsync(); } catch { }
            try { await transactionTwo.RollbackAsync(); } catch { }
            if (closeId > 0)
            {
                try
                {
                    await using var cleanupConnection = new OracleConnection(safe);
                    await cleanupConnection.OpenAsync();
                    await SetTemplateSchemaAsync(cleanupConnection);
                    await using var cleanupTransaction = await cleanupConnection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                    await using var cleanup = cleanupConnection.CreateCommand();
                    cleanup.Transaction = (OracleTransaction)cleanupTransaction;
                    cleanup.CommandText = "DELETE FROM HR_LEAVE_YEAR_CLOSE WHERE ID = :recordId AND STATUS = 'DRAFT'";
                    cleanup.Parameters.Add("recordId", OracleDbType.Int64, closeId, System.Data.ParameterDirection.Input);
                    await cleanup.ExecuteNonQueryAsync();
                    await cleanupTransaction.CommitAsync();
                }
                catch
                {
                    throw new InvalidOperationException($"Could not remove DEV_TEMPLATE concurrent post fixture {closeId}; inspect only that reserved HR close row.");
                }
            }
        }
    }

    private static async Task VerifyPostUnpostWorkflowAsync(string connectionString, List<string> report)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();
        await SetTemplateSchemaAsync(connection);
        await using var db = CreateContext(connection);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        string? closeKey = null;
        long closeId = 0;
        LeaveBalance? sourceBeforeEntity = null;
        var sourceBefore = (Accrued: 0m, Used: 0m, Carried: 0m);
        var targetBefore = (Exists: false, Id: 0L, Accrued: 0m, Used: 0m, Carried: 0m);
        try
        {
            var candidates = await db.LeaveBalances.Include(balance => balance.LeaveType)
                .Where(balance => balance.LeaveType!.TracksBalance)
                .OrderBy(balance => balance.EmployeeCode).ThenBy(balance => balance.LeaveTypeCode).ThenBy(balance => balance.YearNo)
                .ToListAsync();
            foreach (var candidate in candidates)
            {
                var policy = candidate.LeaveType!;
                var sourceStart = (candidate.CycleStartDate ?? new DateTime(candidate.YearNo, 1, 1)).Date;
                var sourceEnd = (candidate.CycleEndDate ?? new DateTime(candidate.YearNo, 12, 31)).Date;
                var targetStart = sourceEnd.AddDays(1);
                var targetEnd = targetStart.AddYears(1).AddDays(-1);
                var unused = candidate.AccruedDays + candidate.CarriedForwardDays - candidate.UsedDays;
                if (unused < 0m) continue;

                var action = policy.YearEndExcessAction.Trim().ToUpperInvariant();
                if (action is not ("EXPIRE" or "REQUIRE_USE" or "CASHOUT")) continue;
                var carried = policy.CarryForwardAllowed
                    ? Math.Min(unused, policy.CarryForwardCapDays > 0m ? policy.CarryForwardCapDays : unused)
                    : 0m;
                var excess = unused - carried;
                if (action == "REQUIRE_USE" && excess > 0m) continue;
                // A real cashout close has a payroll dependency; this fixture deliberately exercises
                // balance and ledger posting without inventing an approved payroll run.
                if (action == "CASHOUT" && excess > 0m) continue;

                var overlap = await db.LeaveYearCloses.AsNoTracking().AnyAsync(close => close.Status == "POSTED" &&
                    close.FromYear == candidate.YearNo &&
                    (close.LeaveTypeCode == "*" || close.LeaveTypeCode == candidate.LeaveTypeCode));
                if (overlap) continue;

                sourceBeforeEntity = candidate;
                sourceBefore = (candidate.AccruedDays, candidate.UsedDays, candidate.CarriedForwardDays);
                var target = await db.LeaveBalances.FirstOrDefaultAsync(balance => balance.EmployeeCode == candidate.EmployeeCode &&
                    balance.LeaveTypeCode == candidate.LeaveTypeCode && balance.CycleStartDate == targetStart);
                targetBefore = target == null
                    ? (false, 0L, 0m, 0m, 0m)
                    : (true, target.Id, target.AccruedDays, target.UsedDays, target.CarriedForwardDays);

                closeKey = $"VERIFY_POST_UNPOST_{Guid.NewGuid():N}";
                var close = new LeaveYearClose
                {
                    CloseKey = closeKey,
                    FromYear = candidate.YearNo,
                    ToYear = targetStart.Year,
                    LeaveTypeCode = candidate.LeaveTypeCode,
                    Status = "DRAFT",
                    CreationUser = "HR_CLOSE_POST_UNPOST_TEST",
                    Lines =
                    [
                        new LeaveYearCloseLine
                        {
                            EmployeeCode = candidate.EmployeeCode,
                            LeaveTypeCode = candidate.LeaveTypeCode,
                            SourceYear = candidate.YearNo,
                            TargetYear = targetStart.Year,
                            SourceBalanceId = candidate.Id,
                            TargetBalanceId = target?.Id,
                            SourceCycleStartDate = sourceStart,
                            SourceCycleEndDate = sourceEnd,
                            TargetCycleStartDate = targetStart,
                            TargetCycleEndDate = targetEnd,
                            SourceAccruedBefore = sourceBefore.Accrued,
                            SourceUsedDays = sourceBefore.Used,
                            SourceCarriedBefore = sourceBefore.Carried,
                            TargetAccruedBefore = targetBefore.Accrued,
                            TargetUsedBefore = targetBefore.Used,
                            TargetCarriedBefore = targetBefore.Carried,
                            UnusedDays = unused,
                            CarriedDays = carried,
                            ExpiredDays = action == "EXPIRE" ? excess : 0m,
                            CashoutDays = 0m,
                            TracksBalanceSnapshot = policy.TracksBalance,
                            CarryForwardAllowedSnapshot = policy.CarryForwardAllowed,
                            CarryForwardCapDaysSnapshot = policy.CarryForwardCapDays,
                            YearEndExcessActionSnapshot = action
                        }
                    ]
                };
                db.LeaveYearCloses.Add(close);
                await db.SaveChangesAsync();
                closeId = close.Id;
                break;
            }

            if (sourceBeforeEntity == null || closeId <= 0 || closeKey == null)
                throw new InvalidOperationException("No tracked DEV_TEMPLATE leave balance can safely exercise close post/unpost without a cashout or REQUIRE_USE dependency.");

            var repo = new LeaveRepository(db);
            var posted = await repo.PostLeaveYearCloseAsync(closeId, "HR_CLOSE_POST_UNPOST_TEST")
                ?? throw new InvalidOperationException("Oracle leave close fixture disappeared before post.");
            if (posted.Status != "POSTED") throw new InvalidOperationException("Oracle leave close fixture did not post.");

            db.ChangeTracker.Clear();
            var postedLine = await db.LeaveYearCloseLines.AsNoTracking().SingleAsync(line => line.CloseId == closeId);
            var postedSource = await db.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == postedLine.SourceBalanceId);
            var postedTarget = postedLine.TargetBalanceId.HasValue
                ? await db.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == postedLine.TargetBalanceId.Value)
                : null;
            if (postedSource.AccruedDays != sourceBefore.Used || postedSource.UsedDays != sourceBefore.Used || postedSource.CarriedForwardDays != 0m)
                throw new InvalidOperationException("Oracle close post did not apply the source balance close-out snapshot.");
            if (postedTarget != null && (postedTarget.AccruedDays != targetBefore.Accrued || postedTarget.UsedDays != targetBefore.Used ||
                postedTarget.CarriedForwardDays != targetBefore.Carried + postedLine.CarriedDays))
                throw new InvalidOperationException("Oracle close post did not apply the target carry-forward snapshot.");

            var closeSource = $"YEAR_CLOSE:{closeId}";
            var firstMovementCount = await db.Set<LeaveBalanceMovement>().AsNoTracking().CountAsync(movement => movement.Source == closeSource);
            if (firstMovementCount < 1 || !await db.Set<LeaveBalanceMovement>().AsNoTracking().AnyAsync(movement =>
                    movement.Source == closeSource && movement.Kind == "CLOSE_OUT"))
                throw new InvalidOperationException("Oracle close post did not record the close-out ledger movement.");

            var repeatedPost = await repo.PostLeaveYearCloseAsync(closeId, "HR_CLOSE_POST_UNPOST_TEST");
            if (repeatedPost?.Status != "POSTED" || await db.Set<LeaveBalanceMovement>().AsNoTracking()
                    .CountAsync(movement => movement.Source == closeSource) != firstMovementCount)
                throw new InvalidOperationException("Repeating Oracle close post changed the posted close or duplicated ledger movements.");

            var sourceForFailure = await db.LeaveBalances.SingleAsync(balance => balance.Id == postedLine.SourceBalanceId);
            var postedSourceState = (sourceForFailure.AccruedDays, sourceForFailure.UsedDays, sourceForFailure.CarriedForwardDays);
            sourceForFailure.AccruedDays += 1m;
            await db.SaveChangesAsync();
            var staleUnpostRejected = false;
            try
            {
                await repo.UnpostLeaveYearCloseAsync(closeId, "HR_CLOSE_POST_UNPOST_TEST");
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("The source balance has changed since posting", StringComparison.Ordinal))
            {
                staleUnpostRejected = true;
            }
            db.ChangeTracker.Clear();
            var closeAfterRejectedUnpost = await db.LeaveYearCloses.AsNoTracking().SingleAsync(close => close.Id == closeId);
            if (!staleUnpostRejected || closeAfterRejectedUnpost.Status != "POSTED" ||
                await db.Set<LeaveBalanceMovement>().AsNoTracking().CountAsync(movement => movement.Source == closeSource) != firstMovementCount)
                throw new InvalidOperationException("An unpost against a changed source balance did not fail without changing close status or ledger movements.");

            sourceForFailure = await db.LeaveBalances.SingleAsync(balance => balance.Id == postedLine.SourceBalanceId);
            sourceForFailure.AccruedDays = postedSourceState.AccruedDays;
            sourceForFailure.UsedDays = postedSourceState.UsedDays;
            sourceForFailure.CarriedForwardDays = postedSourceState.CarriedForwardDays;
            await db.SaveChangesAsync();

            var unposted = await repo.UnpostLeaveYearCloseAsync(closeId, "HR_CLOSE_POST_UNPOST_TEST");
            if (unposted?.Status != "UNPOSTED") throw new InvalidOperationException("Oracle leave close fixture did not unpost.");
            db.ChangeTracker.Clear();
            var restoredLine = await db.LeaveYearCloseLines.AsNoTracking().SingleAsync(line => line.CloseId == closeId);
            var restoredSource = await db.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == restoredLine.SourceBalanceId);
            if (restoredSource.AccruedDays != sourceBefore.Accrued || restoredSource.UsedDays != sourceBefore.Used ||
                restoredSource.CarriedForwardDays != sourceBefore.Carried)
                throw new InvalidOperationException("Oracle close unpost did not restore the source balance exactly.");
            var restoredTarget = restoredLine.TargetBalanceId.HasValue
                ? await db.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == restoredLine.TargetBalanceId.Value)
                : null;
            if (restoredTarget != null && (restoredTarget.AccruedDays != targetBefore.Accrued || restoredTarget.UsedDays != targetBefore.Used ||
                restoredTarget.CarriedForwardDays != targetBefore.Carried))
                throw new InvalidOperationException("Oracle close unpost did not restore the target balance exactly.");

            var closeMovements = await db.Set<LeaveBalanceMovement>().AsNoTracking().Where(movement => movement.Source == closeSource).ToListAsync();
            var originals = closeMovements.Where(movement => movement.Kind is not ("REVERSAL" or "LEGACY_REVERSAL")).ToList();
            if (originals.Count == 0 || originals.Any(original => !closeMovements.Any(reversal => reversal.ReversesMovementId == original.Id)))
                throw new InvalidOperationException("Oracle close unpost did not append a linked reversal for each original close movement.");
            var unpostedMovementCount = closeMovements.Count;
            var repeatedUnpost = await repo.UnpostLeaveYearCloseAsync(closeId, "HR_CLOSE_POST_UNPOST_TEST");
            if (repeatedUnpost?.Status != "UNPOSTED" || await db.Set<LeaveBalanceMovement>().AsNoTracking()
                    .CountAsync(movement => movement.Source == closeSource) != unpostedMovementCount)
                throw new InvalidOperationException("Repeating Oracle close unpost changed the unposted close or duplicated ledger movements.");

            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            if (await db.LeaveYearCloses.AnyAsync(close => close.CloseKey == closeKey) ||
                await db.Set<LeaveBalanceMovement>().AsNoTracking().AnyAsync(movement => movement.Source == closeSource))
                throw new InvalidOperationException("Oracle post/unpost rollback left a close header or ledger fixture behind.");
            var rollbackSource = await db.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == sourceBeforeEntity.Id);
            if (rollbackSource.AccruedDays != sourceBefore.Accrued || rollbackSource.UsedDays != sourceBefore.Used ||
                rollbackSource.CarriedForwardDays != sourceBefore.Carried)
                throw new InvalidOperationException("Oracle post/unpost rollback did not preserve the pre-verification source balance.");
            if (targetBefore.Exists)
            {
                var rollbackTarget = await db.LeaveBalances.AsNoTracking().SingleAsync(balance => balance.Id == targetBefore.Id);
                if (rollbackTarget.AccruedDays != targetBefore.Accrued || rollbackTarget.UsedDays != targetBefore.Used ||
                    rollbackTarget.CarriedForwardDays != targetBefore.Carried)
                    throw new InvalidOperationException("Oracle post/unpost rollback did not preserve the pre-verification target balance.");
            }

            report.Add("Oracle LEV-18 close workflow: post changed source/target balances and recorded ledger movement(s); duplicate post was idempotent; unpost against a changed source failed without ledger/status changes, then succeeded after the snapshot was restored; unpost restored balances and appended linked reversals; duplicate unpost was idempotent; outer rollback removed the fixture and preserved prior balances.");
        }
        finally
        {
            try { await transaction.RollbackAsync(); } catch { }
        }
    }

    private static async Task VerifyPolicyChangeRejectedAsync(string connectionString, List<string> report)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();
        await SetTemplateSchemaAsync(connection);
        await using var db = CreateContext(connection);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var source = await db.LeaveBalances.Include(balance => balance.LeaveType)
            .FirstOrDefaultAsync(balance => balance.LeaveType!.TracksBalance);
        if (source?.LeaveType == null)
            throw new InvalidOperationException("Leave policy snapshot verification requires a DEV_TEMPLATE balance with a tracked leave type.");

        var policy = source.LeaveType;
        var sourceStart = source.CycleStartDate ?? new DateTime(source.YearNo, 1, 1);
        var sourceEnd = source.CycleEndDate ?? new DateTime(source.YearNo, 12, 31);
        var targetStart = sourceEnd.AddDays(1);
        var targetEnd = targetStart.AddYears(1).AddDays(-1);
        var close = new LeaveYearClose
        {
            CloseKey = $"VERIFY_POLICY_{Guid.NewGuid():N}", FromYear = source.YearNo, ToYear = source.YearNo + 1,
            LeaveTypeCode = source.LeaveTypeCode, Status = "DRAFT", CreationUser = "HR_POLICY_SNAPSHOT_TEST",
            Lines =
            [
                new LeaveYearCloseLine
                {
                    EmployeeCode = source.EmployeeCode, LeaveTypeCode = source.LeaveTypeCode,
                    SourceYear = source.YearNo, TargetYear = source.YearNo + 1, SourceBalanceId = source.Id,
                    SourceCycleStartDate = sourceStart, SourceCycleEndDate = sourceEnd,
                    TargetCycleStartDate = targetStart, TargetCycleEndDate = targetEnd,
                    SourceAccruedBefore = 0m, SourceUsedDays = 0m, SourceCarriedBefore = 0m,
                    TargetAccruedBefore = 0m, TargetCarriedBefore = 0m, TargetUsedBefore = 0m,
                    UnusedDays = 0m, CarriedDays = 0m, ExpiredDays = 0m, CashoutDays = 0m,
                    TracksBalanceSnapshot = policy.TracksBalance,
                    CarryForwardAllowedSnapshot = policy.CarryForwardAllowed,
                    CarryForwardCapDaysSnapshot = policy.CarryForwardCapDays,
                    YearEndExcessActionSnapshot = policy.YearEndExcessAction.Trim().ToUpperInvariant()
                }
            ]
        };
        db.LeaveYearCloses.Add(close);
        await db.SaveChangesAsync();
        var closeId = close.Id;
        var originalCarryForwardAllowed = policy.CarryForwardAllowed;
        policy.CarryForwardAllowed = !policy.CarryForwardAllowed;
        await db.SaveChangesAsync();

        var stalePolicyRejected = false;
        try
        {
            await new LeaveRepository(db).PostLeaveYearCloseAsync(closeId, "HR_POLICY_SNAPSHOT_TEST");
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Leave-year policy", StringComparison.Ordinal))
        {
            stalePolicyRejected = true;
        }

        if (!stalePolicyRejected || close.Status != "DRAFT")
            throw new InvalidOperationException("Posting a leave close did not reject a changed carry-forward policy while preserving DRAFT status.");
        await transaction.RollbackAsync();
        db.ChangeTracker.Clear();
        if (await db.LeaveYearCloses.AnyAsync(item => item.Id == closeId) || await db.LeaveTypes.AsNoTracking()
                .Where(item => item.LeaveTypeCode == source.LeaveTypeCode).Select(item => item.CarryForwardAllowed).SingleAsync() != originalCarryForwardAllowed)
            throw new InvalidOperationException("Policy snapshot Oracle fixture or temporary leave-type change remained after rollback.");
        report.Add("Oracle LEV-18 policy snapshot: changing carry-forward policy after draft caused post rejection before balance mutation; close and policy fixtures rolled back.");
    }

    private static async Task VerifyWildcardAndSpecificPostConcurrencyAsync(string connectionString, string leaveTypeCode, List<string> report)
    {
        var wildcardKey = $"{OverlapFromYear}:{OverlapToYear}:*";
        var specificKey = $"{OverlapFromYear}:{OverlapToYear}:{leaveTypeCode}";
        await using var seedConnection = new OracleConnection(connectionString);
        await seedConnection.OpenAsync();
        await SetTemplateSchemaAsync(seedConnection);
        var occupied = Convert.ToInt32(await ReadOneAsync(seedConnection,
            "SELECT COUNT(*) FROM HR_LEAVE_YEAR_CLOSE WHERE CLOSE_KEY IN (:wildcardKey, :specificKey) OR (FROM_YEAR = :fromYear AND STATUS = 'POSTED')",
            ("wildcardKey", wildcardKey), ("specificKey", specificKey), ("fromYear", OverlapFromYear)));
        if (occupied != 0)
            throw new InvalidOperationException($"Reserved overlap close fixture for {OverlapFromYear} is already occupied.");

        var seededIds = new List<long>();
        await using var seedContext = CreateContext(seedConnection);
        var wildcard = CreateClose(wildcardKey, "*", "HR_CLOSE_OVERLAP_TEST");
        var specific = CreateClose(specificKey, leaveTypeCode, "HR_CLOSE_OVERLAP_TEST");
        try
        {
            await using (var seedTransaction = await seedContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
            {
                seedContext.LeaveYearCloses.AddRange(wildcard, specific);
                await seedContext.SaveChangesAsync();
                seededIds.Add(wildcard.Id);
                seededIds.Add(specific.Id);
                await seedTransaction.CommitAsync();
            }

            var optionsOne = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(seedConnection,
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
            await using var connectionTwo = new OracleConnection(connectionString);
            await connectionTwo.OpenAsync();
            await SetTemplateSchemaAsync(connectionTwo);
            var optionsTwo = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connectionTwo,
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
            await using var dbOne = new OracleDbContext(optionsOne);
            await using var dbTwo = new OracleDbContext(optionsTwo);
            await using var transactionOne = await dbOne.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await using var transactionTwo = await dbTwo.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

            await LockAllLeaveTypesAsync(seedConnection, (OracleTransaction)transactionOne.GetDbTransaction());
            var postedWildcard = await new LeaveRepository(dbOne).PostLeaveYearCloseAsync(wildcard.Id, "HR_CLOSE_OVERLAP_TEST")
                ?? throw new InvalidOperationException("Wildcard close fixture was not found.");
            if (postedWildcard.Status != "POSTED")
                throw new InvalidOperationException("Wildcard close fixture did not post within its transaction.");

            var specificPostTask = new LeaveRepository(dbTwo).PostLeaveYearCloseAsync(specific.Id, "HR_CLOSE_OVERLAP_TEST");
            await Task.Delay(TimeSpan.FromMilliseconds(350));
            var secondWaitedForTypeLock = !specificPostTask.IsCompleted;
            await transactionOne.CommitAsync();

            var overlapRejected = false;
            try
            {
                await specificPostTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("An overlapping leave-year close is already posted.", StringComparison.Ordinal))
            {
                overlapRejected = true;
            }
            await transactionTwo.RollbackAsync();

            if (!secondWaitedForTypeLock || !overlapRejected)
                throw new InvalidOperationException("A type-specific close did not wait for, then reject, a concurrently posted wildcard close.");
            var specificStatus = Convert.ToString(await ReadOneAsync(seedConnection,
                "SELECT STATUS FROM HR_LEAVE_YEAR_CLOSE WHERE ID = :recordId", ("recordId", specific.Id)));
            if (specificStatus != "DRAFT")
                throw new InvalidOperationException($"Rejected type-specific close changed to '{specificStatus}'.");

            report.Add("Oracle LEV-18 wildcard/type overlap concurrency: type-specific post waited for wildcard close lock, then rejected against the committed POSTED wildcard; the rejected close remained DRAFT.");
        }
        finally
        {
            try
            {
                await using var cleanupConnection = new OracleConnection(connectionString);
                await cleanupConnection.OpenAsync();
                await SetTemplateSchemaAsync(cleanupConnection);
                await using var cleanupTransaction = await cleanupConnection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                await using var cleanupCommand = cleanupConnection.CreateCommand();
                cleanupCommand.Transaction = (OracleTransaction)cleanupTransaction;
                cleanupCommand.CommandText = "DELETE FROM HR_LEAVE_YEAR_CLOSE WHERE CLOSE_KEY IN (:wildcardKey, :specificKey)";
                cleanupCommand.Parameters.Add("wildcardKey", OracleDbType.Varchar2, wildcardKey, System.Data.ParameterDirection.Input);
                cleanupCommand.Parameters.Add("specificKey", OracleDbType.Varchar2, specificKey, System.Data.ParameterDirection.Input);
                await cleanupCommand.ExecuteNonQueryAsync();
                await cleanupTransaction.CommitAsync();
            }
            catch
            {
                throw new InvalidOperationException($"Could not remove DEV_TEMPLATE overlap fixtures {string.Join(',', seededIds)}; inspect only these reserved HR close rows.");
            }
        }
    }

    private static LeaveYearClose CreateClose(string closeKey, string leaveTypeCode, string user) => new()
    {
        CloseKey = closeKey, FromYear = OverlapFromYear, ToYear = OverlapToYear,
        LeaveTypeCode = leaveTypeCode, Status = "DRAFT", CreationUser = user
    };

    private static OracleDbContext CreateContext(OracleConnection connection)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
        return new OracleDbContext(options);
    }

    private static async Task LockAllLeaveTypesAsync(OracleConnection connection, OracleTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT LEAVE_TYPE_CODE FROM HR_LEAVE_TYPE ORDER BY LEAVE_TYPE_CODE FOR UPDATE";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { }
    }

    private static async Task SetTemplateSchemaAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA = DEV_TEMPLATE";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ReadOneAsync(OracleConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return await command.ExecuteScalarAsync();
    }
}

internal sealed class ThrowOnSecondSaveChangesInterceptor : SaveChangesInterceptor
{
    public const string FailureMessage = "Injected leave-close failure after the balance and ledger flush.";
    private int _saveCalls;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _saveCalls) == 2)
            throw new InvalidOperationException(FailureMessage);
        return ValueTask.FromResult(result);
    }
}
