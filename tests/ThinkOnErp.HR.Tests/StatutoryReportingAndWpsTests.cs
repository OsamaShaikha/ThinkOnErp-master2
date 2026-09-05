using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class StatutoryReportingAndWpsTests
{
    private readonly Mock<IPayrollRepository> _payrollRepoMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly StatutoryReportingService _reportingService;

    public StatutoryReportingAndWpsTests()
    {
        _reportingService = new StatutoryReportingService(
            _payrollRepoMock.Object,
            _employeeRepoMock.Object,
            NullLogger<StatutoryReportingService>.Instance);
    }

    [Fact]
    public async Task GenerateBankWpsPayrollFile_ApprovedPayrollRun_GeneratesValidFormatAndBalancedTotal()
    {
        // Arrange
        var run = new PayrollRun
        {
            Id = 100,
            PayPeriod = "2026-08",
            Status = "APPROVED",
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    PayrollRunId = 100,
                    EmployeeCode = "EMP-001",
                    GrossSalary = 1000.00m,
                    NetPay = 875.00m,
                    Iban = "JO00ARAB0000000000012345678900",
                    BankCode = "ARAB",
                    Employee = new Employee
                    {
                        EmployeeCode = "EMP-001",
                        NameEn = "Ahmad Al-Khalidi",
                        NationalId = "9901020304"
                    }
                },
                new()
                {
                    PayrollRunId = 100,
                    EmployeeCode = "EMP-002",
                    GrossSalary = 2000.00m,
                    NetPay = 1650.00m,
                    Iban = "JO00BOJO0000000000098765432100",
                    BankCode = "BOJO",
                    Employee = new Employee
                    {
                        EmployeeCode = "EMP-002",
                        NameEn = "Sarah Al-Husseini",
                        NationalId = "9921020304"
                    }
                }
            }
        };

        _payrollRepoMock.Setup(r => r.GetAllRunsAsync("2026-08", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollRun> { run });

        _payrollRepoMock.Setup(r => r.GetRunByIdAsync(100, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        // Act
        var wps = await _reportingService.GenerateBankWpsPayrollFileAsync("2026-08", "JO00CBJO0000000000000000000000", "CORP-01");

        // Assert
        wps.TotalRecordCount.Should().Be(2);
        wps.TotalDisbursementAmount.Should().Be(2525.00m); // 875 + 1650
        wps.RawFileContent.Should().Contain("H,CORP-01,JO00CBJO0000000000000000000000");
        wps.RawFileContent.Should().Contain("D,EMP-001");
        wps.RawFileContent.Should().Contain("D,EMP-002");
        wps.RawFileContent.Should().Contain("T,2,2525.000");
    }
}
