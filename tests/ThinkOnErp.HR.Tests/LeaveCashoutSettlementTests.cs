using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class LeaveCashoutSettlementTests
{
    [Fact]
    public async Task Draft_UsesCompanyConfiguredBasicBasisAndStoresSourceSnapshot()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 4m, 2600m, "JOD");
        LeaveCashoutSettlement? saved = null;
        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveCashoutSettlement, CancellationToken>((batch, _) => { batch.Id = 91; saved = batch; })
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var result = await fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-91", 7, "2026-09", "jod"), "REVIEWER");

        var line = Assert.Single(result.Lines);
        Assert.Equal(91, result.Id);
        Assert.Equal("DRAFT", result.Status);
        Assert.Equal("JOD", result.CurrencyCode);
        Assert.Equal(2600m, line.WageBasisAmount);
        Assert.Equal(400m, line.CashoutAmount);
        Assert.Contains("structureId", line.BasisSnapshotJson);
        Assert.Equal("LEAVE_CASHOUT", result.PayComponentCode);
        Assert.NotNull(saved);
        Assert.Equal(64, saved!.PayloadHash.Length);
    }

    [Fact]
    public async Task Draft_GrossBasisSumsActiveEarningsAndRejectsPostedClose()
    {
        var fixture = CreateFixture("GROSS_EARNINGS", null, 26m, 2.5m, 2600m, "JOD");
        var structure = fixture.EmployeeStructure;
        structure.Lines.Add(new SalaryStructureLine { ComponentCode = "HOUSING", Amount = 100m, IsActive = true,
            Component = new SalaryComponent { ComponentCode = "HOUSING", ComponentType = "ALLOWANCE" } });
        structure.Lines.Add(new SalaryStructureLine { ComponentCode = "TRANSPORT", Percent = 10m, IsActive = true,
            Component = new SalaryComponent { ComponentCode = "TRANSPORT", ComponentType = "EARNING" } });
        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var result = await fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-gross", 7, "2026-09", "JOD"), "REVIEWER");
        var line = Assert.Single(result.Lines);
        Assert.Equal(2960m, line.WageBasisAmount);
        Assert.Equal(284.615m, line.CashoutAmount);

        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveYearClose { Id = 7, Status = "POSTED", Lines = [new LeaveYearCloseLine { Id = 17, EmployeeCode = "EMP1", LeaveTypeCode = "ANNUAL", CashoutDays = 2m }] });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-unposted", 7, "2026-09", "JOD"), "REVIEWER"));
    }

    [Fact]
    public async Task Draft_RetryIsIdempotentAndKeyCannotBeReusedForAnotherCurrency()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var existing = new LeaveCashoutSettlement
        {
            Id = 99, SettlementKey = "same-key", PayloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("7|2026-09|JOD"))),
            LeaveYearCloseId = 7, PayPeriod = "2026-09", Status = "DRAFT", WageBasisCode = "BASIC", DayDivisor = 26m,
            PayComponentCode = "LEAVE_CASHOUT", Lines = [new LeaveCashoutSettlementLine
            {
                EmployeeCode = "EMP1", LeaveTypeCode = "ANNUAL", CurrencyCode = "JOD", CashoutDays = 1m, CashoutAmount = 100m
            }]
        };
        fixture.Repository.Setup(r => r.GetLeaveCashoutSettlementByKeyAsync("same-key", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var retry = await fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("same-key", 7, "2026-09", "jod"), "REVIEWER");
        Assert.Equal(99, retry.Id);
        Assert.Equal(100m, retry.TotalAmount);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("same-key", 7, "2026-09", "USD"), "REVIEWER"));
    }

    [Fact]
    public async Task Draft_UsesSalaryComponentBasis()
    {
        var fixture = CreateFixture("SALARY_COMPONENT", "HOUSING", 30m, 3m, 2000m, "JOD");
        var structure = fixture.EmployeeStructure;
        structure.Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "HOUSING", Amount = 600m, IsActive = true,
            Component = new SalaryComponent { ComponentCode = "HOUSING", ComponentType = "ALLOWANCE" }
        });
        structure.Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "TRANSPORT", Amount = 150m, IsActive = true,
            Component = new SalaryComponent { ComponentCode = "TRANSPORT", ComponentType = "EARNING" }
        });
        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var result = await fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-comp", 7, "2026-09", "JOD"), "REVIEWER");

        var line = Assert.Single(result.Lines);
        Assert.Equal(600m, line.WageBasisAmount); // Exactly HOUSING amount, not 2000m basic or 2750m gross
        Assert.Equal(60m, line.CashoutAmount); // 3 * 600 / 30 = 60
    }

    [Fact]
    public async Task Draft_SalaryComponentBasis_ThrowsIfComponentNotFound()
    {
        var fixture = CreateFixture("SALARY_COMPONENT", "NON_EXISTENT", 30m, 3m, 2000m, "JOD");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-missing-comp", 7, "2026-09", "JOD"), "REVIEWER"));
        Assert.Contains("has no active earning line", ex.Message);
    }

    [Fact]
    public async Task Draft_ThrowsIfNoSettingsConfigured()
    {
        var fixture = CreateFixture("INVALID", null, 0m, 3m, 2000m, "JOD");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-no-settings", 7, "2026-09", "JOD"), "REVIEWER"));
        Assert.Contains("Configure the company leave cashout wage basis", ex.Message);
    }

    [Fact]
    public async Task Draft_ThrowsIfCloseNotFound()
    {
        var fixture = CreateFixture("BASIC", null, 30m, 3m, 2000m, "JOD");
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveYearClose?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-close-nf", 999, "2026-09", "JOD"), "REVIEWER"));
    }

    [Fact]
    public async Task Draft_ThrowsIfPeriodNotOpen()
    {
        var fixture = CreateFixture("BASIC", null, 30m, 3m, 2000m, "JOD");
        fixture.Repository.Setup(r => r.GetPayrollPeriodForLeaveCashoutAsync("2026-09", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollPeriod { PeriodCode = "2026-09", Status = "CLOSED" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-period-closed", 7, "2026-09", "JOD"), "REVIEWER"));
        Assert.Contains("require an open payroll period", ex.Message);
    }

    [Fact]
    public async Task Draft_ThrowsIfNoActiveSalaryStructure()
    {
        var fixture = CreateFixture("BASIC", null, 30m, 3m, 2000m, "JOD");
        var employeeRepo = new Mock<IEmployeeRepository>();
        employeeRepo.Setup(r => r.GetActiveSalaryStructureAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalaryStructure?)null);
        var service = new LeaveService(fixture.Repository.Object, employeeRepo.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-nostruct", 7, "2026-09", "JOD"), "REVIEWER"));
        Assert.Contains("No active salary structure applies to", ex.Message);
    }

    [Fact]
    public async Task Draft_ThrowsIfNoCashoutDaysForSelectedCurrency()
    {
        var fixture = CreateFixture("BASIC", null, 30m, 3m, 2000m, "JOD");

        // Requested currency is USD, but employee structure is JOD
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-diff-curr", 7, "2026-09", "USD"), "REVIEWER"));
        Assert.Contains("no cashout days for the selected currency", ex.Message);
    }

    [Theory]
    [InlineData(0, "key", "2026-09", "JOD")]
    [InlineData(-1, "key", "2026-09", "JOD")]
    public async Task Draft_RejectsInvalidCloseId(long closeId, string key, string period, string currency)
    {
        var fixture = CreateFixture("BASIC", null, 30m, 3m, 2000m, "JOD");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto(key, closeId, period, currency), "REVIEWER"));
    }

    [Theory]
    [InlineData(7, "", "2026-09", "JOD")]
    [InlineData(7, "   ", "2026-09", "JOD")]
    [InlineData(7, "key", "", "JOD")]
    [InlineData(7, "key", "2026-09", "")]
    [InlineData(7, "key", "2026-09", "JOD$")] // Special character not letter or digit
    public async Task Draft_RejectsInvalidInputParameters(long closeId, string key, string period, string currency)
    {
        var fixture = CreateFixture("BASIC", null, 30m, 3m, 2000m, "JOD");
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto(key, closeId, period, currency), "REVIEWER"));
    }

    [Fact]
    public async Task Get_ThrowsKeyNotFoundWhenSettlementNotFound()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        fixture.Repository.Setup(r => r.GetLeaveCashoutSettlementAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.GetLeaveCashoutSettlementAsync(999));
    }

    [Fact]
    public async Task Post_ReturnsPayrollAdjustmentReferencesAndAuditFields()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var posted = new LeaveCashoutSettlement
        {
            Id = 92, SettlementKey = "posted-92", Status = "POSTED", CreationUser = "REVIEWER",
            PostedBy = "APPROVER", PostedDate = DateTime.UtcNow,
            Lines = [new LeaveCashoutSettlementLine { LeaveYearCloseLineId = 17, EmployeeCode = "EMP1", LeaveTypeCode = "ANNUAL",
                CashoutDays = 1m, CashoutAmount = 100m, CurrencyCode = "JOD", PayrollAdjustmentId = 702 }]
        };
        fixture.Repository.Setup(r => r.PostLeaveCashoutSettlementAsync(92, "APPROVER", It.IsAny<CancellationToken>())).ReturnsAsync(posted);

        var result = await fixture.Service.PostLeaveCashoutSettlementAsync(92, "APPROVER");

        Assert.Equal("POSTED", result.Status);
        Assert.Equal("APPROVER", result.PostedBy);
        Assert.Equal(702, Assert.Single(result.Lines).PayrollAdjustmentId);
    }

    [Fact]
    public async Task Post_ThrowsKeyNotFoundWhenSettlementNotFound()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        fixture.Repository.Setup(r => r.PostLeaveCashoutSettlementAsync(999, "APPROVER", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.PostLeaveCashoutSettlementAsync(999, "APPROVER"));
    }

    [Fact]
    public async Task Reverse_RequiresReasonAndPreservesReversalAudit()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var reversed = new LeaveCashoutSettlement
        {
            Id = 93, SettlementKey = "reversed-93", Status = "REVERSED", ReversedBy = "REVIEWER",
            ReversedDate = DateTime.UtcNow, ReversalReason = "Payroll input correction"
        };
        fixture.Repository.Setup(r => r.ReverseLeaveCashoutSettlementAsync(93, "Payroll input correction", "REVIEWER", It.IsAny<CancellationToken>())).ReturnsAsync(reversed);

        var result = await fixture.Service.ReverseLeaveCashoutSettlementAsync(93, new ReverseLeaveCashoutSettlementDto(" Payroll input correction "), "REVIEWER");
        Assert.Equal("REVERSED", result.Status);
        Assert.Equal("Payroll input correction", result.ReversalReason);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.ReverseLeaveCashoutSettlementAsync(93, new ReverseLeaveCashoutSettlementDto(" "), "REVIEWER"));
    }

    [Fact]
    public async Task Reverse_ThrowsKeyNotFoundWhenSettlementNotFound()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        fixture.Repository.Setup(r => r.ReverseLeaveCashoutSettlementAsync(999, "Reason", "REVIEWER", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.ReverseLeaveCashoutSettlementAsync(999, new ReverseLeaveCashoutSettlementDto("Reason"), "REVIEWER"));
    }

    [Fact]
    public async Task Draft_ThrowsWhenCloseHasNoLines()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveYearClose { Id = 7, Status = "DRAFT", Lines = [] });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-no-lines", 7, "2026-09", "JOD"), "REVIEWER"));
        Assert.Contains("has no cashout days for the selected currency", ex.Message);
    }

    [Fact]
    public async Task Draft_ThrowsWhenAllCloseLinesHaveZeroCashoutDays()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 0m, 2600m, "JOD");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-zero-days", 7, "2026-09", "JOD"), "REVIEWER"));
        Assert.Contains("has no cashout days for the selected currency", ex.Message);
    }

    [Fact]
    public async Task Draft_SkipsLinesWithZeroOrNegativeCashoutDays()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 3m, 2600m, "JOD");
        var close = new LeaveYearClose
        {
            Id = 7, Status = "DRAFT", Lines =
            [
                new LeaveYearCloseLine { Id = 17, EmployeeCode = "EMP1", LeaveTypeCode = "ANNUAL", CashoutDays = 3m },
                new LeaveYearCloseLine { Id = 18, EmployeeCode = "EMP2", LeaveTypeCode = "ANNUAL", CashoutDays = 0m }
            ]
        };
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(close);
        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var result = await fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-skip-zero", 7, "2026-09", "JOD"), "REVIEWER");

        var line = Assert.Single(result.Lines);
        Assert.Equal("EMP1", line.EmployeeCode);
        Assert.Equal(3m, line.CashoutDays);
    }

    [Fact]
    public async Task Draft_HandlesMultipleEmployeesAndCalculatesTotalAmountAccurately()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 2m, 2600m, "JOD");
        var close = new LeaveYearClose
        {
            Id = 7, Status = "DRAFT", Lines =
            [
                new LeaveYearCloseLine { Id = 17, EmployeeCode = "EMP1", LeaveTypeCode = "ANNUAL", CashoutDays = 2m },
                new LeaveYearCloseLine { Id = 18, EmployeeCode = "EMP2", LeaveTypeCode = "ANNUAL", CashoutDays = 3m }
            ]
        };
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(close);
        var emp2Structure = new SalaryStructure { Id = 42, EmployeeCode = "EMP2", EffectiveFrom = new DateTime(2026, 1, 1), BasicSalary = 1300m, CurrencyCode = "JOD", IsActive = true };

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetActiveSalaryStructureAsync("EMP1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(fixture.EmployeeStructure);
        employees.Setup(r => r.GetActiveSalaryStructureAsync("EMP2", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(emp2Structure);
        employees.Setup(r => r.GetEmployeeByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => new Employee { EmployeeCode = code, IsActive = true, EmploymentStatus = "ACTIVE" });
        var service = new LeaveService(fixture.Repository.Object, employees.Object);

        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var result = await service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-multi-emp", 7, "2026-09", "JOD"), "REVIEWER");

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(350m, result.TotalAmount);
        Assert.Equal(5m, result.TotalCashoutDays);
    }

    [Fact]
    public async Task Draft_GrossBasis_IgnoresInactiveSalaryComponentLines()
    {
        var fixture = CreateFixture("GROSS_EARNINGS", null, 20m, 2m, 2000m, "JOD");
        var structure = fixture.EmployeeStructure;
        structure.Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "ACTIVE_ALLOWANCE", Amount = 500m, IsActive = true,
            Component = new SalaryComponent { ComponentCode = "ACTIVE_ALLOWANCE", ComponentType = "ALLOWANCE" }
        });
        structure.Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "INACTIVE_ALLOWANCE", Amount = 300m, IsActive = false,
            Component = new SalaryComponent { ComponentCode = "INACTIVE_ALLOWANCE", ComponentType = "ALLOWANCE" }
        });

        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var result = await fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-inactive-comp", 7, "2026-09", "JOD"), "REVIEWER");

        var line = Assert.Single(result.Lines);
        Assert.Equal(2500m, line.WageBasisAmount);
        Assert.Equal(250m, line.CashoutAmount);
    }

    [Fact]
    public async Task Draft_GrossBasis_IgnoresDeductionComponentLines()
    {
        var fixture = CreateFixture("GROSS_EARNINGS", null, 20m, 2m, 2000m, "JOD");
        var structure = fixture.EmployeeStructure;
        structure.Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "ALLOWANCE", Amount = 400m, IsActive = true,
            Component = new SalaryComponent { ComponentCode = "ALLOWANCE", ComponentType = "ALLOWANCE" }
        });
        structure.Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "DEDUCTION", Amount = 200m, IsActive = true,
            Component = new SalaryComponent { ComponentCode = "DEDUCTION", ComponentType = "DEDUCTION" }
        });

        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var result = await fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-deduct-comp", 7, "2026-09", "JOD"), "REVIEWER");

        var line = Assert.Single(result.Lines);
        Assert.Equal(2400m, line.WageBasisAmount);
        Assert.Equal(240m, line.CashoutAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Draft_ThrowsIfDivisorZeroOrNegative(decimal divisor)
    {
        var fixture = CreateFixture("BASIC", null, divisor, 2m, 2000m, "JOD");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-div-zero", 7, "2026-09", "JOD"), "REVIEWER"));
        Assert.Contains("Configure the company leave cashout wage basis", ex.Message);
    }

    [Fact]
    public async Task Draft_ThrowsWhenDtoIsNull()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(null!, "REVIEWER"));
    }

    [Fact]
    public async Task Reverse_ThrowsWhenDtoIsNull()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.ReverseLeaveCashoutSettlementAsync(93, null!, "REVIEWER"));
    }

    [Fact]
    public async Task Reverse_RejectsReasonLengthExceedingOneThousand()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var longReason = new string('X', 1001);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.ReverseLeaveCashoutSettlementAsync(93, new ReverseLeaveCashoutSettlementDto(longReason), "REVIEWER"));
    }

    [Theory]
    [InlineData(201, "2026-09", "JOD")]
    [InlineData(10, "PERIOD-CODE-EXCEEDING-FORTY-CHARACTERS-IN-LENGTH-HERE", "JOD")]
    [InlineData(10, "2026-09", "CURRENCY-EXCEEDING-TWENTY-CHARS")]
    public async Task Draft_RejectsExcessiveParameterLengths(int keyLength, string period, string currency)
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var key = new string('K', keyLength);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto(key, 7, period, currency), "REVIEWER"));
    }

    [Fact]
    public async Task Draft_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), ct))
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var result = await fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-ct", 7, "2026-09", "JOD"), "REVIEWER", ct);

        Assert.NotNull(result);
        fixture.Repository.Verify(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), ct), Times.Once);
    }

    [Fact]
    public async Task Reverse_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var reversed = new LeaveCashoutSettlement { Id = 93, Status = "REVERSED", Lines = new List<LeaveCashoutSettlementLine>() };
        fixture.Repository.Setup(r => r.ReverseLeaveCashoutSettlementAsync(93, "Audit check failed", "REVIEWER", ct))
            .ReturnsAsync(reversed);

        var result = await fixture.Service.ReverseLeaveCashoutSettlementAsync(93, new ReverseLeaveCashoutSettlementDto("Audit check failed"), "REVIEWER", ct);

        Assert.Equal("REVERSED", result.Status);
        fixture.Repository.Verify(r => r.ReverseLeaveCashoutSettlementAsync(93, "Audit check failed", "REVIEWER", ct), Times.Once);
    }

    [Fact]
    public async Task GetById_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var settlement = new LeaveCashoutSettlement { Id = 94, Status = "DRAFT", Lines = new List<LeaveCashoutSettlementLine>() };
        fixture.Repository.Setup(r => r.GetLeaveCashoutSettlementAsync(94, ct))
            .ReturnsAsync(settlement);

        var result = await fixture.Service.GetLeaveCashoutSettlementAsync(94, ct);

        Assert.Equal(94, result.Id);
        fixture.Repository.Verify(r => r.GetLeaveCashoutSettlementAsync(94, ct), Times.Once);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        fixture.Repository.Setup(r => r.GetLeaveCashoutSettlementAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.GetLeaveCashoutSettlementAsync(999));
    }

    [Fact]
    public async Task Post_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var posted = new LeaveCashoutSettlement { Id = 95, Status = "POSTED", Lines = new List<LeaveCashoutSettlementLine>() };
        fixture.Repository.Setup(r => r.PostLeaveCashoutSettlementAsync(95, "POSTER", ct))
            .ReturnsAsync(posted);

        var result = await fixture.Service.PostLeaveCashoutSettlementAsync(95, "POSTER", ct);

        Assert.Equal("POSTED", result.Status);
        fixture.Repository.Verify(r => r.PostLeaveCashoutSettlementAsync(95, "POSTER", ct), Times.Once);
    }

    [Fact]
    public async Task Post_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        fixture.Repository.Setup(r => r.PostLeaveCashoutSettlementAsync(999, "POSTER", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.PostLeaveCashoutSettlementAsync(999, "POSTER"));
    }

    [Fact]
    public async Task Draft_WhenMultipleEmployeesInYearClose_CalculatesEachLineCorrectly()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 2m, 2600m, "JOD");
        var close = new LeaveYearClose
        {
            Id = 7,
            Status = "DRAFT",
            Lines =
            [
                new LeaveYearCloseLine { Id = 17, EmployeeCode = "EMP1", LeaveTypeCode = "ANNUAL", CashoutDays = 2m },
                new LeaveYearCloseLine { Id = 18, EmployeeCode = "EMP2", LeaveTypeCode = "ANNUAL", CashoutDays = 3m }
            ]
        };
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(close);

        var structure2 = new SalaryStructure { Id = 42, EmployeeCode = "EMP2", EffectiveFrom = new DateTime(2026, 1, 1), BasicSalary = 1300m, CurrencyCode = "JOD", IsActive = true };

        // EMP1: 2600 basic / 26 * 2 days = 200m
        // EMP2: 1300 basic / 26 * 3 days = 150m
        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var employeeRepo = new Mock<IEmployeeRepository>();
        employeeRepo.Setup(r => r.GetActiveSalaryStructureAsync("EMP1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.EmployeeStructure);
        employeeRepo.Setup(r => r.GetActiveSalaryStructureAsync("EMP2", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(structure2);
        employeeRepo.Setup(r => r.GetEmployeeByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => new Employee { EmployeeCode = code, IsActive = true, EmploymentStatus = "ACTIVE" });

        var service = new LeaveService(fixture.Repository.Object, employeeRepo.Object);

        var result = await service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-multi", 7, "2026-09", "JOD"), "REVIEWER");

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(5m, result.TotalCashoutDays); // 2 + 3
        Assert.Equal(350m, result.TotalAmount); // 200 + 150
        Assert.Contains(result.Lines, l => l.EmployeeCode == "EMP1" && l.CashoutAmount == 200m);
        Assert.Contains(result.Lines, l => l.EmployeeCode == "EMP2" && l.CashoutAmount == 150m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99)]
    public async Task Draft_ThrowsArgumentOutOfRangeException_WhenCloseIdZeroOrNegative(long closeId)
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-invalid-close", closeId, "2026-09", "JOD"), "REVIEWER"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Draft_ThrowsArgumentException_WhenKeyIsNullOrWhitespace(string? key)
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto(key!, 7, "2026-09", "JOD"), "REVIEWER"));
        Assert.Contains("Settlement key is required", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Draft_ThrowsArgumentException_WhenPayPeriodIsNullOrWhitespace(string? payPeriod)
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("valid-key", 7, payPeriod!, "JOD"), "REVIEWER"));
        Assert.Contains("Pay period is required", ex.Message);
    }

    [Theory]
    [InlineData("JOD$")]
    [InlineData("USD 1")]
    [InlineData("JOD-")]
    [InlineData("US_D")]
    public async Task Draft_ThrowsArgumentException_WhenCurrencyContainsNonAsciiLettersOrDigits(string currency)
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("valid-key", 7, "2026-09", currency), "REVIEWER"));
        Assert.Contains("Currency code must contain only ASCII letters or digits", ex.Message);
    }

    [Fact]
    public async Task Reverse_AcceptsMinValidReasonLength_OneCharacter()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var reversed = new LeaveCashoutSettlement { Id = 93, Status = "REVERSED", Lines = [] };
        fixture.Repository.Setup(r => r.ReverseLeaveCashoutSettlementAsync(93, "X", "REVIEWER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversed);

        var result = await fixture.Service.ReverseLeaveCashoutSettlementAsync(93, new ReverseLeaveCashoutSettlementDto("X"), "REVIEWER");

        Assert.Equal("REVERSED", result.Status);
        fixture.Repository.Verify(r => r.ReverseLeaveCashoutSettlementAsync(93, "X", "REVIEWER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reverse_AcceptsMaxValidReasonLength_ExactOneThousandCharacters()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var reason = new string('R', 1000);
        var reversed = new LeaveCashoutSettlement { Id = 93, Status = "REVERSED", Lines = [] };
        fixture.Repository.Setup(r => r.ReverseLeaveCashoutSettlementAsync(93, reason, "REVIEWER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversed);

        var result = await fixture.Service.ReverseLeaveCashoutSettlementAsync(93, new ReverseLeaveCashoutSettlementDto(reason), "REVIEWER");

        Assert.Equal("REVERSED", result.Status);
        fixture.Repository.Verify(r => r.ReverseLeaveCashoutSettlementAsync(93, reason, "REVIEWER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reverse_TrimsWhitespaceAroundReason()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var reversed = new LeaveCashoutSettlement { Id = 93, Status = "REVERSED", Lines = [] };
        fixture.Repository.Setup(r => r.ReverseLeaveCashoutSettlementAsync(93, "Audit check failed", "REVIEWER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversed);

        var result = await fixture.Service.ReverseLeaveCashoutSettlementAsync(93, new ReverseLeaveCashoutSettlementDto("  Audit check failed  \t\n"), "REVIEWER");

        Assert.Equal("REVERSED", result.Status);
        fixture.Repository.Verify(r => r.ReverseLeaveCashoutSettlementAsync(93, "Audit check failed", "REVIEWER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reverse_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        fixture.Repository.Setup(r => r.ReverseLeaveCashoutSettlementAsync(999, "Reason", "REVIEWER", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Service.ReverseLeaveCashoutSettlementAsync(999, new ReverseLeaveCashoutSettlementDto("Reason"), "REVIEWER"));
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public async Task Draft_WhenNoActiveSalaryStructure_ThrowsInvalidOperationException()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "JOD");
        var employeeRepo = new Mock<IEmployeeRepository>();
        employeeRepo.Setup(r => r.GetActiveSalaryStructureAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalaryStructure?)null);
        var service = new LeaveService(fixture.Repository.Object, employeeRepo.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-no-structure", 7, "2026-09", "JOD"), "REVIEWER"));
        Assert.Contains("No active salary structure applies to", ex.Message);
    }

    [Fact]
    public async Task Draft_WhenNoCloseLinesMatchCurrency_ThrowsInvalidOperationException()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 1m, 2600m, "USD");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-diff-currency", 7, "2026-09", "EUR"), "REVIEWER"));
        Assert.Contains("The posted leave close has no cashout days for the selected currency.", ex.Message);
    }

    [Fact]
    public async Task Draft_SkipsCloseLinesWithZeroCashoutDays()
    {
        var fixture = CreateFixture("BASIC", null, 26m, 2m, 2600m, "JOD");
        var close = new LeaveYearClose
        {
            Id = 7,
            Status = "DRAFT",
            Lines =
            [
                new LeaveYearCloseLine { Id = 17, EmployeeCode = "EMP1", LeaveTypeCode = "ANNUAL", CashoutDays = 2m },
                new LeaveYearCloseLine { Id = 18, EmployeeCode = "EMP2", LeaveTypeCode = "ANNUAL", CashoutDays = 0m }
            ]
        };
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(close);
        fixture.Repository.Setup(r => r.SaveLeaveCashoutSettlementDraftAsync(It.IsAny<LeaveCashoutSettlement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveCashoutSettlement batch, CancellationToken _) => batch);

        var result = await fixture.Service.CreateLeaveCashoutSettlementDraftAsync(
            new CreateLeaveCashoutSettlementDto("key-skip-zero", 7, "2026-09", "JOD"), "REVIEWER");

        var line = Assert.Single(result.Lines);
        Assert.Equal("EMP1", line.EmployeeCode);
        Assert.Equal(2m, line.CashoutDays);
    }

    private static (Mock<ILeaveRepository> Repository, ILeaveService Service, SalaryStructure EmployeeStructure) CreateFixture(
        string basis, string? basisComponent, decimal divisor, decimal days, decimal basic, string currency)
    {
        var close = new LeaveYearClose
        {
            Id = 7, Status = "DRAFT", Lines = [new LeaveYearCloseLine
            {
                Id = 17, EmployeeCode = "EMP1", LeaveTypeCode = "ANNUAL", CashoutDays = days
            }]
        };
        var period = new PayrollPeriod { Id = 3, CompanyId = 1, PeriodCode = "2026-09", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30), Status = "OPEN" };
        var settings = new HrLeaveSettings
        {
            LeaveCashoutWageBasisCode = basis, LeaveCashoutBasisComponentCode = basisComponent,
            LeaveCashoutDayDivisor = divisor, LeaveCashoutPayComponentCode = "LEAVE_CASHOUT"
        };
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetLeaveCashoutSettlementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LeaveCashoutSettlement?)null);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        repository.Setup(r => r.GetLeaveYearCloseAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(close);
        repository.Setup(r => r.GetPayrollPeriodForLeaveCashoutAsync("2026-09", It.IsAny<CancellationToken>())).ReturnsAsync(period);

        var structure = new SalaryStructure { Id = 41, EmployeeCode = "EMP1", EffectiveFrom = new DateTime(2026, 1, 1), BasicSalary = basic, CurrencyCode = currency, IsActive = true };
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetActiveSalaryStructureAsync("EMP1", period.StartDate, It.IsAny<CancellationToken>())).ReturnsAsync(structure);
        employees.Setup(r => r.GetEmployeeByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string employeeCode, CancellationToken _) => new Employee
            {
                EmployeeCode = employeeCode,
                IsActive = true,
                EmploymentStatus = "ACTIVE"
            });
        return (repository, new LeaveService(repository.Object, employees.Object), structure);
    }
}
