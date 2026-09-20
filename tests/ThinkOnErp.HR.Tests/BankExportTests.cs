using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class BankExportTests
{
    private readonly Mock<IPayrollPeriodRepository> _payrollRepoMock;
    private readonly BankPayrollExportService _service;

    public BankExportTests()
    {
        _payrollRepoMock = new Mock<IPayrollPeriodRepository>();
        _service = new BankPayrollExportService(_payrollRepoMock.Object);
    }

    private PayrollRun CreateSampleRun()
    {
        var run = new PayrollRun
        {
            Id = 100,
            PayPeriod = "2026-09",
            Status = "APPROVED",
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    Id = 1,
                    EmployeeCode = "EMP001",
                    NetPay = 750.500m,
                    BankCode = "ARAB_BANK",
                    Iban = "JO94ARAB1234567890123456789012",
                    Employee = new Employee
                    {
                        EmployeeCode = "EMP001",
                        NameLocal = "أحمد محمد",
                        NationalId = "9901020304",
                        BankName = "ARAB_BANK",
                        BankIban = "JO94ARAB1234567890123456789012"
                    }
                },
                new()
                {
                    Id = 2,
                    EmployeeCode = "EMP002",
                    NetPay = 920.000m,
                    BankCode = "HOUSING_BANK",
                    Iban = "JO88THBK9876543210987654321098",
                    Employee = new Employee
                    {
                        EmployeeCode = "EMP002",
                        NameLocal = "سارة خليل",
                        NationalId = "9951020305",
                        BankName = "HOUSING_BANK",
                        BankIban = "JO88THBK9876543210987654321098"
                    }
                },
                new()
                {
                    Id = 3,
                    EmployeeCode = "EMP003",
                    NetPay = 0m, // Zero net pay - should be skipped from bank file
                    Employee = new Employee
                    {
                        EmployeeCode = "EMP003",
                        NameLocal = "خالد علي",
                        NationalId = "9881020306"
                    }
                }
            }
        };

        return run;
    }

    [Fact]
    public async Task ExportPayroll_Csv_ReturnsValidCsvFile()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "CSV");

        Assert.NotNull(result);
        Assert.Equal(100, result.PayrollRunId);
        Assert.Equal("2026-09", result.PayPeriod);
        Assert.Equal("CSV", result.Format);
        Assert.Equal(2, result.TotalRecords); // Only 2 with NetPay > 0
        Assert.Equal(1670.500m, result.TotalAmount);
        Assert.Equal("text/csv", result.ContentType);
        Assert.EndsWith(".csv", result.FileName);

        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("EMP001", content);
        Assert.Contains("EMP002", content);
        Assert.DoesNotContain("EMP003", content);
        Assert.Contains("JO94ARAB1234567890123456789012", content);
    }

    [Fact]
    public async Task ExportPayroll_Rawatebkom_ReturnsValidFixedFormat()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "RAWATEBKOM");

        Assert.NotNull(result);
        Assert.Equal("RAWATEBKOM", result.Format);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal("text/plain", result.ContentType);
        Assert.StartsWith("RAWATEBKOM_2026-09_", result.FileName);

        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("H|2026-09|2|1670.500|", content);
        Assert.Contains("D|1|EMP001|9901020304|أحمد محمد|ARAB_BANK|JO94ARAB1234567890123456789012|JOD|750.500|", content);
        Assert.Contains("T|2|1670.500", content);
    }

    [Fact]
    public async Task ExportPayroll_WpsSif_ReturnsValidSifFormat()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "WPS");

        Assert.NotNull(result);
        Assert.Equal("WPS", result.Format);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal("text/plain", result.ContentType);
        Assert.StartsWith("SIF_2026-09_", result.FileName);

        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("SCR|THINKON|CBJ|", content);
        Assert.Contains("EDR|EMP001|ARAB_BANK|JO94ARAB1234567890123456789012|2026-09|750.500|SALARY", content);
    }

    [Fact]
    public async Task ExportPayroll_NonExistentRun_ThrowsKeyNotFoundException()
    {
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ExportPayrollToBankFormatAsync(999, "CSV"));
    }
}
