using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrEmployeeImportRollbackVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            options => options.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var seed = await (from employee in db.Employees.AsNoTracking()
                          join branch in db.SysBranches.AsNoTracking() on employee.BranchId equals (long?)branch.Id
                          where employee.IsActive && employee.EmploymentStatus == "ACTIVE" && branch.IsActive && branch.CompanyId.HasValue
                          select new { employee.EmployeeCode, BranchId = branch.Id, CompanyId = branch.CompanyId!.Value, branch.BaseCurrencyId }).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Employee import rollback verification requires an active employee branch on DEV_TEMPLATE.");
        var currencyCode = await db.SysCurrencies.AsNoTracking().Where(currency => currency.ShortNameEn != null)
            .Select(currency => currency.ShortNameEn!.Trim().ToUpper()).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Employee import rollback verification requires an already configured ERP currency.");

        var token = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var firstCode = "VIMP" + token[..12];
        var lastCode = "VIMP" + token[12..24];
        var firstNationalId = "VNI" + token[..14];
        var lastNationalId = "VNI" + token[14..28];
        const string actor = "HR_EMPLOYEE_IMPORT_ROLLBACK_VERIFY";
        var hireDate = new DateTime(9996, 1, 1);
        var birthDate = new DateTime(1980, 1, 1);
        db.HrScopeCompanyId = seed.CompanyId;
        db.HrScopeEnabled = true;
        db.HrScopeAllBranches = true;
        long temporaryBranchId = 0;
        try
        {
            var temporaryBranch = new ThinkOnErp.Domain.Entities.SysBranch
            {
                CompanyId = seed.CompanyId,
                BranchNameLocal = "فحص استيراد HR " + token[..8],
                BranchNameEn = "HR import verification " + token[..8],
                BaseCurrencyId = seed.BaseCurrencyId,
                IsActive = true,
                CreationUser = actor,
                CreationDate = DateTime.UtcNow
            };
            db.SysBranches.Add(temporaryBranch);
            await db.SaveChangesAsync();
            temporaryBranchId = temporaryBranch.Id;
            if (temporaryBranchId <= 0) throw new InvalidOperationException("Temporary employee import branch was not created.");

            if (await db.Employees.IgnoreQueryFilters().AnyAsync(employee => employee.EmployeeCode == firstCode || employee.EmployeeCode == lastCode))
                throw new InvalidOperationException("Generated employee import fixture code already exists.");

            var workbook = new XlsxEmployeeWorkbookWriter().WriteExportWorkbook(new[]
            {
                new EmployeeExportRowDto
                {
                    EmployeeCode = firstCode, NameLocal = "فحص استيراد أول", NameEn = "Import first row",
                    NationalId = firstNationalId, Nationality = "Verification nationality", DateOfBirth = birthDate,
                    Gender = "M", MaritalStatus = "SINGLE", HireDate = hireDate, EmploymentType = "FULL_TIME",
                    EmploymentStatus = "ACTIVE", BranchId = temporaryBranchId, IsActive = true,
                    BasicSalary = 1000m, SalaryEffectiveFrom = hireDate, CurrencyCode = currencyCode, PaymentMethod = "BANK_TRANSFER"
                },
                new EmployeeExportRowDto
                {
                    EmployeeCode = lastCode, NameLocal = "فحص استيراد أخير", NameEn = "Import final row",
                    NationalId = lastNationalId, Nationality = "Verification nationality", DateOfBirth = birthDate,
                    Gender = "M", MaritalStatus = "SINGLE", HireDate = hireDate, EmploymentType = "FULL_TIME",
                    EmploymentStatus = "INVALID_STATUS", BranchId = temporaryBranchId, IsActive = true,
                    BasicSalary = 1000m, SalaryEffectiveFrom = hireDate, CurrencyCode = currencyCode, PaymentMethod = "BANK_TRANSFER"
                }
            });

            var service = new EmployeeExcelService(new EmployeeRepository(db), new XlsxEmployeeWorkbookReader(),
                new XlsxEmployeeWorkbookWriter(), NullLogger<EmployeeExcelService>.Instance, new HrUnitOfWork(db));
            using var stream = new MemoryStream(workbook);
            var result = await service.ImportEmployeesAsync(stream, updateExisting: false, user: actor);
            var lastRowError = result.Errors.SingleOrDefault(error => error.RowNumber == 3 && error.Code == "INVALID_EMPLOYEE_PROFILE");
            if (lastRowError == null || result.SuccessCount != 0 || result.InsertedCount != 0 || result.UpdatedCount != 0 || result.FailureCount != 1)
                throw new InvalidOperationException($"The actual employee workbook import did not reject the invalid final row as an all-or-nothing batch (errors={string.Join(',', result.Errors.Select(error => $"{error.RowNumber}:{error.Code}"))}; success={result.SuccessCount}; inserted={result.InsertedCount}; updated={result.UpdatedCount}; failures={result.FailureCount}).");

            if (await db.Employees.IgnoreQueryFilters().AnyAsync(employee => employee.EmployeeCode == firstCode || employee.EmployeeCode == lastCode) ||
                await db.SalaryStructures.IgnoreQueryFilters().AnyAsync(structure => structure.EmployeeCode == firstCode || structure.EmployeeCode == lastCode))
                throw new InvalidOperationException("Employee import persisted an earlier valid row after its final row failed.");
            report.Add("Oracle employee Excel import rollback: the real XLSX reader/service accepted a valid first new employee, rejected the invalid final row, returned zero successes, and left no employee or salary structure persisted.");
        }
        finally
        {
            await CleanupAsync(connection, firstCode, lastCode, temporaryBranchId, actor);
        }

        if (await db.Employees.IgnoreQueryFilters().AnyAsync(employee => employee.EmployeeCode == firstCode || employee.EmployeeCode == lastCode) ||
            await db.SalaryStructures.IgnoreQueryFilters().AnyAsync(structure => structure.EmployeeCode == firstCode || structure.EmployeeCode == lastCode) ||
            await db.SysBranches.IgnoreQueryFilters().AnyAsync(branch => branch.Id == temporaryBranchId))
            throw new InvalidOperationException("Employee import rollback cleanup left fixture rows behind.");
        report.Add("Oracle employee Excel import cleanup verified: both employee codes, salary structures, and the temporary branch are absent.");
    }

    private static async Task CleanupAsync(OracleConnection connection, string firstCode, string lastCode, long temporaryBranchId, string actor)
    {
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await using (var lines = connection.CreateCommand())
        {
            lines.Transaction = (OracleTransaction)transaction;
            lines.CommandText = "DELETE FROM HR_SALARY_STRUCTURE_LINE WHERE STRUCTURE_ID IN (SELECT ID FROM HR_SALARY_STRUCTURE WHERE EMPLOYEE_CODE IN (:p_first,:p_last) AND CREATION_USER=:p_actor)";
            lines.Parameters.Add("p_first", OracleDbType.NVarchar2, firstCode, System.Data.ParameterDirection.Input);
            lines.Parameters.Add("p_last", OracleDbType.NVarchar2, lastCode, System.Data.ParameterDirection.Input);
            lines.Parameters.Add("p_actor", OracleDbType.NVarchar2, actor, System.Data.ParameterDirection.Input);
            await lines.ExecuteNonQueryAsync();
        }
        await using (var structures = connection.CreateCommand())
        {
            structures.Transaction = (OracleTransaction)transaction;
            structures.CommandText = "DELETE FROM HR_SALARY_STRUCTURE WHERE EMPLOYEE_CODE IN (:p_first,:p_last) AND CREATION_USER=:p_actor";
            structures.Parameters.Add("p_first", OracleDbType.NVarchar2, firstCode, System.Data.ParameterDirection.Input);
            structures.Parameters.Add("p_last", OracleDbType.NVarchar2, lastCode, System.Data.ParameterDirection.Input);
            structures.Parameters.Add("p_actor", OracleDbType.NVarchar2, actor, System.Data.ParameterDirection.Input);
            await structures.ExecuteNonQueryAsync();
        }
        await using (var employees = connection.CreateCommand())
        {
            employees.Transaction = (OracleTransaction)transaction;
            employees.CommandText = "DELETE FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE IN (:p_first,:p_last) AND CREATION_USER=:p_actor";
            employees.Parameters.Add("p_first", OracleDbType.NVarchar2, firstCode, System.Data.ParameterDirection.Input);
            employees.Parameters.Add("p_last", OracleDbType.NVarchar2, lastCode, System.Data.ParameterDirection.Input);
            employees.Parameters.Add("p_actor", OracleDbType.NVarchar2, actor, System.Data.ParameterDirection.Input);
            await employees.ExecuteNonQueryAsync();
        }
        if (temporaryBranchId > 0)
        {
            await using var branch = connection.CreateCommand();
            branch.Transaction = (OracleTransaction)transaction;
            branch.CommandText = "DELETE FROM SYS_BRANCH WHERE \"Id\"=:p_branch_id AND CREATION_USER=:p_actor";
            branch.Parameters.Add("p_branch_id", OracleDbType.Int64, temporaryBranchId, System.Data.ParameterDirection.Input);
            branch.Parameters.Add("p_actor", OracleDbType.NVarchar2, actor, System.Data.ParameterDirection.Input);
            if (await branch.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("Temporary employee import branch fixture was not removed exactly once.");
        }
        await transaction.CommitAsync();
    }
}
