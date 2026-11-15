using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrPayrollOvertimeOracleVerification
{
    private const string FixtureUser = "HR_PAY17_ORACLE_VERIFY";

    public static async Task Verify(string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { ConnectionTimeout = 10, Pooling = false }.ConnectionString;
        await using var firstConnection = new OracleConnection(safeConnectionString);
        await using var secondConnection = new OracleConnection(safeConnectionString);
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();
        await SetTemplateSchema(firstConnection);
        await SetTemplateSchema(secondConnection);

        var optionsBuilder = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle(firstConnection, oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        using var lookupDb = new OracleDbContext(optionsBuilder.Options);
        var suffix = Guid.NewGuid().ToString("N");
        var componentCode = "VOT" + suffix[..12];
        var fixture = await FindFixtureAsync(lookupDb, componentCode);
        if (fixture == null)
        {
            var activeEmployees = await (from employee in lookupDb.Employees.AsNoTracking()
                join branch in lookupDb.SysBranches.AsNoTracking() on employee.BranchId equals (long?)branch.Id
                where employee.IsActive && employee.EmploymentStatus == "ACTIVE" && branch.IsActive && branch.CompanyId.HasValue
                select employee.EmployeeCode).CountAsync();
            var activeSalaries = await lookupDb.SalaryStructures.AsNoTracking().CountAsync(s => s.IsActive && s.BasicSalary > 0m &&
                (!s.EffectiveTo.HasValue || s.EffectiveTo.Value >= s.EffectiveFrom));
            var activeComponents = await lookupDb.SalaryComponents.AsNoTracking().CountAsync(c => c.IsActive);
            var eligibleComponents = await lookupDb.SalaryComponents.AsNoTracking().CountAsync(c => c.IsActive && (c.ComponentType == "BASIC" || c.ComponentType == "ALLOWANCE"));
            throw new InvalidOperationException($"PAY-17 Oracle verification fixture unavailable (activeEmployees={activeEmployees}, activeSalaryStructures={activeSalaries}, activeComponents={activeComponents}, eligibleEarningComponents={eligibleComponents}).");
        }

        var periodCode = "V" + suffix[..19];
        var ruleCode = "V" + suffix[..19];
        var attendanceDate = fixture.AttendanceDate;
        long periodId = 0;
        long overtimeRuleId = 0;
        long attendanceId = 0;
        var gate = new AdjustmentInsertGate();
        Task<PayrollAdjustment>? firstCreate = null;
        Task<PayrollAdjustment>? secondCreate = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));

        try
        {
            lookupDb.PayrollPeriods.Add(new PayrollPeriod
            {
                CompanyId = fixture.CompanyId, PeriodCode = periodCode, NameAr = "تحقق إضافي مؤقت", NameEn = "Temporary overtime verification",
                FiscalYear = attendanceDate.Year, Month = attendanceDate.Month, StartDate = attendanceDate, EndDate = attendanceDate,
                PayDate = attendanceDate, PayrollType = "SPECIAL", Status = "OPEN", CreationUser = FixtureUser, CreationDate = DateTime.UtcNow
            });
            var rule = new OvertimeRule
            {
                CompanyId = fixture.CompanyId, Code = ruleCode, NameAr = "تحقق إضافي", NameEn = "Verification overtime",
                EffectiveFrom = attendanceDate, DayType = "REGULAR", Multiplier = 1.25m, MinimumMinutes = 1, MaximumMinutes = 240,
                HourlyDivisorFormula = "MONTHLY_SALARY / 240", RequiresApproval = true, IsActive = true,
                CreationUser = FixtureUser, CreationDate = DateTime.UtcNow
            };
            lookupDb.OvertimeRules.Add(rule);
            lookupDb.SalaryComponents.Add(new SalaryComponent
            {
                ComponentCode = componentCode, NameLocal = "عنصر تحقق الإضافي", NameEn = "Verification overtime earning",
                ComponentType = "ALLOWANCE", CalculationType = "FIXED", IsTaxable = true, IsSscApplicable = true,
                IsActive = true, CreationUser = FixtureUser, CreationDate = DateTime.UtcNow
            });
            var attendance = new AttendanceDay
            {
                EmployeeCode = fixture.EmployeeCode, AttendanceDate = attendanceDate, Status = "PRESENT", OvertimeHours = 2m,
                CheckInBranchId = fixture.BranchId, CheckOutBranchId = fixture.BranchId,
                CreationUser = FixtureUser, CreationDate = DateTime.UtcNow
            };
            lookupDb.AttendanceDays.Add(attendance);
            await lookupDb.SaveChangesAsync(deadline.Token);
            periodId = await lookupDb.PayrollPeriods.Where(p => p.CompanyId == fixture.CompanyId && p.PeriodCode == periodCode)
                .Select(p => p.Id).SingleAsync(deadline.Token);
            overtimeRuleId = rule.Id;
            attendanceId = attendance.Id;
            lookupDb.ChangeTracker.Clear();

            using var firstDb = CreateContext(firstConnection, fixture.CompanyId, gate);
            using var secondDb = CreateContext(secondConnection, fixture.CompanyId);
            var firstService = CreateService(firstDb);
            var secondService = CreateService(secondDb);
            var dto = new CreateOvertimePayrollAdjustmentDto(fixture.CompanyId, fixture.EmployeeCode, attendanceDate,
                periodCode, fixture.ComponentCode, fixture.BranchId);

            firstCreate = Task.Run(() => firstService.CreateOvertimeAdjustmentAsync(dto, FixtureUser, deadline.Token), deadline.Token);
            await gate.InsertReached.Task.WaitAsync(deadline.Token);
            var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            secondCreate = Task.Run(async () =>
            {
                secondStarted.TrySetResult();
                return await secondService.CreateOvertimeAdjustmentAsync(dto, FixtureUser, deadline.Token);
            }, deadline.Token);
            await secondStarted.Task.WaitAsync(deadline.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(400), deadline.Token);
            if (secondCreate.IsCompleted)
                throw new InvalidOperationException("The second Oracle overtime submission completed while the first held the payroll period lock.");

            gate.AllowInsert.TrySetResult();
            var created = await firstCreate.WaitAsync(deadline.Token);
            var duplicate = await secondCreate.WaitAsync(deadline.Token);
            if (created.Id <= 0 || created.Id != duplicate.Id || created.Status != "PENDING" || created.SourceAttendanceDate?.Date != attendanceDate ||
                created.SourceOvertimeHours != 2m || created.SourceBaseSalary != fixture.BasicSalary || created.Amount <= 0m)
                throw new InvalidOperationException("Concurrent Oracle overtime submissions did not return the same valid pending source adjustment.");
            if (await CountSourceAdjustments(firstConnection, fixture.CompanyId, attendanceId) != 1)
                throw new InvalidOperationException("Concurrent overtime submissions produced more than one Oracle adjustment for the attendance source.");
            report.Add("PAY-17 Oracle create race: two service calls on independent sessions serialized on the period lock and returned the same single PENDING attendance overtime adjustment.");

            await ChangeAttendanceHours(secondDb, attendanceId, 3m, deadline.Token);
            await AssertApprovalRejected(firstService, created.Id, fixture.BranchId,
                "Attendance source changed after overtime submission. Remove and resubmit the adjustment after review.", deadline.Token);
            await ChangeAttendanceHours(secondDb, attendanceId, 2m, deadline.Token);

            await ChangeOvertimeMultiplier(secondDb, overtimeRuleId, 1.5m, deadline.Token);
            await AssertApprovalRejected(firstService, created.Id, fixture.BranchId, "The effective overtime policy changed after submission. Remove and resubmit under the current policy.", deadline.Token);
            await ChangeOvertimeMultiplier(secondDb, overtimeRuleId, 1.25m, deadline.Token);

            var firstDecision = Task.Run(() => firstService.DecideAdjustmentAsync(created.Id, true,
                "Oracle PAY-17 first decision", FixtureUser, fixture.BranchId, deadline.Token), deadline.Token);
            await gate.ApprovalReached.Task.WaitAsync(deadline.Token);
            var secondDecision = Task.Run(() => secondService.DecideAdjustmentAsync(created.Id, true,
                "Oracle PAY-17 competing decision", FixtureUser, fixture.BranchId, deadline.Token), deadline.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(400), deadline.Token);
            if (secondDecision.IsCompleted)
                throw new InvalidOperationException("The second Oracle overtime decision completed while the first held the payroll period lock.");
            gate.AllowApproval.TrySetResult();

            var approved = await firstDecision.WaitAsync(deadline.Token);
            if (approved.Status != "APPROVED" || approved.DecisionReason != "Oracle PAY-17 first decision")
                throw new InvalidOperationException("The first Oracle PAY-17 decision did not persist approval with its audit reason.");
            try
            {
                _ = await secondDecision.WaitAsync(deadline.Token);
                throw new InvalidOperationException("Both concurrent Oracle PAY-17 approval decisions succeeded.");
            }
            catch (InvalidOperationException exception) when (exception.Message == "Only a pending adjustment can be approved or rejected.") { }
            secondDb.ChangeTracker.Clear();
            var payrollInputs = await new PayrollAdjustmentRepository(secondDb)
                .GetPayrollCalculationAdjustmentsAsync(fixture.CompanyId, periodCode, deadline.Token);
            if (payrollInputs.Count != 1 || payrollInputs[0].Id != created.Id || payrollInputs[0].Status != "APPROVED")
                throw new InvalidOperationException("Oracle payroll calculation input selection did not include exactly the approved overtime adjustment.");
            report.Add("PAY-17 Oracle revalidation/concurrency: changed attendance hours and changed effective multiplier were each rejected; after restoration, concurrent decisions serialized, one persisted APPROVED and the waiter rejected the already-decided row; the actual payroll adjustment query selected that approved source exactly once.");
        }
        finally
        {
            gate.AllowInsert.TrySetResult();
            gate.AllowApproval.TrySetResult();
            if (firstCreate != null) try { await firstCreate.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            if (secondCreate != null) try { await secondCreate.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            await CleanupAsync(firstConnection, fixture.CompanyId, fixture.EmployeeCode, periodCode, ruleCode, attendanceDate,
                periodId, overtimeRuleId, attendanceId, componentCode);
        }

        if (await CountFixtures(firstConnection, fixture.CompanyId, periodCode, ruleCode, attendanceDate, componentCode) != 0)
            throw new InvalidOperationException("PAY-17 Oracle verification left a payroll adjustment, attendance, period, overtime rule or salary component fixture behind.");
        report.Add("PAY-17 Oracle cleanup verified: temporary source adjustment, attendance day, payroll period, overtime rule and earning component are absent.");
    }

    private static async Task<Fixture?> FindFixtureAsync(OracleDbContext db, string componentCode)
    {
        var candidates = await (from employee in db.Employees.AsNoTracking()
            join branch in db.SysBranches.AsNoTracking() on employee.BranchId equals (long?)branch.Id
            where employee.IsActive && employee.EmploymentStatus == "ACTIVE" && branch.IsActive && branch.CompanyId.HasValue
            orderby employee.EmployeeCode
            select new { employee.EmployeeCode, BranchId = branch.Id, CompanyId = branch.CompanyId!.Value }).ToListAsync();
        foreach (var candidate in candidates)
        {
            var salary = await db.SalaryStructures.AsNoTracking()
                .Where(s => s.EmployeeCode == candidate.EmployeeCode && s.IsActive && s.BasicSalary > 0m &&
                    (!s.EffectiveTo.HasValue || s.EffectiveTo.Value >= s.EffectiveFrom))
                .OrderByDescending(s => s.EffectiveFrom).FirstOrDefaultAsync();
            if (salary != null)
                return new Fixture(candidate.CompanyId, candidate.BranchId, candidate.EmployeeCode, componentCode,
                    salary.BasicSalary, salary.EffectiveFrom.Date);
        }
        return null;
    }

    private static OracleDbContext CreateContext(OracleConnection connection, long companyId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle(connection, oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        if (interceptor != null) options.AddInterceptors(interceptor);
        var db = new OracleDbContext(options.Options)
        {
            HrScopeEnabled = true,
            HrScopeCompanyId = companyId,
            HrScopeAllBranches = true
        };
        return db;
    }

    private static PayrollAdjustmentService CreateService(OracleDbContext db) => new(
        new PayrollAdjustmentRepository(db), new HrUnitOfWork(db), new AttendanceCorrectionRepository(db),
        new EmployeeRepository(db), new PayrollPeriodRepository(db), new OvertimeCalculationService(new PolicyRepository(db)));

    private static async Task AssertApprovalRejected(PayrollAdjustmentService service, long adjustmentId, long branchId,
        string expectedMessage, CancellationToken cancellationToken)
    {
        try
        {
            await service.DecideAdjustmentAsync(adjustmentId, true, "Oracle PAY-17 negative check", FixtureUser, branchId, cancellationToken);
            throw new InvalidOperationException("Oracle PAY-17 approval succeeded despite a changed source snapshot.");
        }
        catch (InvalidOperationException exception) when (exception.Message == expectedMessage) { }
    }

    private static async Task ChangeAttendanceHours(OracleDbContext db, long attendanceId, decimal hours, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var day = await db.AttendanceDays.SingleAsync(d => d.Id == attendanceId, cancellationToken);
        day.OvertimeHours = hours;
        day.UpdateUser = FixtureUser;
        day.UpdateDate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private static async Task ChangeOvertimeMultiplier(OracleDbContext db, long ruleId, decimal multiplier, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var rule = await db.OvertimeRules.SingleAsync(r => r.Id == ruleId, cancellationToken);
        rule.Multiplier = multiplier;
        rule.UpdateUser = FixtureUser;
        rule.UpdateDate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private static async Task<int> CountSourceAdjustments(OracleConnection connection, long companyId, long attendanceId)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "SELECT COUNT(*) FROM HR_PAYROLL_ADJUSTMENT WHERE COMPANY_ID=:company AND SOURCE_TYPE='ATTENDANCE_OVERTIME' AND SOURCE_ID=:sourceId";
        command.Parameters.Add("company", OracleDbType.Int64).Value = companyId;
        command.Parameters.Add("sourceId", OracleDbType.Varchar2, 100).Value = attendanceId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task CleanupAsync(OracleConnection connection, long companyId, string employeeCode, string periodCode,
        string ruleCode, DateTime attendanceDate, long periodId, long ruleId, long attendanceId, string componentCode)
    {
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            await Execute("DELETE FROM HR_PAYROLL_ADJUSTMENT WHERE COMPANY_ID=:company AND SOURCE_TYPE='ATTENDANCE_OVERTIME' AND SOURCE_ID=:sourceId AND CREATION_USER=:fixtureUser",
                ("company", companyId), ("sourceId", attendanceId.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("fixtureUser", FixtureUser));
            await Execute("DELETE FROM HR_SALARY_COMPONENT WHERE COMPONENT_CODE=:code AND CREATION_USER=:fixtureUser",
                ("code", componentCode), ("fixtureUser", FixtureUser));
            await Execute("DELETE FROM HR_ATTENDANCE_DAY WHERE (:id=0 OR ID=:id) AND EMPLOYEE_CODE=:employee AND ATTENDANCE_DATE=:attendanceDate AND CREATION_USER=:fixtureUser",
                ("id", attendanceId), ("employee", employeeCode), ("attendanceDate", attendanceDate), ("fixtureUser", FixtureUser));
            await Execute("DELETE FROM HR_OVERTIME_RULE WHERE (:id=0 OR ID=:id) AND COMPANY_ID=:company AND CODE=:code AND CREATION_USER=:fixtureUser",
                ("id", ruleId), ("company", companyId), ("code", ruleCode), ("fixtureUser", FixtureUser));
            await Execute("DELETE FROM HR_PAYROLL_PERIOD WHERE (:id=0 OR ID=:id) AND COMPANY_ID=:company AND PERIOD_CODE=:periodCode AND CREATION_USER=:fixtureUser AND STATUS='OPEN'",
                ("id", periodId), ("company", companyId), ("periodCode", periodCode), ("fixtureUser", FixtureUser));
            await transaction.CommitAsync();

            async Task Execute(string sql, params (string Name, object Value)[] parameters)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (OracleTransaction)transaction;
                command.BindByName = true;
                command.CommandText = sql;
                foreach (var (name, value) in parameters)
                {
                    var parameter = value switch
                    {
                        long => command.Parameters.Add(name, OracleDbType.Int64),
                        DateTime => command.Parameters.Add(name, OracleDbType.TimeStamp),
                        _ => command.Parameters.Add(name, OracleDbType.Varchar2, 200)
                    };
                    parameter.Value = value;
                }
                await command.ExecuteNonQueryAsync();
            }
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }

    private static async Task<int> CountFixtures(OracleConnection connection, long companyId, string periodCode, string ruleCode, DateTime date, string componentCode)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "SELECT (SELECT COUNT(*) FROM HR_PAYROLL_PERIOD WHERE COMPANY_ID=:company AND PERIOD_CODE=:periodCode AND CREATION_USER=:fixtureUser) + (SELECT COUNT(*) FROM HR_OVERTIME_RULE WHERE COMPANY_ID=:company AND CODE=:ruleCode AND CREATION_USER=:fixtureUser) + (SELECT COUNT(*) FROM HR_ATTENDANCE_DAY WHERE ATTENDANCE_DATE=:attendanceDate AND CREATION_USER=:fixtureUser) + (SELECT COUNT(*) FROM HR_PAYROLL_ADJUSTMENT WHERE COMPANY_ID=:company AND CREATION_USER=:fixtureUser AND SOURCE_TYPE='ATTENDANCE_OVERTIME') + (SELECT COUNT(*) FROM HR_SALARY_COMPONENT WHERE COMPONENT_CODE=:componentCode AND CREATION_USER=:fixtureUser) FROM DUAL";
        command.Parameters.Add("company", OracleDbType.Int64).Value = companyId;
        command.Parameters.Add("periodCode", OracleDbType.Varchar2, 20).Value = periodCode;
        command.Parameters.Add("ruleCode", OracleDbType.Varchar2, 50).Value = ruleCode;
        command.Parameters.Add("fixtureUser", OracleDbType.Varchar2, 100).Value = FixtureUser;
        command.Parameters.Add("attendanceDate", OracleDbType.TimeStamp).Value = date;
        command.Parameters.Add("componentCode", OracleDbType.Varchar2, 50).Value = componentCode;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task SetTemplateSchema(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE";
        await command.ExecuteNonQueryAsync();
    }

    private sealed record Fixture(long CompanyId, long BranchId, string EmployeeCode, string ComponentCode, decimal BasicSalary, DateTime AttendanceDate);

    private sealed class AdjustmentInsertGate : SaveChangesInterceptor
    {
        public TaskCompletionSource InsertReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowInsert { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ApprovalReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowApproval { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<PayrollAdjustment>().Any(entry => entry.State == EntityState.Added) == true)
            {
                InsertReached.TrySetResult();
                await AllowInsert.Task.WaitAsync(cancellationToken);
            }
            if (eventData.Context?.ChangeTracker.Entries<PayrollAdjustment>().Any(entry => entry.State == EntityState.Modified && entry.Entity.Status == "APPROVED") == true)
            {
                ApprovalReached.TrySetResult();
                await AllowApproval.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
