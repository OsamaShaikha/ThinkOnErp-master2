using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class EmployeeSalaryImportTests
{
    [Fact]
    public async Task PayrollAcceptsContiguousSalarySegmentsForCalendarDayPolicy()
    {
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = employee.HireDate, EffectiveTo = new DateTime(2026, 11, 14), BasicSalary = 500m });
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 15), BasicSalary = 700m });
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) });
        repository.Setup(r => r.GetActiveEmployeesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { employee });
        var result = await new PayrollValidationService(repository.Object, Mock.Of<IPolicyRepository>())
            .ValidatePreCalculationAsync(1, "2026-11", 1);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static EmployeeExcelService Service(Employee employee, EmployeeImportRowDto row, out Mock<IEmployeeRepository> repository)
    {
        employee.DateOfBirth = new(1990, 1, 1); employee.NameLocal = "Employee"; employee.NameEn = "Employee"; employee.Nationality = "X";
        row.NameLocal = "Employee"; row.NameEn = "Employee"; row.Nationality = "X";
        repository = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetEmployeeByCodeAsync(employee.EmployeeCode, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<EmployeeImportRowDto>)new[] { row }, new EmployeeImportResultDto { TotalRows = 1 }));
        var unit = new Mock<IHrUnitOfWork>();
        unit.Setup(u => u.ExecuteEmployeeImportAsync(It.IsAny<IReadOnlyList<EmployeeImportRowDto>>(), It.IsAny<Func<Task<EmployeeImportResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<EmployeeImportRowDto> rows, Func<Task<EmployeeImportResultDto>> action, CancellationToken ct) => action());
        return new EmployeeExcelService(repository.Object, reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(), Mock.Of<ILogger<EmployeeExcelService>>(), unit.Object);
    }

    [Fact]
    public async Task NewSalaryPreservesHistoricalAmountAndCopiesAllowanceIntoIndependentLine()
    {
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        var previous = new SalaryStructure { Id = 10, EffectiveFrom = employee.HireDate, BasicSalary = 500m, CurrencyCode = "USD", PaymentMethod = "CASH" };
        previous.Lines.Add(new SalaryStructureLine { ComponentCode = "TRANSPORT", Amount = 25m });
        employee.SalaryStructures.Add(previous);
        var from = new DateTime(2026, 11, 1);
        var service = Service(employee, new EmployeeImportRowDto { EmployeeCode = "E1", NationalId = "N1", BasicSalary = 700m, SalaryEffectiveFrom = from }, out var repository);
        repository.Setup(r => r.GetSalaryComponentByCodeAsync("TRANSPORT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryComponent { ComponentCode = "TRANSPORT", ComponentType = "ALLOWANCE", CalculationType = "FIXED", IsActive = true });
        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);
        Assert.Empty(result.Errors);
        Assert.Equal(500m, previous.BasicSalary);
        Assert.Equal(from.AddDays(-1), previous.EffectiveTo);
        var replacement = Assert.Single(employee.SalaryStructures.Where(s => s != previous));
        Assert.Equal(700m, replacement.BasicSalary);
        Assert.Equal("USD", replacement.CurrencyCode);
        Assert.Equal("CASH", replacement.PaymentMethod);
        Assert.Equal(25m, Assert.Single(replacement.Lines).Amount);
        Assert.NotSame(previous.Lines[0], replacement.Lines[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedSalaryWithoutDateOrConflictingStartDoesNotRewriteOldSalary(bool conflictingStart)
    {
        var from = new DateTime(2024, 1, 1);
        var employee = new Employee { EmployeeCode = "E1", HireDate = from };
        var salary = new SalaryStructure { EffectiveFrom = from, BasicSalary = 500m };
        employee.SalaryStructures.Add(salary);
        var service = Service(employee, new EmployeeImportRowDto
        {
            EmployeeCode = "E1", NationalId = "N1", BasicSalary = 700m,
            SalaryEffectiveFrom = conflictingStart ? from : null
        }, out var repository);
        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);
        Assert.Contains(result.Errors, e => e.Code == (conflictingStart ? "SALARY_DATE_CONFLICT" : "SALARY_DATE_REQUIRED"));
        Assert.Equal(500m, salary.BasicSalary);
        Assert.Null(salary.EffectiveTo);
        Assert.Single(employee.SalaryStructures);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GeneratedTemplateMapsSalaryDateAndActiveFlagToCorrectColumns()
    {
        using var workbook = new MemoryStream(new XlsxEmployeeWorkbookWriter().WriteTemplateWorkbook());
        var (rows, result) = await new XlsxEmployeeWorkbookReader().ReadAsync(workbook);
        Assert.Empty(result.Errors);
        var row = Assert.Single(rows);
        Assert.Equal(750m, row.BasicSalary);
        Assert.Equal(new DateTime(2024, 1, 1), row.SalaryEffectiveFrom);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task ExportRoundTripPreservesSalaryPrecisionAndEffectiveDate()
    {
        var date = new DateTime(2026, 11, 1);
        var employee = new EmployeeExportRowDto
        {
            EmployeeCode = "E1", NameLocal = "موظف", NameEn = "Employee", NationalId = "1234567890",
            Nationality = "Jordanian", DateOfBirth = new DateTime(1990, 1, 1), HireDate = new DateTime(2024, 1, 1),
            BasicSalary = 750.123m, SalaryEffectiveFrom = date
        };
        using var workbook = new MemoryStream(new XlsxEmployeeWorkbookWriter().WriteExportWorkbook(new[] { employee }));
        var (rows, result) = await new XlsxEmployeeWorkbookReader().ReadAsync(workbook);
        Assert.Empty(result.Errors);
        Assert.Equal(750.123m, Assert.Single(rows).BasicSalary);
        Assert.Equal(date, rows[0].SalaryEffectiveFrom);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task Import_NegativeOrZeroBasicSalary_ReturnsInvalidSalaryError(decimal nonPositiveSalary)
    {
        var hireDate = new DateTime(2024, 1, 1);
        var employee = new Employee { EmployeeCode = "E1", HireDate = hireDate };
        var service = Service(employee, new EmployeeImportRowDto
        {
            EmployeeCode = "E1",
            NationalId = "N1",
            BasicSalary = nonPositiveSalary,
            SalaryEffectiveFrom = hireDate,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER"
        }, out var repository);

        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);

        Assert.Contains(result.Errors, e => e.Code == "INVALID_SALARY");
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_SalaryEffectiveDateBeforeHireDate_ReturnsSalaryBeforeHireError()
    {
        var hireDate = new DateTime(2024, 1, 1);
        var employee = new Employee { EmployeeCode = "E1", HireDate = hireDate };
        var service = Service(employee, new EmployeeImportRowDto
        {
            EmployeeCode = "E1",
            NationalId = "N1",
            BasicSalary = 800m,
            SalaryEffectiveFrom = new DateTime(2023, 12, 31), // Before hire!
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER"
        }, out var repository);

        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);

        Assert.Contains(result.Errors, e => e.Code == "SALARY_BEFORE_HIRE");
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_CurrencyWithoutBasicSalary_ReturnsSalaryRequiredError()
    {
        var hireDate = new DateTime(2024, 1, 1);
        var employee = new Employee { EmployeeCode = "E1", HireDate = hireDate };
        var service = Service(employee, new EmployeeImportRowDto
        {
            EmployeeCode = "E1",
            NationalId = "N1",
            BasicSalary = null, // No salary
            CurrencyCode = "JOD", // But currency supplied
            PaymentMethod = "BANK_TRANSFER"
        }, out var repository);

        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);

        Assert.Contains(result.Errors, e => e.Code == "SALARY_REQUIRED");
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_AttemptingToChangeCurrencyOnExistingSalary_ReturnsRequiresAssignmentError()
    {
        var hireDate = new DateTime(2024, 1, 1);
        var employee = new Employee { EmployeeCode = "E1", HireDate = hireDate };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = hireDate,
            BasicSalary = 500m,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER",
            IsActive = true
        });

        // Attempting to change currency from JOD to USD via excel import
        var service = Service(employee, new EmployeeImportRowDto
        {
            EmployeeCode = "E1",
            NationalId = "N1",
            BasicSalary = 600m,
            SalaryEffectiveFrom = new DateTime(2026, 11, 1),
            CurrencyCode = "USD",
            PaymentMethod = "BANK_TRANSFER"
        }, out var repository);

        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);

        Assert.Contains(result.Errors, e => e.Code == "SALARY_METADATA_CHANGE_REQUIRES_ASSIGNMENT");
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_AttemptingToChangePaymentMethodOnExistingSalary_ReturnsRequiresAssignmentError()
    {
        var hireDate = new DateTime(2024, 1, 1);
        var employee = new Employee { EmployeeCode = "E1", HireDate = hireDate };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = hireDate,
            BasicSalary = 500m,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER",
            IsActive = true
        });

        // Attempting to change payment method from BANK_TRANSFER to CASH via excel import
        var service = Service(employee, new EmployeeImportRowDto
        {
            EmployeeCode = "E1",
            NationalId = "N1",
            BasicSalary = 600m,
            SalaryEffectiveFrom = new DateTime(2026, 11, 1),
            CurrencyCode = "JOD",
            PaymentMethod = "CASH"
        }, out var repository);

        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);

        Assert.Contains(result.Errors, e => e.Code == "SALARY_METADATA_CHANGE_REQUIRES_ASSIGNMENT");
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_SameEffectiveDateAndSameSalary_PreservesExistingStructureWithoutError()
    {
        var hireDate = new DateTime(2024, 1, 1);
        var employee = new Employee { EmployeeCode = "E1", HireDate = hireDate };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = hireDate,
            BasicSalary = 500m,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER",
            IsActive = true
        });

        // Re-importing exact same structure (idempotent row update)
        var service = Service(employee, new EmployeeImportRowDto
        {
            EmployeeCode = "E1",
            NationalId = "N1",
            BasicSalary = 500m,
            SalaryEffectiveFrom = hireDate,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER"
        }, out var repository);

        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);

        Assert.Empty(result.Errors);
        Assert.Single(employee.SalaryStructures);
        Assert.Equal(500m, employee.SalaryStructures[0].BasicSalary);
    }
}
