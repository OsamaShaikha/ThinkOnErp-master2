using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrPayrollPeriodConcurrencyVerification
{
    private const string FixtureUser = "HR_PAYROLL_PERIOD_VERIFY";

    public static async Task Verify(OracleConnection setupConnection, string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        await using var closeConnection = new OracleConnection(safeConnectionString);
        await using var reopenConnection = new OracleConnection(safeConnectionString);
        await closeConnection.OpenAsync();
        await reopenConnection.OpenAsync();
        await SetTemplateSchemaAsync(closeConnection);
        await SetTemplateSchemaAsync(reopenConnection);

        static OracleDbContext Context(OracleConnection connection) => new(
            new DbContextOptionsBuilder<OracleDbContext>()
                .UseOracle(connection, provider => provider.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
                .Options);

        await using var setupDb = Context(setupConnection);
        var companyId = await setupDb.SysBranches.AsNoTracking()
            .Where(b => b.IsActive && b.CompanyId.HasValue)
            .Select(b => b.CompanyId!.Value)
            .FirstOrDefaultAsync();
        if (companyId <= 0)
            throw new InvalidOperationException("PAY-10 Oracle concurrency verification requires an active company branch.");

        var periodCode = "HRV-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var now = DateTime.UtcNow;
        var period = new PayrollPeriod
        {
            CompanyId = companyId,
            PeriodCode = periodCode,
            NameAr = "فترة تحقق مؤقتة",
            NameEn = "Temporary verification period",
            FiscalYear = 9025,
            Month = 1,
            StartDate = new DateTime(9025, 1, 1),
            EndDate = new DateTime(9025, 1, 31),
            PayDate = new DateTime(9025, 1, 31),
            PayrollType = "MONTHLY",
            Status = "OPEN",
            CreationUser = FixtureUser,
            CreationDate = now
        };

        long periodId = 0;
        try
        {
            setupDb.PayrollPeriods.Add(period);
            await setupDb.SaveChangesAsync();
            periodId = period.Id;
            if (periodId <= 0)
                throw new InvalidOperationException("Oracle did not generate an ID for the temporary PAY-10 period.");

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var closeOutcomes = await RunContendedPairAsync(
                setupDb, periodId, periodCode, companyId, closeConnection, reopenConnection,
                (service, token) => service.ClosePeriodAsync(companyId, periodCode, "PAY-10 Oracle close concurrency verification", FixtureUser, token),
                deadline.Token);
            RequireSingleWinner(closeOutcomes, "already closed", "concurrent period close");
            var closed = await setupDb.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == periodId, deadline.Token);
            if (closed.Status != "CLOSED" || closed.UpdateUser != FixtureUser || closed.UpdateDate == null)
                throw new InvalidOperationException("PAY-10 close did not persist CLOSED state and audit fields exactly once.");

            var reopenOutcomes = await RunContendedPairAsync(
                setupDb, periodId, periodCode, companyId, closeConnection, reopenConnection,
                (service, token) => service.ReopenPeriodAsync(companyId, periodCode, "PAY-10 Oracle reopen concurrency verification", FixtureUser, token),
                deadline.Token);
            RequireSingleWinner(reopenOutcomes, "not closed", "concurrent period reopen");
            var reopened = await setupDb.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == periodId, deadline.Token);
            if (reopened.Status != "OPEN" || reopened.UpdateUser != FixtureUser || reopened.UpdateDate == null)
                throw new InvalidOperationException("PAY-10 reopen did not persist OPEN state and audit fields exactly once.");

            report.Add("Oracle PAY-10 payroll period lifecycle: two independent service sessions contended on a held Oracle row lock for close and reopen; exactly one close and one reopen succeeded, each losing request observed the committed winner's state, and audit fields were persisted. The marked synthetic period was removed in cleanup.");
        }
        finally
        {
            if (periodId > 0)
            {
                await using var cleanup = await setupDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                var removed = await setupDb.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM HR_PAYROLL_PERIOD WHERE ID = {periodId} AND COMPANY_ID = {companyId} AND CREATION_USER = {FixtureUser}");
                if (removed != 1)
                    throw new InvalidOperationException("PAY-10 cleanup did not remove exactly its marked temporary payroll period.");
                await cleanup.CommitAsync();
                if (await setupDb.PayrollPeriods.AsNoTracking().AnyAsync(p => p.Id == periodId))
                    throw new InvalidOperationException("PAY-10 temporary payroll period remains after cleanup.");
            }
        }
    }

    private static async Task<Outcome[]> RunContendedPairAsync(
        OracleDbContext lockDb,
        long periodId,
        string periodCode,
        long companyId,
        OracleConnection firstConnection,
        OracleConnection secondConnection,
        Func<PayrollService, CancellationToken, Task<PayrollPeriod>> action,
        CancellationToken cancellationToken)
    {
        await using var lockTransaction = await lockDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using (var command = lockDb.Database.GetDbConnection().CreateCommand())
        {
            command.Transaction = lockTransaction.GetDbTransaction();
            command.CommandText = "SELECT ID FROM HR_PAYROLL_PERIOD WHERE ID=:periodId AND COMPANY_ID=:companyId FOR UPDATE";
            var idParameter = command.CreateParameter(); idParameter.ParameterName = "periodId"; idParameter.Value = periodId;
            var companyParameter = command.CreateParameter(); companyParameter.ParameterName = "companyId"; companyParameter.Value = companyId;
            command.Parameters.Add(idParameter); command.Parameters.Add(companyParameter);
            if (await command.ExecuteScalarAsync(cancellationToken) == null)
                throw new InvalidOperationException("PAY-10 lock holder could not lock its temporary payroll period.");
        }

        var firstTask = InvokeAsync(firstConnection, action, cancellationToken);
        var secondTask = InvokeAsync(secondConnection, action, cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
        if (firstTask.IsCompleted || secondTask.IsCompleted)
            throw new InvalidOperationException($"A concurrent {periodCode} lifecycle request did not wait on the held Oracle row lock.");
        await lockTransaction.CommitAsync(cancellationToken);
        return await Task.WhenAll(firstTask, secondTask);

        async Task<Outcome> InvokeAsync(OracleConnection connection, Func<PayrollService, CancellationToken, Task<PayrollPeriod>> operation, CancellationToken token)
        {
            await using var context = new OracleDbContext(
                new DbContextOptionsBuilder<OracleDbContext>()
                    .UseOracle(connection, provider => provider.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
                    .Options);
            var repository = new PayrollPeriodRepository(context);
            var service = new PayrollService(repository, UnusedPayrollCalculationEngine.Instance,
                new PayrollValidationService(repository, new PolicyRepository(context)), new HrUnitOfWork(context));
            try
            {
                await operation(service, token);
                return new Outcome(null);
            }
            catch (Exception exception)
            {
                return new Outcome(exception);
            }
        }
    }

    private static void RequireSingleWinner(Outcome[] outcomes, string expectedFailure, string operation)
    {
        if (outcomes.Length != 2 || outcomes.Count(o => o.Error == null) != 1 || outcomes.Count(o => o.Error is InvalidOperationException ex && ex.Message.Contains(expectedFailure, StringComparison.OrdinalIgnoreCase)) != 1)
            throw new InvalidOperationException($"PAY-10 {operation} did not serialize to one success and one expected state rejection: {string.Join(" | ", outcomes.Select(o => o.Error?.GetType().Name + ": " + o.Error?.Message ?? "success"))}.");
    }

    private static async Task SetTemplateSchemaAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE";
        await command.ExecuteNonQueryAsync();
    }

    private sealed record Outcome(Exception? Error);

    private sealed class UnusedPayrollCalculationEngine : IPayrollCalculationEngine
    {
        public static readonly UnusedPayrollCalculationEngine Instance = new();
        public Task<PayrollRun> CalculatePayrollForPeriodAsync(PayrollPeriod period, IReadOnlyList<Employee> employees, long? branchId = null, string calculatedBy = "SYSTEM", CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Calculation is outside the PAY-10 period lifecycle verifier.");
        public Task<PayrollRunLine> CalculateEmployeePayrollLineAsync(PayrollPeriod period, Employee employee, ProrationPolicy? prorationPolicy, SSCPolicy? sscPolicy, TaxPolicy? taxPolicy, DeductionPolicy? deductionPolicy, IReadOnlyList<PayrollAdjustment> adjustments, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Calculation is outside the PAY-10 period lifecycle verifier.");
    }
}
