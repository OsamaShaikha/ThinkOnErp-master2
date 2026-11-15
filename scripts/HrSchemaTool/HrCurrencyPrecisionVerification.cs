using System.Text.RegularExpressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrCurrencyPrecisionVerification
{
    public static async Task Verify(OracleConnection connection, string connectionString, List<string> report)
    {
        const string code = "HRTESTPREC";
        var company1 = DateTime.UtcNow.Ticks + 1_000_000_000_000_000_000L;
        var company2 = company1 + 1;
        var originalSchema = Convert.ToString(await Scalar(connection,
            "SELECT SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL")) ?? throw new InvalidOperationException("Could not identify the current Oracle schema.");
        if (!Regex.IsMatch(originalSchema, "^[A-Z][A-Z0-9_$#]*$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("The current Oracle schema identifier cannot be restored safely.");
        await using var transaction = connection.BeginTransaction();

        try
        {
            var existing = await Scalar(connection,
                "SELECT COUNT(*) FROM DEV_TEMPLATE.HR_PAYROLL_CURRENCY_PRECISION WHERE COMPANY_ID IN (:company1,:company2) AND CURRENCY_CODE=:code",
                ("company1", company1), ("company2", company2), ("code", code));
            if (Convert.ToInt32(existing) != 0)
                throw new InvalidOperationException("The reserved payroll currency precision verification keys are already in use.");

            await Execute(connection, $"ALTER SESSION SET CURRENT_SCHEMA={originalSchema}");
            await Execute(connection, "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
            var options = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection).Options;
            await using (var db = new OracleDbContext(options))
            {
                await db.Database.UseTransactionAsync(transaction);
                var repository = new PolicyRepository(db);
                await repository.SavePayrollCurrencyPrecisionAsync(new PayrollCurrencyPrecision
                {
                    CompanyId = company1, CurrencyCode = code, DecimalPlaces = 0,
                    CreationUser = "HR_SCHEMA_VERIFY", CreationDate = DateTime.UtcNow
                });
                await repository.SaveChangesAsync();
                await repository.SavePayrollCurrencyPrecisionAsync(new PayrollCurrencyPrecision
                {
                    CompanyId = company2, CurrencyCode = code, DecimalPlaces = 4,
                    CreationUser = "HR_SCHEMA_VERIFY", CreationDate = DateTime.UtcNow
                });
                await repository.SaveChangesAsync();

                var first = await repository.GetConfiguredPayrollCurrencyDecimalsAsync(company1);
                var second = await repository.GetConfiguredPayrollCurrencyDecimalsAsync(company2);
                if (first.Count != 1 || first[code] != 0 || second.Count != 1 || second[code] != 4)
                    throw new InvalidOperationException("PolicyRepository did not preserve company-isolated precision values.");

                await repository.SavePayrollCurrencyPrecisionAsync(new PayrollCurrencyPrecision
                {
                    CompanyId = company1, CurrencyCode = code, DecimalPlaces = 2,
                    UpdateUser = "HR_SCHEMA_VERIFY", UpdateDate = DateTime.UtcNow
                });
                await repository.SaveChangesAsync();
                first = await repository.GetConfiguredPayrollCurrencyDecimalsAsync(company1);
                if (first[code] != 2)
                    throw new InvalidOperationException("PolicyRepository update was not visible within the transaction.");

                if (!await repository.DeletePayrollCurrencyPrecisionAsync(company1, code) ||
                    !await repository.DeletePayrollCurrencyPrecisionAsync(company2, code))
                    throw new InvalidOperationException("PolicyRepository did not delete the verification overrides.");
                await repository.SaveChangesAsync();
                if ((await repository.GetConfiguredPayrollCurrencyDecimalsAsync(company1)).Count != 0 ||
                    (await repository.GetConfiguredPayrollCurrencyDecimalsAsync(company2)).Count != 0)
                    throw new InvalidOperationException("PolicyRepository verification overrides remained before rollback.");

                await VerifyCalculationEngineAsync(repository, company1, company2, report);
            }

            transaction.Rollback();
            await Execute(connection, $"ALTER SESSION SET CURRENT_SCHEMA={originalSchema}");
            report.Add("Verified PolicyRepository INSERT/SELECT/UPDATE/DELETE and company isolation in Oracle; synthetic rows were rolled back and not retained.");
            await VerifyConcurrentUpdate(connectionString, report);
        }
        catch
        {
            try { transaction.Rollback(); } catch { }
            try { await Execute(connection, $"ALTER SESSION SET CURRENT_SCHEMA={originalSchema}"); } catch { }
            throw;
        }
    }

    private static async Task VerifyCalculationEngineAsync(PolicyRepository repository, long company1, long company2, List<string> report)
    {
        const string code = "HRTESTENG";
        await repository.SavePayrollCurrencyPrecisionAsync(new PayrollCurrencyPrecision
        {
            CompanyId = company1, CurrencyCode = code, DecimalPlaces = 0,
            CreationUser = "HR_SCHEMA_VERIFY", CreationDate = DateTime.UtcNow
        });
        await repository.SavePayrollCurrencyPrecisionAsync(new PayrollCurrencyPrecision
        {
            CompanyId = company2, CurrencyCode = code, DecimalPlaces = 4,
            CreationUser = "HR_SCHEMA_VERIFY", CreationDate = DateTime.UtcNow
        });
        await repository.SaveChangesAsync();

        var engine = new PayrollCalculationEngine(
            repository,
            PayrollVerificationProxy.Create<IPayrollProrationService>(),
            PayrollVerificationProxy.Create<IOvertimeCalculationService>(),
            PayrollVerificationProxy.Create<ISSCCalculationService>(),
            PayrollVerificationProxy.Create<ITaxCalculationEngine>(),
            PayrollVerificationProxy.Create<ILoanDeductionService>(),
            PayrollVerificationProxy.Create<IPayrollAdjustmentRepository>(),
            PayrollVerificationProxy.Create<ILeaveService>());
        var employee = new Employee { EmployeeCode = "HR_PREC_VERIFY", HireDate = new DateTime(2026, 1, 1), IsActive = true, EmploymentStatus = "ACTIVE" };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2026, 1, 1),
            BasicSalary = 100.005m,
            CurrencyCode = code
        });

        var first = await engine.CalculateEmployeePayrollLineAsync(
            new PayrollPeriod { CompanyId = company1, PeriodCode = "VERIFY-1", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) },
            employee, null, null, null, null, Array.Empty<PayrollAdjustment>());
        var second = await engine.CalculateEmployeePayrollLineAsync(
            new PayrollPeriod { CompanyId = company2, PeriodCode = "VERIFY-2", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) },
            employee, null, null, null, null, Array.Empty<PayrollAdjustment>());
        if (first.BasicSalary != 100m || second.BasicSalary != 100.005m)
            throw new InvalidOperationException($"PayrollCalculationEngine did not apply Oracle company-specific precision (company 1: {first.BasicSalary}; company 2: {second.BasicSalary}).");

        if (!await repository.DeletePayrollCurrencyPrecisionAsync(company1, code) ||
            !await repository.DeletePayrollCurrencyPrecisionAsync(company2, code))
            throw new InvalidOperationException("Could not delete payroll engine verification overrides before rollback.");
        await repository.SaveChangesAsync();
        report.Add("Verified PayrollCalculationEngine against Oracle-backed PolicyRepository: company-specific precision produced 100 and 100.005 from the same 100.005 salary; supporting calculation collaborators were deterministic verification doubles and all rows rolled back.");
    }

    public class PayrollVerificationProxy : DispatchProxy
    {
        public static T Create<T>() where T : class => DispatchProxy.Create<T, PayrollVerificationProxy>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var returnType = targetMethod?.ReturnType ?? typeof(void);
            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                var result = DefaultResult(resultType);
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result]);
            }
            return DefaultResult(returnType);
        }

        private static object? DefaultResult(Type type)
        {
            if (type == typeof(ProrationResult)) return new ProrationResult(1m, 30m, 30m, "CALENDAR_DAYS", "VERIFY");
            if (type == typeof(SSCResult)) return new SSCResult(0m, 0m, 0m, 0m, 0m, 0m, 0m, "VERIFY");
            if (type == typeof(TaxResult)) return new TaxResult(0m, 0m, 0m, 0m, 0m, 0m, "VERIFY");
            if (type == typeof(LoanDeductionCalculationResult)) return new LoanDeductionCalculationResult(0m, 0m, 0m, [], []);
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    private static async Task VerifyConcurrentUpdate(string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        await using var firstConnection = new OracleConnection(safeConnectionString);
        await using var secondConnection = new OracleConnection(safeConnectionString);
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();
        await Execute(firstConnection, "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await Execute(secondConnection, "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");

        var company = DateTime.UtcNow.Ticks + 2_000_000_000_000_000_000L;
        const string code = "HRTESTLOCK";
        var reserved = await Scalar(firstConnection,
            "SELECT COUNT(*) FROM HR_PAYROLL_CURRENCY_PRECISION WHERE COMPANY_ID=:company AND CURRENCY_CODE=:code",
            ("company", company), ("code", code));
        if (Convert.ToInt32(reserved) != 0)
            throw new InvalidOperationException("The reserved payroll precision concurrency key is already in use.");

        await using var seedTransaction = firstConnection.BeginTransaction();
        try
        {
            await Execute(firstConnection,
                "INSERT INTO HR_PAYROLL_CURRENCY_PRECISION (COMPANY_ID,CURRENCY_CODE,DECIMAL_PLACES,CREATION_USER,CREATION_DATE) VALUES (:company,:code,1,'HR_SCHEMA_VERIFY',SYSTIMESTAMP)",
                ("company", company), ("code", code));
            seedTransaction.Commit();
        }
        catch
        {
            try { seedTransaction.Rollback(); } catch { }
            throw;
        }

        try
        {
            await using var firstTransaction = firstConnection.BeginTransaction();
            await using var lockCommand = firstConnection.CreateCommand();
            lockCommand.Transaction = firstTransaction;
            lockCommand.BindByName = true;
            lockCommand.CommandText = "SELECT DECIMAL_PLACES FROM HR_PAYROLL_CURRENCY_PRECISION WHERE COMPANY_ID=:company AND CURRENCY_CODE=:code FOR UPDATE";
            lockCommand.Parameters.Add("company", OracleDbType.Int64).Value = company;
            lockCommand.Parameters.Add("code", OracleDbType.Varchar2, 10).Value = code;
            if (Convert.ToInt32(await lockCommand.ExecuteScalarAsync()) != 1)
                throw new InvalidOperationException("Could not lock the Oracle payroll precision concurrency fixture.");

            var options = new DbContextOptionsBuilder<OracleDbContext>()
                .UseOracle(secondConnection, o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
            await using var secondDb = new OracleDbContext(options);
            await using var secondTransaction = await secondDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            var updateFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var updateTask = Task.Run(async () =>
            {
                try
                {
                    var repository = new PolicyRepository(secondDb);
                    await repository.SavePayrollCurrencyPrecisionAsync(new PayrollCurrencyPrecision
                    {
                        CompanyId = company, CurrencyCode = code, DecimalPlaces = 3,
                        UpdateUser = "HR_SCHEMA_VERIFY", UpdateDate = DateTime.UtcNow
                    });
                    await repository.SaveChangesAsync();
                }
                finally { updateFinished.TrySetResult(); }
            });

            await Task.Delay(TimeSpan.FromMilliseconds(350));
            var blockedByRowLock = !updateFinished.Task.IsCompleted;
            await firstTransaction.RollbackAsync();
            await updateTask.WaitAsync(TimeSpan.FromSeconds(10));
            await secondTransaction.RollbackAsync();
            if (!blockedByRowLock)
                throw new InvalidOperationException("The PolicyRepository update did not wait for the competing Oracle row lock.");

            var after = await Scalar(firstConnection,
                "SELECT DECIMAL_PLACES FROM HR_PAYROLL_CURRENCY_PRECISION WHERE COMPANY_ID=:company AND CURRENCY_CODE=:code",
                ("company", company), ("code", code));
            if (Convert.ToInt32(after) != 1)
                throw new InvalidOperationException("The rolled-back concurrent precision update changed the persisted value.");
        }
        finally
        {
            await using var cleanupTransaction = firstConnection.BeginTransaction();
            try
            {
                await Execute(firstConnection,
                    "DELETE FROM HR_PAYROLL_CURRENCY_PRECISION WHERE COMPANY_ID=:company AND CURRENCY_CODE=:code",
                    ("company", company), ("code", code));
                cleanupTransaction.Commit();
            }
            catch
            {
                try { cleanupTransaction.Rollback(); } catch { }
                throw;
            }
        }

        report.Add("Verified a second Oracle session waits on the payroll currency precision row during PolicyRepository update; both update and fixture cleanup were rolled back/verified.");
    }

    private static async Task<object?> Scalar(OracleConnection connection, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = sql;
        AddParameters(command, parameters);
        return await command.ExecuteScalarAsync();
    }

    private static async Task Execute(OracleConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = sql;
        AddParameters(command, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameters(OracleCommand command, IEnumerable<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            var parameter = value switch
            {
                long => command.Parameters.Add(name, OracleDbType.Int64),
                int => command.Parameters.Add(name, OracleDbType.Int32),
                _ => command.Parameters.Add(name, OracleDbType.Varchar2, 10)
            };
            parameter.Value = value;
        }
    }
}
