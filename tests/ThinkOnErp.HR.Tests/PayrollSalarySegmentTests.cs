using System.Text.Json;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollSalarySegmentTests
{
    [Theory]
    [InlineData("CALENDAR_DAYS", false, 600)]
    [InlineData("CALENDAR_DAYS", true, 560)]
    [InlineData("WORKING_DAYS", false, 600)]
    [InlineData("FIXED_30", false, 600, "CALENDAR_WEIGHTED")]
    [InlineData("FIXED_30", false, 600, "WORKING_WEIGHTED")]
    [InlineData("FIXED_30", false, 603.226, "CALENDAR_WEIGHTED", 75.484, 31)]
    public async Task SalaryAndAllowancesUseEachSegmentAndStatutoryCalculationsRunOnce(string method, bool unpaid, decimal expectedBasic,
        string allocation = "UNCONFIGURED", decimal expectedAllowance = 75m, int periodDays = 30)
    {
        var month = periodDays == 31 ? 10 : 11;
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11", StartDate = new DateTime(2026, month, 1), EndDate = new DateTime(2026, month, periodDays) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        var oldSalary = new SalaryStructure { EffectiveFrom = employee.HireDate, EffectiveTo = new DateTime(2026, month, 15), BasicSalary = 500m };
        var newSalary = new SalaryStructure { EffectiveFrom = new DateTime(2026, month, 16), BasicSalary = 700m };
        oldSalary.Lines.Add(new SalaryStructureLine { ComponentCode = "TRANSPORT", Amount = 60m });
        newSalary.Lines.Add(new SalaryStructureLine { ComponentCode = "TRANSPORT", Amount = 90m });
        employee.SalaryStructures.AddRange(new[] { oldSalary, newSalary });
        var policy = new ProrationPolicy { Method = method, SalarySegmentAllocation = allocation };
        var calendar = new Mock<IWorkCalendarService>();
        calendar.Setup(c => c.CountWorkingDaysAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long company, DateTime from, DateTime to, CancellationToken token) => from == period.StartDate && to == period.EndDate ? 20 : 10);
        var ssc = new Mock<ISSCCalculationService>();
        ssc.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(0, 0, 0, 0, 0, 0, 0, "TEST"));
        var tax = new Mock<ITaxCalculationEngine>();
        tax.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(0, 0, 0, 0, 0, 0, "TEST"));
        var loans = new Mock<ILoanDeductionService>();
        loans.Setup(l => l.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0, 0, 0, new(), new()));
        var leave = new Mock<ILeaveService>();
        leave.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync("E1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(unpaid ? 1m : 0m);
        // Recorded attendance overtime must not silently become payable earnings (PAY-15).
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDaysAsync("E1", period.StartDate, period.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AttendanceDay { AttendanceDate = period.StartDate, OvertimeHours = 5m,
                LastCheckOut = period.StartDate.AddHours(20) } });
        var overtime = new Mock<IOvertimeCalculationService>(MockBehavior.Strict);
        var engine = new PayrollCalculationEngine(Mock.Of<IPolicyRepository>(), new PayrollProrationService(Mock.Of<IPolicyRepository>(), calendar.Object),
            overtime.Object, ssc.Object, tax.Object, loans.Object, Mock.Of<IPayrollAdjustmentRepository>(), leave.Object, attendance.Object);
        var line = await engine.CalculateEmployeePayrollLineAsync(period, employee, policy, null, null, null, Array.Empty<PayrollAdjustment>());
        Assert.Equal(expectedBasic, line.BasicSalary);
        Assert.Equal(expectedAllowance, line.TotalEarnings);
        Assert.Equal(expectedBasic + expectedAllowance, line.NetPay);
        ssc.Verify(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>()), Times.Once);
        tax.Verify(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, 0, It.IsAny<TaxPolicy?>()), Times.Once);
        using var snapshot = JsonDocument.Parse(line.Snapshot!.ExplanationJson);
        Assert.Equal(0m, snapshot.RootElement.GetProperty("overtime").GetProperty("hours").GetDecimal());
        Assert.Equal(0m, snapshot.RootElement.GetProperty("overtime").GetProperty("earnings").GetDecimal());
        overtime.VerifyNoOtherCalls();
        Assert.Equal(2, snapshot.RootElement.GetProperty("salarySegments").GetArrayLength());
        var factorSum = snapshot.RootElement.GetProperty("salarySegments").EnumerateArray().Sum(s => s.GetProperty("factor").GetDecimal());
        Assert.Equal(1m, factorSum);
    }

    [Theory]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 10, 31)]
    public async Task FixedThirtyKeepsThirtyDayDenominatorForAFullMonth(int year, int month, int days)
    {
        var service = new PayrollProrationService(Mock.Of<IPolicyRepository>(), Mock.Of<IWorkCalendarService>());
        var result = await service.CalculateProrationFactorAsync(1, new DateTime(year, month, 1), new DateTime(year, month, days),
            new DateTime(2024, 1, 1), null, new ProrationPolicy { Method = "FIXED_30" });
        Assert.Equal(1m, result.ProrationFactor);
        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(30m, result.EligibleDays);
    }

    [Fact]
    public async Task FixedThirtyTransitionWithoutCompanySelectionIsRejected()
    {
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = employee.HireDate, EffectiveTo = new DateTime(2026, 11, 15) });
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 16) });
        var policies = new Mock<IPolicyRepository>();
        policies.Setup(p => p.GetActiveProrationPolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "FIXED_30" });
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(p => p.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) });
        periods.Setup(p => p.GetActiveEmployeesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { employee });
        var result = await new PayrollValidationService(periods.Object, policies.Object).ValidatePreCalculationAsync(1, "2026-11", 1);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("FIXED_30"));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(16)]
    public void SalaryCoverageRejectsGapAndOverlap(int oldSalaryEnd)
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { HireDate = period.StartDate };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, oldSalaryEnd) });
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 16) });
        Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
    }

    [Fact]
    public void Build_WhenSingleSegmentSpansEntirePeriod_ReturnsSingleClampedSegment()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        var salary = new SalaryStructure { EffectiveFrom = new DateTime(2025, 1, 1), EffectiveTo = null, BasicSalary = 1000m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        employee.SalaryStructures.Add(salary);

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Single(segments);
        Assert.Equal(period.StartDate, segments[0].Start);
        Assert.Equal(period.EndDate, segments[0].End);
        Assert.Same(salary, segments[0].Structure);
    }

    [Fact]
    public void Build_WhenMidPeriodHire_ClampsStartToHireDate()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var hireDate = new DateTime(2026, 11, 10);
        var employee = new Employee { EmployeeCode = "E1", HireDate = hireDate };
        var salary = new SalaryStructure { EffectiveFrom = hireDate, EffectiveTo = null, BasicSalary = 1200m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        employee.SalaryStructures.Add(salary);

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Single(segments);
        Assert.Equal(hireDate, segments[0].Start);
        Assert.Equal(period.EndDate, segments[0].End);
    }

    [Fact]
    public void Build_WhenMidPeriodTermination_ClampsEndToTerminationDate()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var termDate = new DateTime(2026, 11, 20);
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1), TerminationDate = termDate };
        var salary = new SalaryStructure { EffectiveFrom = new DateTime(2024, 1, 1), EffectiveTo = null, BasicSalary = 1000m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        employee.SalaryStructures.Add(salary);

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Single(segments);
        Assert.Equal(period.StartDate, segments[0].Start);
        Assert.Equal(termDate, segments[0].End);
    }

    [Fact]
    public void Build_WhenMidPeriodHireAndTermination_ClampsToBoth()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var hireDate = new DateTime(2026, 11, 5);
        var termDate = new DateTime(2026, 11, 25);
        var employee = new Employee { EmployeeCode = "E1", HireDate = hireDate, TerminationDate = termDate };
        var salary = new SalaryStructure { EffectiveFrom = hireDate, EffectiveTo = termDate, BasicSalary = 1500m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        employee.SalaryStructures.Add(salary);

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Single(segments);
        Assert.Equal(hireDate, segments[0].Start);
        Assert.Equal(termDate, segments[0].End);
    }

    [Fact]
    public void Build_WhenHiredAfterPeriodEnd_ReturnsEmptyList()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2026, 12, 1) };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = employee.HireDate, BasicSalary = 1000m, CurrencyCode = "JOD" });

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Empty(segments);
    }

    [Fact]
    public void Build_WhenTerminatedBeforePeriodStart_ReturnsEmptyList()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1), TerminationDate = new DateTime(2026, 10, 31) };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = employee.HireDate, BasicSalary = 1000m, CurrencyCode = "JOD" });

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Empty(segments);
    }

    [Fact]
    public void Build_WhenSalaryStructureInactive_IgnoresInactiveAndThrowsIfGap()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, 15), BasicSalary = 500m, IsActive = true, CurrencyCode = "JOD" });
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 16), BasicSalary = 700m, IsActive = false, CurrencyCode = "JOD" });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("no salary covering all eligible payroll dates", ex.Message);
    }

    [Fact]
    public void Build_WhenThreeConsecutiveSegmentsInPeriod_ReturnsAllThreeSegmentsSorted()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        var seg1 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 1), EffectiveTo = new DateTime(2026, 11, 10), BasicSalary = 500m, CurrencyCode = "JOD" };
        var seg2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 11), EffectiveTo = new DateTime(2026, 11, 20), BasicSalary = 600m, CurrencyCode = "JOD" };
        var seg3 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 21), EffectiveTo = new DateTime(2026, 11, 30), BasicSalary = 700m, CurrencyCode = "JOD" };
        employee.SalaryStructures.AddRange(new[] { seg2, seg3, seg1 });

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Equal(3, segments.Count);
        Assert.Equal(new DateTime(2026, 11, 1), segments[0].Start);
        Assert.Equal(new DateTime(2026, 11, 10), segments[0].End);
        Assert.Equal(new DateTime(2026, 11, 11), segments[1].Start);
        Assert.Equal(new DateTime(2026, 11, 20), segments[1].End);
        Assert.Equal(new DateTime(2026, 11, 21), segments[2].Start);
        Assert.Equal(new DateTime(2026, 11, 30), segments[2].End);
    }

    [Fact]
    public void Build_WhenGapAtBeginning_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 5), EffectiveTo = period.EndDate, BasicSalary = 800m, CurrencyCode = "JOD" });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("overlapping salary structures or a coverage gap", ex.Message);
    }

    [Fact]
    public void Build_WhenGapAtEnd_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, 25), BasicSalary = 800m, CurrencyCode = "JOD" });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("no salary covering all eligible payroll dates", ex.Message);
    }

    [Fact]
    public void Build_WhenMixedCurrenciesAcrossSegments_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, 15), BasicSalary = 500m, CurrencyCode = "JOD" });
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 16), BasicSalary = 700m, CurrencyCode = "USD" });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("Payroll cannot combine different currencies", ex.Message);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_InheritsDepartmentCodeAsCostCenterCode_WhenDepartmentCodePresent()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1), DepartmentCode = "HR_DEPT" };
        var salary = new SalaryStructure { EffectiveFrom = employee.HireDate, BasicSalary = 1000m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        salary.Lines.Add(new SalaryStructureLine { ComponentCode = "BASIC", Amount = 1000m, Component = new SalaryComponent { GlAccountCode = "GL_FALLBACK" } });
        employee.SalaryStructures.Add(salary);

        var calendar = new Mock<IWorkCalendarService>();
        var ssc = new Mock<ISSCCalculationService>();
        ssc.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>())).Returns(new SSCResult(0, 0, 0, 0, 0, 0, 0, "TEST"));
        var tax = new Mock<ITaxCalculationEngine>();
        tax.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, 0, It.IsAny<TaxPolicy?>())).Returns(new TaxResult(0, 0, 0, 0, 0, 0, "TEST"));
        var loans = new Mock<ILoanDeductionService>();
        loans.Setup(l => l.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0, 0, 0, new(), new()));
        var leave = new Mock<ILeaveService>();
        leave.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync("E1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDaysAsync("E1", period.StartDate, period.EndDate, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AttendanceDay>());
        var engine = new PayrollCalculationEngine(Mock.Of<IPolicyRepository>(), new PayrollProrationService(Mock.Of<IPolicyRepository>(), calendar.Object),
            Mock.Of<IOvertimeCalculationService>(), ssc.Object, tax.Object, loans.Object, Mock.Of<IPayrollAdjustmentRepository>(), leave.Object, attendance.Object);

        var line = await engine.CalculateEmployeePayrollLineAsync(period, employee, new ProrationPolicy { Method = "CALENDAR_DAYS" }, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal("HR_DEPT", line.DepartmentCode);
        Assert.Equal("HR_DEPT", line.CostCenterCode);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_FallsBackToGlAccountCode_WhenDepartmentCodeEmpty()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1), DepartmentCode = null };
        var salary = new SalaryStructure { EffectiveFrom = employee.HireDate, BasicSalary = 1000m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        salary.Lines.Add(new SalaryStructureLine { ComponentCode = "BASIC", Amount = 1000m, Component = new SalaryComponent { GlAccountCode = "GL_FALLBACK" } });
        employee.SalaryStructures.Add(salary);

        var calendar = new Mock<IWorkCalendarService>();
        var ssc = new Mock<ISSCCalculationService>();
        ssc.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>())).Returns(new SSCResult(0, 0, 0, 0, 0, 0, 0, "TEST"));
        var tax = new Mock<ITaxCalculationEngine>();
        tax.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, 0, It.IsAny<TaxPolicy?>())).Returns(new TaxResult(0, 0, 0, 0, 0, 0, "TEST"));
        var loans = new Mock<ILoanDeductionService>();
        loans.Setup(l => l.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0, 0, 0, new(), new()));
        var leave = new Mock<ILeaveService>();
        leave.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync("E1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDaysAsync("E1", period.StartDate, period.EndDate, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AttendanceDay>());
        var engine = new PayrollCalculationEngine(Mock.Of<IPolicyRepository>(), new PayrollProrationService(Mock.Of<IPolicyRepository>(), calendar.Object),
            Mock.Of<IOvertimeCalculationService>(), ssc.Object, tax.Object, loans.Object, Mock.Of<IPayrollAdjustmentRepository>(), leave.Object, attendance.Object);

        var line = await engine.CalculateEmployeePayrollLineAsync(period, employee, new ProrationPolicy { Method = "CALENDAR_DAYS" }, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Null(line.DepartmentCode);
        Assert.Equal("GL_FALLBACK", line.CostCenterCode);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_DetectsMixedPaymentMethodsAcrossSegments()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        var seg1 = new SalaryStructure { EffectiveFrom = employee.HireDate, EffectiveTo = new DateTime(2026, 11, 15), BasicSalary = 500m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        var seg2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 16), BasicSalary = 700m, CurrencyCode = "JOD", PaymentMethod = "CASH" };
        employee.SalaryStructures.AddRange(new[] { seg1, seg2 });

        var calendar = new Mock<IWorkCalendarService>();
        var ssc = new Mock<ISSCCalculationService>();
        ssc.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>())).Returns(new SSCResult(0, 0, 0, 0, 0, 0, 0, "TEST"));
        var tax = new Mock<ITaxCalculationEngine>();
        tax.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, 0, It.IsAny<TaxPolicy?>())).Returns(new TaxResult(0, 0, 0, 0, 0, 0, "TEST"));
        var loans = new Mock<ILoanDeductionService>();
        loans.Setup(l => l.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0, 0, 0, new(), new()));
        var leave = new Mock<ILeaveService>();
        leave.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync("E1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDaysAsync("E1", period.StartDate, period.EndDate, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AttendanceDay>());
        var engine = new PayrollCalculationEngine(Mock.Of<IPolicyRepository>(), new PayrollProrationService(Mock.Of<IPolicyRepository>(), calendar.Object),
            Mock.Of<IOvertimeCalculationService>(), ssc.Object, tax.Object, loans.Object, Mock.Of<IPayrollAdjustmentRepository>(), leave.Object, attendance.Object);

        var line = await engine.CalculateEmployeePayrollLineAsync(period, employee, new ProrationPolicy { Method = "CALENDAR_DAYS" }, null, null, null, Array.Empty<PayrollAdjustment>());

        using var snapshot = JsonDocument.Parse(line.Snapshot!.ExplanationJson);
        Assert.True(snapshot.RootElement.GetProperty("hasMixedPaymentMethods").GetBoolean());
    }

    [Fact]
    public void Build_WhenEmployeeTerminatedBeforePeriodStart_ReturnsEmptyList()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee
        {
            EmployeeCode = "E1",
            HireDate = new DateTime(2024, 1, 1),
            TerminationDate = new DateTime(2026, 10, 25) // Terminated in prior month
        };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = employee.HireDate, BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true });

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Empty(segments);
    }

    [Fact]
    public void Build_WhenEmployeeHiredAfterPeriodEnd_ReturnsEmptyList()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee
        {
            EmployeeCode = "E1",
            HireDate = new DateTime(2026, 12, 1) // Hired next month
        };
        employee.SalaryStructures.Add(new SalaryStructure { EffectiveFrom = employee.HireDate, BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true });

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Empty(segments);
    }

    [Fact]
    public void Build_WhenSingleDayPeriod_BuildsExactSegmentForThatDay()
    {
        var singleDate = new DateTime(2026, 11, 15);
        var period = new PayrollPeriod { PeriodCode = "2026-11-15", StartDate = singleDate, EndDate = singleDate };
        var employee = new Employee
        {
            EmployeeCode = "E1",
            HireDate = new DateTime(2024, 1, 1)
        };
        var structure = new SalaryStructure { EffectiveFrom = employee.HireDate, BasicSalary = 1200m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(structure);

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Single(segments);
        Assert.Equal(singleDate, segments[0].Start);
        Assert.Equal(singleDate, segments[0].End);
        Assert.Same(structure, segments[0].Structure);
    }

    [Fact]
    public void Build_WhenInactiveSalaryStructurePresent_IgnoresInactiveStructure()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        var activeStructure = new SalaryStructure { EffectiveFrom = employee.HireDate, BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        var inactiveStructure = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 10), EffectiveTo = new DateTime(2026, 11, 20), BasicSalary = 1500m, CurrencyCode = "JOD", IsActive = false };
        employee.SalaryStructures.AddRange(new[] { activeStructure, inactiveStructure });

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Single(segments);
        Assert.Same(activeStructure, segments[0].Structure);
        Assert.Equal(period.StartDate, segments[0].Start);
        Assert.Equal(period.EndDate, segments[0].End);
    }

    [Fact]
    public void Build_WhenOverlappingActiveStructures_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        var structure1 = new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, 20), BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        var structure2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 15), EffectiveTo = period.EndDate, BasicSalary = 1200m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.AddRange(new[] { structure1, structure2 });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("overlapping salary structures or a coverage gap", ex.Message);
    }

    [Fact]
    public void Build_WhenCoverageGapInsidePeriod_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        var structure1 = new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, 10), BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        var structure2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 15), EffectiveTo = period.EndDate, BasicSalary = 1200m, CurrencyCode = "JOD", IsActive = true }; // gap: 11-14
        employee.SalaryStructures.AddRange(new[] { structure1, structure2 });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("overlapping salary structures or a coverage gap", ex.Message);
    }

    [Fact]
    public void Build_WhenSalaryStartsAfterPeriodStart_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        var structure = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 5), EffectiveTo = period.EndDate, BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(structure);

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("overlapping salary structures or a coverage gap", ex.Message);
    }

    [Fact]
    public void Build_ClampsStructureDatesToEmploymentHireAndTermination()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee
        {
            EmployeeCode = "E1",
            HireDate = new DateTime(2026, 11, 10),
            TerminationDate = new DateTime(2026, 11, 20)
        };
        var structure = new SalaryStructure { EffectiveFrom = new DateTime(2024, 1, 1), BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(structure);

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Single(segments);
        Assert.Equal(new DateTime(2026, 11, 10), segments[0].Start);
        Assert.Equal(new DateTime(2026, 11, 20), segments[0].End);
    }

    [Fact]
    public void Build_WhenThreeSequentialSegmentsUniformCurrency_ReturnsAllThreeSegmentsOrdered()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        var s1 = new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, 10), BasicSalary = 600m, CurrencyCode = "JOD", IsActive = true };
        var s2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 11), EffectiveTo = new DateTime(2026, 11, 20), BasicSalary = 750m, CurrencyCode = "JOD", IsActive = true };
        var s3 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 21), EffectiveTo = period.EndDate, BasicSalary = 900m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.AddRange(new[] { s1, s2, s3 });

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Equal(3, segments.Count);
        Assert.Equal(new DateTime(2026, 11, 1), segments[0].Start);
        Assert.Equal(new DateTime(2026, 11, 10), segments[0].End);
        Assert.Equal(new DateTime(2026, 11, 11), segments[1].Start);
        Assert.Equal(new DateTime(2026, 11, 20), segments[1].End);
        Assert.Equal(new DateTime(2026, 11, 21), segments[2].Start);
        Assert.Equal(new DateTime(2026, 11, 30), segments[2].End);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WhenCurrencyIsMissingOrWhitespace_ThrowsInvalidOperationException(string? currency)
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        var structure = new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = period.EndDate, BasicSalary = 1000m, CurrencyCode = currency!, IsActive = true };
        employee.SalaryStructures.Add(structure);

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("currency", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_WhenEmployeeHiredAfterPeriodEnds_ReturnsEmpty()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2026, 12, 1) };
        var structure = new SalaryStructure { EffectiveFrom = new DateTime(2026, 12, 1), BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(structure);

        var segments = PayrollSalarySegments.Build(employee, period);
        Assert.Empty(segments);
    }

    [Fact]
    public void Build_WhenEmployeeTerminatedBeforePeriodStarts_ReturnsEmpty()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee
        {
            EmployeeCode = "E1",
            HireDate = new DateTime(2025, 1, 1),
            TerminationDate = new DateTime(2026, 10, 31)
        };
        var structure = new SalaryStructure { EffectiveFrom = new DateTime(2025, 1, 1), BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(structure);

        var segments = PayrollSalarySegments.Build(employee, period);
        Assert.Empty(segments);
    }

    [Fact]
    public void Build_WhenEmployeeHiredMidPeriod_SegmentStartsWithHireDate()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2026, 11, 10) };
        var structure = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 10), EffectiveTo = new DateTime(2026, 11, 30), BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(structure);

        var segments = PayrollSalarySegments.Build(employee, period);
        var segment = Assert.Single(segments);
        Assert.Equal(new DateTime(2026, 11, 10), segment.Start);
        Assert.Equal(new DateTime(2026, 11, 30), segment.End);
    }

    [Fact]
    public void Build_WhenEmployeeTerminatedMidPeriod_SegmentEndsWithTerminationDate()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee
        {
            EmployeeCode = "E1",
            HireDate = new DateTime(2025, 1, 1),
            TerminationDate = new DateTime(2026, 11, 20)
        };
        var structure = new SalaryStructure { EffectiveFrom = new DateTime(2025, 1, 1), EffectiveTo = new DateTime(2026, 11, 20), BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(structure);

        var segments = PayrollSalarySegments.Build(employee, period);
        var segment = Assert.Single(segments);
        Assert.Equal(new DateTime(2026, 11, 1), segment.Start);
        Assert.Equal(new DateTime(2026, 11, 20), segment.End);
    }

    [Fact]
    public void Build_WhenEmployeeHiredAndTerminatedInSamePeriod_SegmentCoversEmploymentWindow()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee
        {
            EmployeeCode = "E1",
            HireDate = new DateTime(2026, 11, 5),
            TerminationDate = new DateTime(2026, 11, 25)
        };
        var structure = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 5), EffectiveTo = new DateTime(2026, 11, 25), BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(structure);

        var segments = PayrollSalarySegments.Build(employee, period);
        var segment = Assert.Single(segments);
        Assert.Equal(new DateTime(2026, 11, 5), segment.Start);
        Assert.Equal(new DateTime(2026, 11, 25), segment.End);
    }

    [Fact]
    public void Build_WhenMultipleSegmentsWithDifferentCurrencies_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        var structure1 = new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, 15), BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        var structure2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 16), EffectiveTo = period.EndDate, BasicSalary = 1500m, CurrencyCode = "USD", IsActive = true };
        employee.SalaryStructures.Add(structure1);
        employee.SalaryStructures.Add(structure2);

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("combine different currencies", ex.Message);
    }

    [Fact]
    public void Build_WhenInactiveStructurePresent_IgnoresInactiveStructure()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        var inactiveStructure = new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = period.EndDate, BasicSalary = 500m, CurrencyCode = "JOD", IsActive = false };
        var activeStructure = new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = period.EndDate, BasicSalary = 1000m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(inactiveStructure);
        employee.SalaryStructures.Add(activeStructure);

        var segments = PayrollSalarySegments.Build(employee, period);
        var segment = Assert.Single(segments);
        Assert.Equal(1000m, segment.Structure.BasicSalary);
    }

    [Fact]
    public void Build_WhenSalaryStructureHasEffectiveToBeforePeriod_IgnoresPastStructure()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2025, 1, 1) };
        var pastStructure = new SalaryStructure { EffectiveFrom = new DateTime(2025, 1, 1), EffectiveTo = new DateTime(2026, 5, 31), BasicSalary = 800m, CurrencyCode = "JOD", IsActive = true };
        var currentStructure = new SalaryStructure { EffectiveFrom = new DateTime(2026, 6, 1), EffectiveTo = null, BasicSalary = 1200m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(pastStructure);
        employee.SalaryStructures.Add(currentStructure);

        var segments = PayrollSalarySegments.Build(employee, period);
        var segment = Assert.Single(segments);
        Assert.Equal(1200m, segment.Structure.BasicSalary);
        Assert.Equal(new DateTime(2026, 11, 1), segment.Start);
        Assert.Equal(new DateTime(2026, 11, 30), segment.End);
    }

    [Fact]
    public void Build_WhenSingleDayEmployment_AndSingleDayPeriod_BuildsExactSingleDaySegment()
    {
        var singleDate = new DateTime(2026, 11, 15);
        var period = new PayrollPeriod { PeriodCode = "2026-11-15", StartDate = singleDate, EndDate = singleDate };
        var employee = new Employee
        {
            EmployeeCode = "E1",
            HireDate = singleDate,
            TerminationDate = singleDate
        };
        var structure = new SalaryStructure { EffectiveFrom = singleDate, EffectiveTo = singleDate, BasicSalary = 1200m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.Add(structure);

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Single(segments);
        Assert.Equal(singleDate, segments[0].Start);
        Assert.Equal(singleDate, segments[0].End);
        Assert.Same(structure, segments[0].Structure);
    }

    [Fact]
    public void Build_WhenCurrencyCodeHasWhitespaceAndMixedCase_PassesSingleCurrencyCheck_BecauseNormalized()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        var seg1 = new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, 10), BasicSalary = 500m, CurrencyCode = "  jod  ", IsActive = true };
        var seg2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 11), EffectiveTo = new DateTime(2026, 11, 20), BasicSalary = 600m, CurrencyCode = "JOD", IsActive = true };
        var seg3 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 21), EffectiveTo = period.EndDate, BasicSalary = 700m, CurrencyCode = " Jod ", IsActive = true };
        employee.SalaryStructures.AddRange(new[] { seg1, seg2, seg3 });

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Equal(3, segments.Count);
        Assert.Equal(new DateTime(2026, 11, 1), segments[0].Start);
        Assert.Equal(new DateTime(2026, 11, 10), segments[0].End);
        Assert.Equal(new DateTime(2026, 11, 11), segments[1].Start);
        Assert.Equal(new DateTime(2026, 11, 20), segments[1].End);
        Assert.Equal(new DateTime(2026, 11, 21), segments[2].Start);
        Assert.Equal(new DateTime(2026, 11, 30), segments[2].End);
    }

    [Fact]
    public void Build_WhenEmployeeHasNoSalaryStructures_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("no salary covering all eligible payroll dates", ex.Message);
    }

    [Fact]
    public void Build_WhenAllSalaryStructuresInactive_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        var inactive1 = new SalaryStructure { EffectiveFrom = period.StartDate, EffectiveTo = new DateTime(2026, 11, 15), BasicSalary = 500m, CurrencyCode = "JOD", IsActive = false };
        var inactive2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 16), EffectiveTo = period.EndDate, BasicSalary = 700m, CurrencyCode = "JOD", IsActive = false };
        employee.SalaryStructures.AddRange(new[] { inactive1, inactive2 });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("no salary covering all eligible payroll dates", ex.Message);
    }

    [Fact]
    public void Build_WhenPastStructureEndsBeforePeriod_AndFutureStructureStartsAfterPeriod_ThrowsCoverageGap()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2025, 1, 1) };
        var past = new SalaryStructure { EffectiveFrom = new DateTime(2025, 1, 1), EffectiveTo = new DateTime(2026, 10, 31), BasicSalary = 500m, CurrencyCode = "JOD", IsActive = true };
        var future = new SalaryStructure { EffectiveFrom = new DateTime(2026, 12, 1), EffectiveTo = null, BasicSalary = 700m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.AddRange(new[] { past, future });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("no salary covering all eligible payroll dates", ex.Message);
    }

    [Fact]
    public void Build_FourConsecutiveSegmentsInPeriod_ReturnsAllFourSortedWithExactBoundaries()
    {
        var period = new PayrollPeriod { PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = period.StartDate };
        var s1 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 1), EffectiveTo = new DateTime(2026, 11, 7), BasicSalary = 400m, CurrencyCode = "JOD", IsActive = true };
        var s2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 8), EffectiveTo = new DateTime(2026, 11, 15), BasicSalary = 500m, CurrencyCode = "JOD", IsActive = true };
        var s3 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 16), EffectiveTo = new DateTime(2026, 11, 23), BasicSalary = 600m, CurrencyCode = "JOD", IsActive = true };
        var s4 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 24), EffectiveTo = new DateTime(2026, 11, 30), BasicSalary = 700m, CurrencyCode = "JOD", IsActive = true };
        employee.SalaryStructures.AddRange(new[] { s4, s1, s3, s2 });

        var segments = PayrollSalarySegments.Build(employee, period);

        Assert.Equal(4, segments.Count);
        Assert.Equal(new DateTime(2026, 11, 1), segments[0].Start);
        Assert.Equal(new DateTime(2026, 11, 7), segments[0].End);
        Assert.Equal(400m, segments[0].Structure.BasicSalary);

        Assert.Equal(new DateTime(2026, 11, 8), segments[1].Start);
        Assert.Equal(new DateTime(2026, 11, 15), segments[1].End);
        Assert.Equal(500m, segments[1].Structure.BasicSalary);

        Assert.Equal(new DateTime(2026, 11, 16), segments[2].Start);
        Assert.Equal(new DateTime(2026, 11, 23), segments[2].End);
        Assert.Equal(600m, segments[2].Structure.BasicSalary);

        Assert.Equal(new DateTime(2026, 11, 24), segments[3].Start);
        Assert.Equal(new DateTime(2026, 11, 30), segments[3].End);
        Assert.Equal(700m, segments[3].Structure.BasicSalary);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WithThreeSalarySegments_ApportionsAndRoundsCorrectly()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1) };
        var seg1 = new SalaryStructure { EffectiveFrom = employee.HireDate, EffectiveTo = new DateTime(2026, 11, 10), BasicSalary = 300m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        var seg2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 11), EffectiveTo = new DateTime(2026, 11, 20), BasicSalary = 600m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        var seg3 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 21), BasicSalary = 900m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        employee.SalaryStructures.AddRange(new[] { seg1, seg2, seg3 });

        var calendar = new Mock<IWorkCalendarService>();
        var ssc = new Mock<ISSCCalculationService>();
        ssc.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>())).Returns(new SSCResult(0, 0, 0, 0, 0, 0, 0, "TEST"));
        var tax = new Mock<ITaxCalculationEngine>();
        tax.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, 0, It.IsAny<TaxPolicy?>())).Returns(new TaxResult(0, 0, 0, 0, 0, 0, "TEST"));
        var loans = new Mock<ILoanDeductionService>();
        loans.Setup(l => l.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0, 0, 0, new(), new()));
        var leave = new Mock<ILeaveService>();
        leave.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync("E1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDaysAsync("E1", period.StartDate, period.EndDate, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AttendanceDay>());
        var engine = new PayrollCalculationEngine(Mock.Of<IPolicyRepository>(), new PayrollProrationService(Mock.Of<IPolicyRepository>(), calendar.Object),
            Mock.Of<IOvertimeCalculationService>(), ssc.Object, tax.Object, loans.Object, Mock.Of<IPayrollAdjustmentRepository>(), leave.Object, attendance.Object);

        var line = await engine.CalculateEmployeePayrollLineAsync(period, employee, new ProrationPolicy { Method = "CALENDAR_DAYS" }, null, null, null, Array.Empty<PayrollAdjustment>());

        // 300 * 0.333333 + 600 * 0.333333 + 900 * 0.333333 = 1800 * 0.333333 = 599.999
        Assert.Equal(599.999m, line.BasicSalary);
        Assert.Equal(599.999m, line.NetPay);

        using var snapshot = JsonDocument.Parse(line.Snapshot!.ExplanationJson);
        var segmentsArray = snapshot.RootElement.GetProperty("salarySegments");
        Assert.Equal(3, segmentsArray.GetArrayLength());
        var sumFactors = segmentsArray.EnumerateArray().Sum(s => s.GetProperty("factor").GetDecimal());
        Assert.Equal(0.999999m, sumFactors);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_InheritsCostCenterCode_FromActiveStructureGlAccountCode_WhenDepartmentCodeNull()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1), DepartmentCode = null };
        var seg1 = new SalaryStructure { EffectiveFrom = employee.HireDate, EffectiveTo = new DateTime(2026, 11, 15), BasicSalary = 500m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        seg1.Lines.Add(new SalaryStructureLine { ComponentCode = "BASIC", Amount = 500m, Component = new SalaryComponent { GlAccountCode = "SEG1_GL" } });
        var seg2 = new SalaryStructure { EffectiveFrom = new DateTime(2026, 11, 16), BasicSalary = 700m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        seg2.Lines.Add(new SalaryStructureLine { ComponentCode = "BASIC", Amount = 700m, Component = new SalaryComponent { GlAccountCode = "SEG2_GL" } });
        employee.SalaryStructures.AddRange(new[] { seg1, seg2 });

        var calendar = new Mock<IWorkCalendarService>();
        var ssc = new Mock<ISSCCalculationService>();
        ssc.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>())).Returns(new SSCResult(0, 0, 0, 0, 0, 0, 0, "TEST"));
        var tax = new Mock<ITaxCalculationEngine>();
        tax.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, 0, It.IsAny<TaxPolicy?>())).Returns(new TaxResult(0, 0, 0, 0, 0, 0, "TEST"));
        var loans = new Mock<ILoanDeductionService>();
        loans.Setup(l => l.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0, 0, 0, new(), new()));
        var leave = new Mock<ILeaveService>();
        leave.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync("E1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDaysAsync("E1", period.StartDate, period.EndDate, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AttendanceDay>());
        var engine = new PayrollCalculationEngine(Mock.Of<IPolicyRepository>(), new PayrollProrationService(Mock.Of<IPolicyRepository>(), calendar.Object),
            Mock.Of<IOvertimeCalculationService>(), ssc.Object, tax.Object, loans.Object, Mock.Of<IPayrollAdjustmentRepository>(), leave.Object, attendance.Object);

        var line = await engine.CalculateEmployeePayrollLineAsync(period, employee, new ProrationPolicy { Method = "CALENDAR_DAYS" }, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal("SEG2_GL", line.CostCenterCode);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WhenDepartmentAndGlAccountBothNull_CostCenterCodeIsNull()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2024, 1, 1), DepartmentCode = null };
        var seg1 = new SalaryStructure { EffectiveFrom = employee.HireDate, BasicSalary = 500m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER" };
        seg1.Lines.Add(new SalaryStructureLine { ComponentCode = "BASIC", Amount = 500m, Component = new SalaryComponent { GlAccountCode = null } });
        employee.SalaryStructures.Add(seg1);

        var calendar = new Mock<IWorkCalendarService>();
        var ssc = new Mock<ISSCCalculationService>();
        ssc.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>())).Returns(new SSCResult(0, 0, 0, 0, 0, 0, 0, "TEST"));
        var tax = new Mock<ITaxCalculationEngine>();
        tax.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, 0, It.IsAny<TaxPolicy?>())).Returns(new TaxResult(0, 0, 0, 0, 0, 0, "TEST"));
        var loans = new Mock<ILoanDeductionService>();
        loans.Setup(l => l.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0, 0, 0, new(), new()));
        var leave = new Mock<ILeaveService>();
        leave.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync("E1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDaysAsync("E1", period.StartDate, period.EndDate, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AttendanceDay>());
        var engine = new PayrollCalculationEngine(Mock.Of<IPolicyRepository>(), new PayrollProrationService(Mock.Of<IPolicyRepository>(), calendar.Object),
            Mock.Of<IOvertimeCalculationService>(), ssc.Object, tax.Object, loans.Object, Mock.Of<IPayrollAdjustmentRepository>(), leave.Object, attendance.Object);

        var line = await engine.CalculateEmployeePayrollLineAsync(period, employee, new ProrationPolicy { Method = "CALENDAR_DAYS" }, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Null(line.CostCenterCode);
    }
}
