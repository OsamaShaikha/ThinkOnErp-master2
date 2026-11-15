using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed partial class HrAttendanceHttpTests
{
    [Fact]
    public async Task OvertimePolicyHttpRequiresCompanyPermissionAndUsesEffectiveCompanyRule()
    {
        var dto = new OvertimeRuleDto(1, "OT_WEEKEND", "إضافي أسبوعي", "Weekend overtime",
            new DateTime(2026, 1, 1), null, " weekend ", 1.75m, 45, 180,
            "MONTHLY_SALARY / 200", true);

        using (var denied = await Send("POST", "/api/hr/payroll-policies/overtime", dto))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysScreen { Id = 997, SystemId = 50, ScreenCode = "hr-payroll-policies", IsActive = true },
                new SysScreenFeature { ScreenId = 997, FeatureId = 61 },
                new SysScreenFeature { ScreenId = 997, FeatureId = 62 },
                new SysUserScreenPermission { UserId = 12, BranchId = 7, ScreenId = 997, FeatureId = 61, IsGranted = true },
                new SysUserScreenPermission { UserId = 12, BranchId = 7, ScreenId = 997, FeatureId = 62, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using (var created = await Send("POST", "/api/hr/payroll-policies/overtime", dto))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            var result = await Data(created);
            Assert.Equal("WEEKEND", result.GetProperty("dayType").GetString());
            Assert.Equal("MOBILE_USER", result.GetProperty("creationUser").GetString());
        }

        using (var listed = await Send("GET", "/api/hr/payroll-policies/overtime?companyId=1&effectiveDate=2026-11-12"))
        {
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            var rules = await Data(listed);
            var rule = Assert.Single(rules.EnumerateArray());
            Assert.Equal("OT_WEEKEND", rule.GetProperty("code").GetString());
            Assert.Equal("MONTHLY_SALARY / 200", rule.GetProperty("hourlyDivisorFormula").GetString());
        }

        using var secondCompany = new OracleDbContext(Options(2));
        Assert.Empty(await secondCompany.OvertimeRules.ToListAsync());
    }

    [Fact]
    public async Task PayrollApprovalHttpRequiresBranchGrantAndDispatchesBoundActor()
    {
        await SeedPayroll();
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysScreenFeature { ScreenId = 52, FeatureId = 72 },
                new SysUserScreenPermission { UserId = 12, BranchId = 7, ScreenId = 52, FeatureId = 72, IsGranted = true });
            await db.SaveChangesAsync();
        }

        var payroll = new Mock<IPayrollService>();
        payroll.Setup(service => service.ApprovePayrollRunAsync(44, "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollRun { Id = 44, Status = "APPROVED", ApprovedBy = "MOBILE_USER" });
        payrollServiceFactory = _ => payroll.Object;

        using var response = await Send("POST", "/api/hr/payroll/44/approve");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        payroll.Verify(service => service.ApprovePayrollRunAsync(44, "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 8)]
    public async Task PayrollApprovalHttpRejectsMissingOrOutOfBranchGrant(bool isGranted, long grantBranch)
    {
        await SeedPayroll();
        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysScreenFeature { ScreenId = 52, FeatureId = 72 });
            db.Add(new SysUserScreenPermission
            {
                UserId = 12, BranchId = grantBranch, ScreenId = 52, FeatureId = 72, IsGranted = isGranted
            });
            await db.SaveChangesAsync();
        }

        var payroll = new Mock<IPayrollService>();
        payrollServiceFactory = _ => payroll.Object;

        using var response = await Send("POST", "/api/hr/payroll/44/approve");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        payroll.Verify(service => service.ApprovePayrollRunAsync(It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PayrollApprovalHttpUsesRealServiceAndRequestsHandoffAfterApprovalIsSaved()
    {
        await SeedPayroll();
        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysScreenFeature { ScreenId = 52, FeatureId = 72 });
            db.Add(new SysUserScreenPermission
            {
                UserId = 12, BranchId = 7, ScreenId = 52, FeatureId = 72, IsGranted = true
            });
            await db.SaveChangesAsync();
        }

        var run = new PayrollRun
        {
            Id = 44, BranchId = 7, PayPeriod = "P26", Status = "CALCULATED",
            TotalGrossSalary = 100m, TotalNetSalary = 100m
        };
        var period = new PayrollPeriod
        {
            Id = 100, CompanyId = 1, PeriodCode = "P26", Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30)
        };
        var events = new List<string>();
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPayrollRunSnapshotAsync(44, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPayrollRunByIdAsync(44, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.UpdatePayrollRunAsync(run, It.IsAny<CancellationToken>()))
            .Callback(() => events.Add("updated")).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => events.Add("saved")).Returns(Task.CompletedTask);

        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePostApprovalAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new(), new()));
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(1, "P26",
                It.IsAny<Func<Task<PayrollRun>>>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, Func<Task<PayrollRun>>, CancellationToken>((_, _, action, _) => action());
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(h => h.CreateApprovedPayrollHandoffAsync(44, "MOBILE_USER", It.IsAny<CancellationToken>()))
            .Callback(() => events.Add("handoff"))
            .ReturnsAsync(new HrAccountingHandoff { Id = 90, SourceType = "PAYROLL", SourceId = "44" });

        accountingHandoffServiceOverride = handoffs.Object;
        payrollServiceFactory = sp => new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(),
            validation.Object, unitOfWork.Object, accountingHandoffs: sp.GetRequiredService<IHrAccountingHandoffService>());

        using var response = await Send("POST", "/api/hr/payroll/44/approve");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("APPROVED", run.Status);
        Assert.Equal("MOBILE_USER", run.ApprovedBy);
        Assert.Null(run.JournalVoucherId);
        Assert.Equal(new[] { "updated", "saved", "handoff" }, events);
        unitOfWork.Verify(u => u.ExecuteForPayrollPeriodAsync(1, "P26",
            It.IsAny<Func<Task<PayrollRun>>>(), It.IsAny<CancellationToken>()), Times.Once);
        handoffs.Verify(h => h.CreateApprovedPayrollHandoffAsync(44, "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PayrollApprovalHttpValidationFailureLeavesRunUnchangedAndDoesNotCreateHandoff()
    {
        await SeedPayroll();
        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysScreenFeature { ScreenId = 52, FeatureId = 72 });
            db.Add(new SysUserScreenPermission
            {
                UserId = 12, BranchId = 7, ScreenId = 52, FeatureId = 72, IsGranted = true
            });
            await db.SaveChangesAsync();
        }

        var run = new PayrollRun { Id = 44, BranchId = 7, PayPeriod = "P26", Status = "CALCULATED" };
        var period = new PayrollPeriod { Id = 100, CompanyId = 1, PeriodCode = "P26", Status = "OPEN" };
        var repository = new Mock<IPayrollPeriodRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetPayrollRunSnapshotAsync(44, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePostApprovalAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(false, new() { "Payroll totals do not match." }, new()));
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(1, "P26",
                It.IsAny<Func<Task<PayrollRun>>>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, Func<Task<PayrollRun>>, CancellationToken>((_, _, action, _) => action());
        var handoffs = new Mock<IHrAccountingHandoffService>(MockBehavior.Strict);
        accountingHandoffServiceOverride = handoffs.Object;
        payrollServiceFactory = sp => new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(),
            validation.Object, unitOfWork.Object, accountingHandoffs: sp.GetRequiredService<IHrAccountingHandoffService>());

        using var response = await Send("POST", "/api/hr/payroll/44/approve");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Payroll totals do not match", await response.Content.ReadAsStringAsync());
        Assert.Equal("CALCULATED", run.Status);
        Assert.Null(run.ApprovedBy);
        Assert.Null(run.ApprovalDate);
        repository.Verify(r => r.GetPayrollRunByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.UpdatePayrollRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        handoffs.Verify(h => h.CreateApprovedPayrollHandoffAsync(It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task SeedPayroll(bool reverseGrant = true, bool closed = false, long runBranch = 7, bool viewGrant = true)
    {
        using var db = new OracleDbContext(Options(1));
        (await db.SysBranches.SingleAsync(b => b.Id == 7)).BaseCurrencyId = 9;
        (await db.Employees.SingleAsync(e => e.EmployeeCode == "SELF")).NationalId = "PRIVATE_PAYROLL_IDENTITY";
        db.AddRange(new SysCurrency { Id = 9, ShortNameEn = "USD" },
            new SysScreen { Id = 52, SystemId = 50, ScreenCode = "hr-payroll", IsActive = true },
            new SysFeature { Id = 64, FeatureCode = "post", IsActive = true },
            new SysScreenFeature { ScreenId = 52, FeatureId = 64 }, new SysScreenFeature { ScreenId = 52, FeatureId = 65 },
            new SysScreenFeature { ScreenId = 52, FeatureId = 62 },
            new SysUserScreenPermission { UserId = 12, BranchId = 7, ScreenId = 52, FeatureId = 64, IsGranted = true },
            new SysUserScreenPermission { UserId = 12, BranchId = 7, ScreenId = 52, FeatureId = 65, IsGranted = reverseGrant },
            new SysUserScreenPermission { UserId = 12, BranchId = 7, ScreenId = 52, FeatureId = 62, IsGranted = viewGrant },
            new GlVoucherType { Id = 4, TypeCode = 4, SerialResetPolicy = "MONTHLY" },
            new SysFiscalYear { Id = 12, BranchId = 7, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31), IsActive = true },
            new SysFiscalYear { Id = 13, BranchId = 7, StartDate = new(2027, 1, 1), EndDate = new(2027, 12, 31), IsActive = true },
            new PayrollPeriod { Id = 100, CompanyId = 1, PeriodCode = "P26", StartDate = new(2026, 11, 1), EndDate = new(2026, 11, 30) },
            new PayrollPostingConfiguration { BranchId = 7, CurrencyId = 9, VoucherType = 4, SalaryExpense = "SALARY", EmployerSscExpense = "EMPLOYER",
                SalaryPayable = "NET", SscPayable = "SSC", TaxPayable = "TAX", DeductionsPayable = "OTHER" });
        foreach (var account in new[] { "SALARY", "EMPLOYER", "NET", "SSC", "TAX", "OTHER" })
            db.Add(new GlAccount { AccountCode = account, AccountType = "DETAIL" });
        var run = new PayrollRun { Id = 100, BranchId = runBranch, PayPeriod = "P26", Status = "APPROVED", TotalGrossSalary = 100, TotalNetSalary = 100 };
        run.Lines.Add(new PayrollRunLine { EmployeeCode = "SELF", BranchId = runBranch, CurrencyCode = "USD", GrossSalary = 100, NetPay = 100 });
        db.Add(run);
        if (closed) db.Add(new GlFiscalPeriod { Id = 1, FiscalYearId = 13, StartDate = new(2027, 1, 1), EndDate = new(2027, 1, 31), Status = "HARD_CLOSE" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task LegacyHrFinancialHttpRoutesAreBlockedWithoutChangingPayrollOrJournals()
    {
        await SeedPayroll();
        foreach (var path in new[] { "post-to-gl", "reverse-gl" })
        {
            using var response = await Send("POST", "/api/hr/payroll/100/" + path,
                path == "reverse-gl" ? new { ReversalDate = "2027-01-05", Reason = "Correction" } : null);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var payload = await response.Content.ReadAsStringAsync();
            Assert.Contains("HR_ACCOUNTING_HANDOFF_REQUIRED", payload); Assert.DoesNotContain("PRIVATE_PAYROLL_IDENTITY", payload);
        }
        using var db = new OracleDbContext(Options(1));
        Assert.Empty(await db.GlVoucherHeaders.ToListAsync()); Assert.Empty(await db.GlVoucherSerials.ToListAsync());
        var run = await db.PayrollRuns.SingleAsync(); Assert.Equal("APPROVED", run.Status); Assert.Null(run.JournalVoucherId);
        using var other = new OracleDbContext(Options(2)); Assert.Empty(await other.GlVoucherHeaders.ToListAsync());
    }

    [Theory] [InlineData("empty")] [InlineData("date")] [InlineData("reason")]
    public async Task PayrollReverseHttpRejectsIncompleteBodyWithoutEffects(string missing)
    {
        await SeedPayroll();
        object request = missing == "empty" ? new { } : missing == "date" ? (object)new { Reason = "Correction" } : new { ReversalDate = "2027-01-05" };
        using var response = await Send("POST", "/api/hr/payroll/100/reverse-gl", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var db = new OracleDbContext(Options(1)); Assert.Empty(await db.GlVoucherHeaders.ToListAsync());
    }

    [Fact]
    public async Task LegacyHrFinancialHttpRetainsPermissionChecksAndCannotExecuteForOtherBranch()
    {
        await SeedPayroll(reverseGrant: false);
        using var denied = await Send("POST", "/api/hr/payroll/100/reverse-gl", new { ReversalDate = "2027-01-05", Reason = "Correction" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var db = new OracleDbContext(Options(1));
        (await db.PayrollRuns.SingleAsync()).BranchId = 8;
        (await db.SysUserScreenPermissions.SingleAsync(p => p.FeatureId == 65)).IsGranted = true; await db.SaveChangesAsync();
        using var outside = await Send("POST", "/api/hr/payroll/100/reverse-gl", new { ReversalDate = "2027-01-05", Reason = "Correction" });
        Assert.Equal(HttpStatusCode.Conflict, outside.StatusCode);
        Assert.Empty(await db.GlVoucherHeaders.ToListAsync());
    }

    [Fact]
    public async Task LegacyHrFinancialHttpDoesNotBypassRetirementForAClosedTargetPeriod()
    {
        await SeedPayroll(closed: true);
        using var denied = await Send("POST", "/api/hr/payroll/100/reverse-gl", new { ReversalDate = "2027-01-05", Reason = "Correction" });
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        using var db = new OracleDbContext(Options(1)); Assert.Empty(await db.GlVoucherHeaders.ToListAsync());
        Assert.Equal("APPROVED", (await db.PayrollRuns.SingleAsync()).Status);
        Assert.Equal("HARD_CLOSE", (await db.GlFiscalPeriods.SingleAsync()).Status);
    }

    [Fact]
    public async Task LegacyHrBankHttpCannotProducePaymentInstructions()
    {
        await SeedPayroll();
        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysFeature { Id = 66, FeatureCode = "export", IsActive = true });
            db.Add(new SysScreenFeature { ScreenId = 52, FeatureId = 66 });
            db.Add(new SysUserScreenPermission { UserId = 12, BranchId = 7, ScreenId = 52, FeatureId = 66, IsGranted = true });
            await db.SaveChangesAsync();
        }
        foreach (var suffix in new[] { "export-bank", "export-bank/preview" })
        {
            using var response = await Send("GET", "/api/hr/payroll/100/" + suffix);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("HR_ACCOUNTING_HANDOFF_REQUIRED", await response.Content.ReadAsStringAsync());
        }
        using var read = new OracleDbContext(Options(1)); Assert.Empty(await read.GlVoucherHeaders.ToListAsync());
    }

    [Fact]
    public async Task PayrollCorrectionHttpDispatchesBoundActorAndEnforcesReversePermission()
    {
        await SeedPayroll(reverseGrant: false);
        var mockPayroll = new Mock<IPayrollService>();
        mockPayroll.Setup(p => p.CreatePayrollCorrectionAsync(100, "Correction due to punch fix", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollCalculationResultDto(101, "P26", 1, 100m, 100m, 0m, 0m, 0m, 0m, 0m, []) { Status = "CALCULATED", CorrectionOfRunId = 100, CorrectionVersion = 2 });
        payrollServiceOverride = mockPayroll.Object;

        using (var denied = await Send("POST", "/api/hr/payroll/100/correct", new { reason = "Correction due to punch fix" }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        mockPayroll.Verify(p => p.CreatePayrollCorrectionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        using (var db = new OracleDbContext(Options(1)))
        {
            (await db.SysUserScreenPermissions.SingleAsync(p => p.FeatureId == 65 && p.ScreenId == 52)).IsGranted = true;
            await db.SaveChangesAsync();
        }

        using var accepted = await Send("POST", "/api/hr/payroll/100/correct", new { reason = "Correction due to punch fix" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        mockPayroll.Verify(p => p.CreatePayrollCorrectionAsync(100, "Correction due to punch fix", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("REVERSED", 200)]
    [InlineData("RETURNED", 200)]
    [InlineData("WITHDRAWN", 200)]
    [InlineData("DELIVERED", 400)]
    [InlineData("ACCEPTED", 400)]
    public async Task PayrollCorrectionHttpUsesRealServiceAndEnforcesAccountingTransition(string handoffStatus, int expectedStatusCode)
    {
        await SeedPayroll();
        var period = new PayrollPeriod
        {
            Id = 100, CompanyId = 1, PeriodCode = "P26", Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30)
        };
        var originalRun = new PayrollRun
        {
            Id = 100, BranchId = 7, PayPeriod = "P26", Status = "APPROVED",
            ApprovalDate = new DateTime(2026, 11, 5), CorrectionVersion = 1,
            TotalGrossSalary = 100m, TotalNetSalary = 100m
        };
        var calculatedRun = new PayrollRun
        {
            Id = 101, BranchId = 7, PayPeriod = "P26", Status = "CALCULATED",
            TotalGrossSalary = 110m, TotalNetSalary = 110m
        };
        PayrollRun? persistedCorrection = null;

        var periodRepository = new Mock<IPayrollPeriodRepository>();
        periodRepository.Setup(r => r.GetPayrollRunSnapshotAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(originalRun);
        periodRepository.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(originalRun);
        periodRepository.Setup(r => r.GetLatestPayrollCorrectionAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(originalRun);
        periodRepository.Setup(r => r.GetPeriodForRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>())).ReturnsAsync(period);
        periodRepository.Setup(r => r.GetPeriodByCodeAsync(1, "P26", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        periodRepository.Setup(r => r.GetActiveEmployeesAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee>());
        periodRepository.Setup(r => r.SupersedePayrollCorrectionFamilyAsync(100, "MOBILE_USER", It.IsAny<CancellationToken>()))
            .Callback(() => originalRun.Status = "SUPERSEDED").Returns(Task.CompletedTask);
        periodRepository.Setup(r => r.AddPayrollRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollRun, CancellationToken>((run, _) => persistedCorrection = run).Returns(Task.CompletedTask);
        periodRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        periodRepository.Setup(r => r.LinkApprovedAdjustmentsToRunAsync(1, It.IsAny<PayrollRun>(), "MOBILE_USER", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var calculator = new Mock<IPayrollCalculationEngine>();
        calculator.Setup(c => c.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 7,
                "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(1, "P26", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new(), new()));

        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(h => h.GetPayrollHandoffStatusAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollHandoffStatusDto(44, "HR-PAY-1-100-1", 1, handoffStatus, "P26",
                DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-4), DateTime.UtcNow, "Accounting correction"));
        accountingHandoffServiceOverride = handoffs.Object;
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(1, "P26",
                It.IsAny<Func<Task<PayrollCalculationResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, Func<Task<PayrollCalculationResultDto>>, CancellationToken>((_, _, action, _) => action());
        payrollServiceFactory = sp => new PayrollService(periodRepository.Object, calculator.Object, validation.Object,
            unitOfWork.Object, accountingHandoffs: sp.GetRequiredService<IHrAccountingHandoffService>());

        using var response = await Send("POST", "/api/hr/payroll/100/correct", new { reason = "Corrected approved attendance" });
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);
        if (expectedStatusCode != (int)HttpStatusCode.OK)
        {
            Assert.Contains("Accounting must return or withdraw", responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.Null(persistedCorrection);
            Assert.Equal("APPROVED", originalRun.Status);
            calculator.Verify(c => c.CalculatePayrollForPeriodAsync(It.IsAny<PayrollPeriod>(), It.IsAny<IReadOnlyList<Employee>>(),
                It.IsAny<long?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            periodRepository.Verify(r => r.SupersedePayrollCorrectionFamilyAsync(It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
            return;
        }

        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        Assert.NotNull(persistedCorrection);
        Assert.Equal(101, persistedCorrection!.Id);
        Assert.Equal(100, persistedCorrection.CorrectionOfRunId);
        Assert.Equal(100, persistedCorrection.CorrectionRootRunId);
        Assert.Equal(2, persistedCorrection.CorrectionVersion);
        Assert.Equal("Corrected approved attendance", persistedCorrection.CorrectionReason);
        Assert.Equal("SUPERSEDED", originalRun.Status);
        handoffs.Verify(h => h.GetPayrollHandoffStatusAsync(100, It.IsAny<CancellationToken>()), Times.Once);
        calculator.Verify(c => c.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 7,
            "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
        periodRepository.Verify(r => r.SupersedePayrollCorrectionFamilyAsync(100, "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(7, "REVERSED", true, HttpStatusCode.OK)]
    [InlineData(7, "RETURNED", true, HttpStatusCode.OK)]
    [InlineData(7, "WITHDRAWN", true, HttpStatusCode.OK)]
    [InlineData(7, "DELIVERED", true, HttpStatusCode.BadRequest)]
    [InlineData(7, "ACCEPTED", true, HttpStatusCode.BadRequest)]
    [InlineData(8, "REVERSED", true, HttpStatusCode.NotFound)]
    [InlineData(7, "REVERSED", false, HttpStatusCode.BadRequest)]
    public async Task PayrollCorrectionHttpPersistsVersionWithActualRepositoryAndRespectsAccountingAndBranchGates(
        long runBranch, string handoffStatus, bool periodOpen, HttpStatusCode expectedStatus)
    {
        await SeedPayroll(runBranch: runBranch);
        using (var db = new OracleDbContext(Options(1)))
        {
            var source = await db.PayrollRuns.SingleAsync(r => r.Id == 100);
            source.ApprovalDate = new DateTime(2026, 11, 5);
            if (!periodOpen)
                (await db.PayrollPeriods.SingleAsync(p => p.CompanyId == 1 && p.PeriodCode == "P26")).Status = "CLOSED";
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 1214,
                CompanyId = 1,
                BranchId = runBranch,
                SourceType = "PAYROLL",
                SourceId = "100",
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "P26",
                HandoffReference = $"HR-CORRECT-{runBranch}",
                PayloadHash = $"PAYROLL-CORRECT-{runBranch}",
                DeliveryStatus = handoffStatus,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "HTTP_TEST"
            });
            await db.SaveChangesAsync();
        }

        accountingHandoffServiceFactory = sp => new HrAccountingHandoffService(
            sp.GetRequiredService<OracleDbContext>(),
            sp.GetRequiredService<IGlVoucherRepository>(),
            sp.GetRequiredService<IHrPayrollPostingService>(),
            Mock.Of<IReceiptPaymentVoucherService>(),
            Mock.Of<IGlVoucherService>(),
            sp.GetRequiredService<IHrUnitOfWork>());
        var engine = new Mock<IPayrollCalculationEngine>();
        var calculated = new PayrollRun
        {
            PayPeriod = "P26",
            BranchId = runBranch,
            Status = "CALCULATED",
            TotalGrossSalary = 110m,
            TotalNetSalary = 110m
        };
        calculated.Lines.Add(new PayrollRunLine
        {
            EmployeeCode = runBranch == 7 ? "SELF" : "OTHER",
            BranchId = runBranch,
            BasicSalary = 110m,
            GrossSalary = 110m,
            NetPay = 110m
        });
        engine.Setup(e => e.CalculatePayrollForPeriodAsync(It.IsAny<PayrollPeriod>(), It.IsAny<IReadOnlyList<Employee>>(),
                runBranch, "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculated);
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(1, "P26", runBranch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new(), new()));
        payrollServiceFactory = sp => new PayrollService(sp.GetRequiredService<IPayrollPeriodRepository>(), engine.Object,
            validation.Object, sp.GetRequiredService<IHrUnitOfWork>(), accountingHandoffs: sp.GetRequiredService<IHrAccountingHandoffService>());

        using var response = await Send("POST", "/api/hr/payroll/100/correct", new { reason = "Corrected attendance in HR" });
        Assert.Equal(expectedStatus, response.StatusCode);

        using var verify = new OracleDbContext(Options(1));
        var original = await verify.PayrollRuns.SingleAsync(r => r.Id == 100);
        if (expectedStatus == HttpStatusCode.OK)
        {
            var result = await Data(response);
            var correctionId = result.GetProperty("payrollRunId").GetInt64();
            var correction = await verify.PayrollRuns.Include(r => r.Lines).SingleAsync(r => r.Id == correctionId);
            Assert.Equal("SUPERSEDED", original.Status);
            Assert.Equal("CALCULATED", correction.Status);
            Assert.Equal(100, correction.CorrectionOfRunId);
            Assert.Equal(100, correction.CorrectionRootRunId);
            Assert.Equal(2, correction.CorrectionVersion);
            Assert.Equal("Corrected attendance in HR", correction.CorrectionReason);
            Assert.Equal(110m, correction.TotalNetSalary);
            Assert.Equal("MOBILE_USER", original.UpdateUser);
        }
        else
        {
            Assert.Equal("APPROVED", original.Status);
            Assert.Null(original.UpdateUser);
            Assert.Single(await verify.PayrollRuns.ToListAsync());
            engine.Verify(e => e.CalculatePayrollForPeriodAsync(It.IsAny<PayrollPeriod>(),
                It.IsAny<IReadOnlyList<Employee>>(), It.IsAny<long?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task PayrollHandoffStatusAndViewHttpEnforceBranchViewPermission()
    {
        await SeedPayroll(viewGrant: false);
        var mockPayroll = new Mock<IPayrollService>();
        mockPayroll.Setup(p => p.GetPayrollHandoffStatusAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollHandoffStatusDto(42, "HR-42", 1, "DELIVERED", "P26", DateTime.UtcNow, DateTime.UtcNow, null, null));
        mockPayroll.Setup(p => p.GetPayrollHandoffPaymentStatusAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollHandoffPaymentStatusDto(42, "HR-42", "USD", 100m, 0m, 0m, 0m, 100m, "PREPARED"));
        payrollServiceOverride = mockPayroll.Object;

        using (var deniedStatus = await Send("GET", "/api/hr/payroll/100/handoff"))
            Assert.Equal(HttpStatusCode.Forbidden, deniedStatus.StatusCode);
        using (var deniedPayments = await Send("GET", "/api/hr/payroll/100/handoff/payments"))
            Assert.Equal(HttpStatusCode.Forbidden, deniedPayments.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            (await db.SysUserScreenPermissions.SingleAsync(p => p.FeatureId == 62 && p.ScreenId == 52)).IsGranted = true;
            await db.SaveChangesAsync();
        }

        using var acceptedStatus = await Send("GET", "/api/hr/payroll/100/handoff");
        Assert.Equal(HttpStatusCode.OK, acceptedStatus.StatusCode);
        using var acceptedPayments = await Send("GET", "/api/hr/payroll/100/handoff/payments");
        Assert.Equal(HttpStatusCode.OK, acceptedPayments.StatusCode);
    }

    [Fact]
    public async Task PayrollHandoffWithdrawalHttpEnforcesReversePermissionAndDispatchesBoundUser()
    {
        await SeedPayroll(reverseGrant: false);
        var mockPayroll = new Mock<IPayrollService>();
        mockPayroll.Setup(p => p.WithdrawPayrollHandoffAsync(100, "Correction required", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollHandoffStatusDto(42, "HR-42", 1, "WITHDRAWN", "P26", DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, "Correction required"));
        payrollServiceOverride = mockPayroll.Object;

        using (var denied = await Send("POST", "/api/hr/payroll/100/handoff/withdraw", new { reason = "Correction required" }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            (await db.SysUserScreenPermissions.SingleAsync(p => p.FeatureId == 65 && p.ScreenId == 52)).IsGranted = true;
            await db.SaveChangesAsync();
        }

        using var accepted = await Send("POST", "/api/hr/payroll/100/handoff/withdraw", new { reason = "Correction required" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        mockPayroll.Verify(p => p.WithdrawPayrollHandoffAsync(100, "Correction required", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PayrollHandoffWithdrawalHttpUsesRealServiceAndPersistsOutboxTransition()
    {
        await SeedPayroll(reverseGrant: true);
        payrollServiceFactory = sp => new PayrollService(
            Mock.Of<IPayrollPeriodRepository>(),
            Mock.Of<IPayrollCalculationEngine>(),
            Mock.Of<IPayrollValidationService>(),
            accountingHandoffs: new HrAccountingHandoffService(
                sp.GetRequiredService<OracleDbContext>(),
                Mock.Of<IGlVoucherRepository>(),
                Mock.Of<IHrPayrollPostingService>(),
                Mock.Of<IReceiptPaymentVoucherService>(),
                Mock.Of<IGlVoucherService>()));

        using (var db = new OracleDbContext(Options(1)))
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 42, CompanyId = 1, BranchId = 7, SourceType = "PAYROLL", SourceId = "100",
                SourceVersion = 1, Operation = "SUBMIT", PayPeriod = "P26", HandoffReference = "HR-42",
                PayloadHash = "immutable-hash", DeliveryStatus = "QUEUED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr-user",
                OutboxMessages =
                [
                    new HrAccountingOutboxMessage
                    {
                        Id = 900, EventKey = "1:PAYROLL:100:1:SUBMIT", EventType = "PAYROLL_SUBMIT",
                        PayloadHash = "immutable-hash", Status = "PENDING", CreatedAtUtc = DateTime.UtcNow
                    }
                ]
            });
            await db.SaveChangesAsync();
        }

        using var response = await Send("POST", "/api/hr/payroll/100/handoff/withdraw", new { reason = "Correction required" });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        await using var verify = new OracleDbContext(Options(1));
        var handoff = await verify.HrAccountingHandoffs.Include(x => x.Transitions).Include(x => x.OutboxMessages).SingleAsync(x => x.Id == 42);
        Assert.Equal("WITHDRAWN", handoff.DeliveryStatus);
        var transition = Assert.Single(handoff.Transitions);
        Assert.Equal("MOBILE_USER", transition.Actor);
        Assert.Equal("Correction required", transition.Reason);
        Assert.Contains(handoff.OutboxMessages, x => x.EventType == "PAYROLL_SUBMIT" && x.Status == "CANCELLED");
        Assert.Contains(handoff.OutboxMessages, x => x.EventType == "PAYROLL_WITHDRAW" && x.Status == "PENDING");
    }
}
