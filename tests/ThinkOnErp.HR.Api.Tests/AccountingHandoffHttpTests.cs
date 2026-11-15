using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed partial class HrAttendanceHttpTests
{
    [Fact]
    public async Task AccountingHandoffReviewRequiresExplicitBranchGrantAndDispatchesBoundActorAndReason()
    {
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(s => s.ReviewAsync(42, It.IsAny<bool>(), It.IsAny<string?>(), "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrAccountingHandoff { Id = 42, DeliveryStatus = "ACCEPTED" });
        accountingHandoffServiceOverride = handoffs.Object;

        using (var denied = await Send("POST", "/api/accounting/hr-handoffs/42/accept"))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        handoffs.Verify(s => s.ReviewAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 300, UserId = 12, ScreenId = 80, FeatureId = 81, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var accepted = await Send("POST", "/api/accounting/hr-handoffs/42/accept");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        handoffs.Verify(s => s.ReviewAsync(42, true, null, "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);

        using var returned = await Send("POST", "/api/accounting/hr-handoffs/42/return", new { reason = "Missing evidence" });
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        handoffs.Verify(s => s.ReviewAsync(42, false, "Missing evidence", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AccountingHandoffReviewHttpPersistsTransitionThroughRealService()
    {
        accountingHandoffServiceFactory = sp => new HrAccountingHandoffService(
            sp.GetRequiredService<OracleDbContext>(),
            Mock.Of<IGlVoucherRepository>(),
            Mock.Of<IHrPayrollPostingService>(),
            Mock.Of<IReceiptPaymentVoucherService>(),
            Mock.Of<IGlVoucherService>());

        using (var db = new OracleDbContext(Options(1)))
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 42, CompanyId = 1, BranchId = 7, SourceType = "PAYROLL", SourceId = "17",
                Operation = "SUBMIT", HandoffReference = "HR-42", PayloadHash = "hash",
                DeliveryStatus = "DELIVERED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr-user"
            });
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 320, UserId = 12, ScreenId = 80, FeatureId = 81, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var response = await Send("POST", "/api/accounting/hr-handoffs/42/return", new { reason = "Missing bank evidence" });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        Assert.Contains("\"handoffId\":42", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"deliveryStatus\":\"RETURNED\"", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PayloadHash", responseBody, StringComparison.OrdinalIgnoreCase);
        await using var verify = new OracleDbContext(Options(1));
        var handoff = await verify.HrAccountingHandoffs.Include(x => x.Transitions).SingleAsync(x => x.Id == 42);
        Assert.Equal("RETURNED", handoff.DeliveryStatus);
        var transition = Assert.Single(handoff.Transitions);
        Assert.Equal("DELIVERED", transition.FromStatus);
        Assert.Equal("RETURNED", transition.ToStatus);
        Assert.Equal("MOBILE_USER", transition.Actor);
        Assert.Equal("Missing bank evidence", transition.Reason);
    }

    [Fact]
    public async Task AccountingHandoffReviewRejectsAnonymousHttpRequest()
    {
        using var response = await Send("POST", "/api/accounting/hr-handoffs/42/accept", company: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PaymentFailureAndReversalRequireSeparateBranchGrantsOverHttp()
    {
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(s => s.FailPaymentAsync(10, "Bank instruction cancelled", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingHandoffPayment { Id = 10, Status = "FAILED" });
        handoffs.Setup(s => s.ReversePaymentAsync(10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingHandoffPayment { Id = 10, Status = "REVERSED" });
        accountingHandoffServiceOverride = handoffs.Object;

        using (var denied = await Send("POST", "/api/accounting/hr-handoffs/payments/10/fail", new { reason = "Bank instruction cancelled" }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 301, UserId = 12, ScreenId = 80, FeatureId = 82, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }
        using (var failed = await Send("POST", "/api/accounting/hr-handoffs/payments/10/fail", new { reason = "Bank instruction cancelled" }))
            Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        handoffs.Verify(s => s.FailPaymentAsync(10, "Bank instruction cancelled", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);

        using (var reverseDenied = await Send("POST", "/api/accounting/hr-handoffs/payments/10/reverse",
            new { reason = "Duplicate transfer", externalReference = "BANK-REV-101", evidenceReference = "REV-EVIDENCE-4" }))
            Assert.Equal(HttpStatusCode.Forbidden, reverseDenied.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 302, UserId = 12, ScreenId = 80, FeatureId = 65, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }
        using var reversed = await Send("POST", "/api/accounting/hr-handoffs/payments/10/reverse",
            new { reason = "Duplicate transfer", externalReference = "BANK-REV-101", evidenceReference = "REV-EVIDENCE-4" });
        Assert.Equal(HttpStatusCode.OK, reversed.StatusCode);
        handoffs.Verify(s => s.ReversePaymentAsync(10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JournalAndFundingReversalRequireExplicitReverseGrantOverHttp()
    {
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(s => s.ReverseJournalVoucherAsync(42, It.IsAny<DateTime>(), "Correction needed", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(8L);
        handoffs.Setup(s => s.ReverseLoanFundingAsync(42, "Duplicate funding", "fund-rev-01", "EXT-REV", "EVD-REV", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(9L);
        accountingHandoffServiceOverride = handoffs.Object;

        using (var jvDenied = await Send("POST", "/api/accounting/hr-handoffs/42/journal-voucher/reverse", new { reason = "Correction needed" }))
            Assert.Equal(HttpStatusCode.Forbidden, jvDenied.StatusCode);

        using (var fundingDenied = await Send("POST", "/api/accounting/hr-handoffs/42/loan-funding-reverse",
            new { reason = "Duplicate funding", eventKey = "fund-rev-01", externalReference = "EXT-REV", evidenceReference = "EVD-REV" }))
            Assert.Equal(HttpStatusCode.Forbidden, fundingDenied.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 303, UserId = 12, ScreenId = 80, FeatureId = 65, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var jvAccepted = await Send("POST", "/api/accounting/hr-handoffs/42/journal-voucher/reverse", new { reason = "Correction needed" });
        Assert.Equal(HttpStatusCode.OK, jvAccepted.StatusCode);
        handoffs.Verify(s => s.ReverseJournalVoucherAsync(42, It.IsAny<DateTime>(), "Correction needed", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);

        using var fundingAccepted = await Send("POST", "/api/accounting/hr-handoffs/42/loan-funding-reverse",
            new { reason = "Duplicate funding", eventKey = "fund-rev-01", externalReference = "EXT-REV", evidenceReference = "EVD-REV" });
        Assert.Equal(HttpStatusCode.OK, fundingAccepted.StatusCode);
        handoffs.Verify(s => s.ReverseLoanFundingAsync(42, "Duplicate funding", "fund-rev-01", "EXT-REV", "EVD-REV", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AccountingInboxAndPaymentsHttpEnforcesAccountingViewPermission()
    {
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(s => s.GetAccountingInboxAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountingHandoffInboxDto>
            {
                new(42, 1, 7, "PAYROLL", "10", 1, "SUBMIT", "HR-42", "hash", "DELIVERED", "P26", DateTime.UtcNow, DateTime.UtcNow, "hr-user", null, null, [])
            });
        handoffs.Setup(s => s.GetAccountingPaymentSummaryAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingHandoffPaymentSummaryDto(42, "HR-42", "POSTED", "USD", 100m, 0m, 0m, 0m, 100m, []));
        accountingHandoffServiceOverride = handoffs.Object;

        using (var deniedInbox = await Send("GET", "/api/accounting/hr-handoffs"))
            Assert.Equal(HttpStatusCode.Forbidden, deniedInbox.StatusCode);

        using (var deniedPayments = await Send("GET", "/api/accounting/hr-handoffs/42/payments"))
            Assert.Equal(HttpStatusCode.Forbidden, deniedPayments.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 304, UserId = 12, ScreenId = 80, FeatureId = 62, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var acceptedInbox = await Send("GET", "/api/accounting/hr-handoffs");
        Assert.Equal(HttpStatusCode.OK, acceptedInbox.StatusCode);

        using var acceptedPayments = await Send("GET", "/api/accounting/hr-handoffs/42/payments");
        Assert.Equal(HttpStatusCode.OK, acceptedPayments.StatusCode);
    }

    [Fact]
    public async Task CreateJournalVoucherAndPostingReceiptRequireExplicitPostGrantOverHttp()
    {
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(s => s.CreateJournalVoucherAsync(42, "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(101L);
        handoffs.Setup(s => s.ConfirmJournalPostedAsync(42, 101L, "evt-post-01", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        accountingHandoffServiceOverride = handoffs.Object;

        using (var deniedJv = await Send("POST", "/api/accounting/hr-handoffs/42/journal-voucher"))
            Assert.Equal(HttpStatusCode.Forbidden, deniedJv.StatusCode);

        using (var deniedReceipt = await Send("POST", "/api/accounting/hr-handoffs/42/posting-receipt",
            new { voucherId = 101, eventKey = "evt-post-01" }))
            Assert.Equal(HttpStatusCode.Forbidden, deniedReceipt.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysFeature { Id = 83, FeatureCode = "post", IsActive = true });
            db.Add(new SysScreenFeature { ScreenId = 80, FeatureId = 83 });
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 305, UserId = 12, ScreenId = 80, FeatureId = 83, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var acceptedJv = await Send("POST", "/api/accounting/hr-handoffs/42/journal-voucher");
        Assert.Equal(HttpStatusCode.OK, acceptedJv.StatusCode);
        handoffs.Verify(s => s.CreateJournalVoucherAsync(42, "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);

        using var acceptedReceipt = await Send("POST", "/api/accounting/hr-handoffs/42/posting-receipt",
            new { voucherId = 101, eventKey = "evt-post-01" });
        Assert.Equal(HttpStatusCode.OK, acceptedReceipt.StatusCode);
        handoffs.Verify(s => s.ConfirmJournalPostedAsync(42, 101L, "evt-post-01", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JournalPostingReceiptHttpUsesRealServiceAndPersistsPostedTransition()
    {
        var voucherRepository = new Mock<IGlVoucherRepository>();
        voucherRepository.Setup(r => r.GetByIdAsync(202, It.IsAny<CancellationToken>())).ReturnsAsync(new GlVoucherHeader
        {
            Id = 202, BranchId = 7, SourceSystemCode = "HR_HANDOFF", SourceRefId = 42,
            Status = 3, PostDate = DateTime.UtcNow, TotalLocalDebit = 500m, TotalLocalCredit = 500m
        });
        accountingHandoffServiceFactory = sp => new HrAccountingHandoffService(
            sp.GetRequiredService<OracleDbContext>(), voucherRepository.Object,
            Mock.Of<IHrPayrollPostingService>(), Mock.Of<IReceiptPaymentVoucherService>(), Mock.Of<IGlVoucherService>());

        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysFeature { Id = 83, FeatureCode = "post", IsActive = true });
            db.Add(new SysScreenFeature { ScreenId = 80, FeatureId = 83 });
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 331, UserId = 12, ScreenId = 80, FeatureId = 83, BranchId = 7, IsGranted = true });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 42, CompanyId = 1, BranchId = 7, SourceType = "PAYROLL", SourceId = "17", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-PAY-1-17-1", PayloadHash = "payroll-payload",
                DeliveryStatus = "ACCEPTED", PayPeriod = "P26", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr"
            });
            await db.SaveChangesAsync();
        }

        using var response = await Send("POST", "/api/accounting/hr-handoffs/42/posting-receipt",
            new { voucherId = 202, eventKey = "posting-event-202" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verify = new OracleDbContext(Options(1));
        var handoff = await verify.HrAccountingHandoffs.Include(x => x.Transitions).SingleAsync(x => x.Id == 42);
        Assert.Equal("POSTED", handoff.DeliveryStatus);
        var transition = Assert.Single(handoff.Transitions);
        Assert.Equal("MOBILE_USER", transition.Actor);
        Assert.Equal("POSTED", transition.ToStatus);
        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync(x => x.HandoffId == 42);
        Assert.Equal(202, receipt.VoucherId);
        Assert.Equal("posting-event-202", receipt.EventKey);
        Assert.Equal("MOBILE_USER", receipt.ReceivedBy);
    }

    [Fact]
    public async Task JournalPostingReceiptHttpSettlesReservedAdvanceAndLoanScheduleThroughRealService()
    {
        var postedVoucher = new GlVoucherHeader
        {
            Id = 203, BranchId = 7, FiscalYearId = 26, VoucherType = 103, VoucherNo = 7001,
            SourceSystemCode = "HR_HANDOFF", SourceRefId = 43, Status = 3, PostDate = DateTime.UtcNow,
            TotalAmount = 250m, TotalLocalDebit = 250m, TotalLocalCredit = 250m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 203, LineSer = 1, BranchId = 7, CurrencyId = 9, AccountCode = "SALARY-EXP", Debit = 250m, LocalDebit = 250m, BaseDebit = 250m, ExchangeRate = 1m },
                new GlVoucherDetail { VoucherId = 203, LineSer = 3, BranchId = 7, CurrencyId = 9, AccountCode = "SALARY-PAYABLE", Credit = 250m, LocalCredit = 250m, BaseCredit = 250m, ExchangeRate = 1m }
            ]
        };
        var voucherStore = new List<GlVoucherHeader> { postedVoucher };
        var voucherRepository = new Mock<IGlVoucherRepository>();
        voucherRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => voucherStore.SingleOrDefault(v => v.Id == id));
        voucherRepository.Setup(r => r.GetBySourceAsync(7, It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long branchId, string sourceType, long sourceId, CancellationToken _) =>
                (IReadOnlyList<GlVoucherHeader>)voucherStore.Where(v => v.BranchId == branchId && v.SourceSystemCode == sourceType && v.SourceRefId == sourceId).ToList());
        voucherRepository.Setup(r => r.GetVoucherTypeByCodeAsync(103, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlVoucherType { TypeCode = 103, TypeKey = "JOURNAL", SerialResetPolicy = "MONTHLY" });
        voucherRepository.Setup(r => r.GenerateNextSerialNoAsync(7, It.IsAny<int>(), It.IsAny<int>(), 103, "MONTHLY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(7002);
        voucherRepository.Setup(r => r.AddVoucherAsync(It.IsAny<GlVoucherHeader>(), It.IsAny<CancellationToken>()))
            .Callback<GlVoucherHeader, CancellationToken>((voucher, _) => { voucher.Id = 204; voucherStore.Add(voucher); })
            .Returns(Task.CompletedTask);
        voucherRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var glVoucherService = new GlVoucherService(voucherRepository.Object, Mock.Of<IGlAccountRepository>(),
            Mock.Of<IGlCostCenterRepository>(), Mock.Of<IGlFiscalPeriodRepository>(), Mock.Of<IGlAccountBalanceRepository>(),
            Mock.Of<IArSubledgerRepository>(), Mock.Of<IApSubledgerRepository>(), Mock.Of<ICurrentTenantContext>(),
            NullLogger<GlVoucherService>.Instance);
        accountingHandoffServiceFactory = sp => new HrAccountingHandoffService(
            sp.GetRequiredService<OracleDbContext>(), voucherRepository.Object,
            Mock.Of<IHrPayrollPostingService>(), Mock.Of<IReceiptPaymentVoucherService>(), glVoucherService);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysFeature { Id = 83, FeatureCode = "post", IsActive = true });
            db.Add(new SysScreenFeature { ScreenId = 80, FeatureId = 83 });
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 332, UserId = 12, ScreenId = 80, FeatureId = 83, BranchId = 7, IsGranted = true });
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 334, UserId = 12, ScreenId = 80, FeatureId = 65, BranchId = 7, IsGranted = true });
            db.Employees.Add(new Employee { EmployeeCode = "EMP_ADV", BranchId = 7 });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 43, CompanyId = 1, BranchId = 7, SourceType = "PAYROLL", SourceId = "100", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-PAY-1-100-1", PayloadHash = "settlement-payload",
                DeliveryStatus = "ACCEPTED", PayPeriod = "P26", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines =
                [
                    new HrAccountingHandoffLine { HandoffId = 43, LineType = "REFERENCE:ADVANCE", SourceReference = "ADVANCE:503:PAYROLL_LINE:1", Amount = 100m, CurrencyCode = "JOD" },
                    new HrAccountingHandoffLine { HandoffId = 43, LineType = "REFERENCE:SCHEDULE", SourceReference = "SCHEDULE:602:PAYROLL_LINE:1", Amount = 150m, CurrencyCode = "JOD" }
                ]
            });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 503, EmployeeCode = "EMP_ADV", Status = "ACTIVE", AdvanceAmount = 100m,
                RemainingBalance = 100m, DeductedAmount = 0m, CurrencyCode = "JOD", ReservedByHandoffId = 43
            });
            var loan = new EmployeeLoan
            {
                Id = 802, EmployeeCode = "EMP_ADV", Status = "ACTIVE", CurrencyCode = "JOD", TotalPaidAmount = 0m, RemainingBalance = 450m
            };
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 602, EmployeeLoanId = 802, EmployeeLoan = loan, ScheduledAmount = 150m, PaidAmount = 0m,
                Status = "PENDING", ReservedByHandoffId = 43
            });
            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            { HandoffId = 43, VoucherId = 203, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting" });
            await db.SaveChangesAsync();
        }

        await using (var seeded = new OracleDbContext(Options(1)))
        {
            Assert.Contains(await seeded.EmployeeAdvances.ToListAsync(), x => x.Id == 503 && x.ReservedByHandoffId == 43);
            Assert.Contains(await seeded.LoanRepaymentSchedules.ToListAsync(), x => x.Id == 602 && x.ReservedByHandoffId == 43);
        }

        using var response = await Send("POST", "/api/accounting/hr-handoffs/43/posting-receipt",
            new { voucherId = 203, eventKey = "settlement-posting-event" });
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);

        await using (var afterPosting = new OracleDbContext(Options(1)))
        {
            var handoff = await afterPosting.HrAccountingHandoffs.Include(x => x.Transitions).SingleAsync(x => x.Id == 43);
            Assert.Equal("POSTED", handoff.DeliveryStatus);
            Assert.Equal("MOBILE_USER", Assert.Single(handoff.Transitions).Actor);
            var receipt = await afterPosting.AccountingHandoffPostingReceipts.SingleAsync(x => x.HandoffId == 43);
            Assert.Equal(203, receipt.VoucherId);
            Assert.Equal("settlement-posting-event", receipt.EventKey);
            Assert.Equal("MOBILE_USER", receipt.ReceivedBy);
            Assert.Contains("\"Kind\":\"ADVANCE\"", receipt.SettlementSnapshotJson);
            Assert.Contains("\"Kind\":\"SCHEDULE\"", receipt.SettlementSnapshotJson);

            var advance = await afterPosting.EmployeeAdvances.SingleAsync(x => x.Id == 503);
            Assert.Equal("RECOVERED", advance.Status);
            Assert.Equal(0m, advance.RemainingBalance);
            Assert.Equal(100m, advance.DeductedAmount);
            Assert.Null(advance.ReservedByHandoffId);
            var schedule = await afterPosting.LoanRepaymentSchedules.Include(x => x.EmployeeLoan).SingleAsync(x => x.Id == 602);
            Assert.Equal("PAID", schedule.Status);
            Assert.Equal(150m, schedule.PaidAmount);
            Assert.Null(schedule.ReservedByHandoffId);
            var loanAfterSettlement = Assert.IsType<EmployeeLoan>(schedule.EmployeeLoan);
            Assert.Equal(300m, loanAfterSettlement.RemainingBalance);
            Assert.Equal(150m, loanAfterSettlement.TotalPaidAmount);
        }

        using var reversalResponse = await Send("POST", "/api/accounting/hr-handoffs/43/journal-voucher/reverse",
            new { reason = "Restore payroll deductions" });
        var reversalBody = await reversalResponse.Content.ReadAsStringAsync();
        Assert.True(reversalResponse.StatusCode == HttpStatusCode.OK, reversalBody);
        Assert.Contains("\"reversalVoucherId\":204", reversalBody, StringComparison.OrdinalIgnoreCase);
        Assert.True(postedVoucher.IsReversed);
        var reversalVoucher = Assert.Single(voucherStore.Where(v => v.Id == 204));
        Assert.Equal("GL_REVERSAL", reversalVoucher.SourceSystemCode);
        Assert.Equal(203, reversalVoucher.SourceRefId);
        Assert.Equal(250m, reversalVoucher.TotalLocalDebit);
        Assert.Equal(250m, reversalVoucher.TotalLocalCredit);

        using var reversalReplay = await Send("POST", "/api/accounting/hr-handoffs/43/journal-voucher/reverse",
            new { reason = "Restore payroll deductions" });
        var replayBody = await reversalReplay.Content.ReadAsStringAsync();
        Assert.True(reversalReplay.StatusCode == HttpStatusCode.OK, replayBody);
        Assert.Contains("\"reversalVoucherId\":204", replayBody, StringComparison.OrdinalIgnoreCase);
        Assert.Single(voucherStore.Where(v => v.SourceSystemCode == "GL_REVERSAL" && v.SourceRefId == 203));

        await using var verify = new OracleDbContext(Options(1));
        var reversedHandoff = await verify.HrAccountingHandoffs.Include(x => x.Transitions).SingleAsync(x => x.Id == 43);
        Assert.Equal("REVERSED", reversedHandoff.DeliveryStatus);
        var reversalTransition = Assert.Single(reversedHandoff.Transitions.Where(t => t.ToStatus == "REVERSED"));
        Assert.Equal("Restore payroll deductions", reversalTransition.Reason);
        Assert.Equal("MOBILE_USER", reversalTransition.Actor);
        var restoredAdvance = await verify.EmployeeAdvances.SingleAsync(x => x.Id == 503);
        Assert.Equal("ACTIVE", restoredAdvance.Status);
        Assert.Equal(100m, restoredAdvance.RemainingBalance);
        Assert.Equal(0m, restoredAdvance.DeductedAmount);
        Assert.Null(restoredAdvance.ReservedByHandoffId);
        var restoredSchedule = await verify.LoanRepaymentSchedules.Include(x => x.EmployeeLoan).SingleAsync(x => x.Id == 602);
        Assert.Equal("PENDING", restoredSchedule.Status);
        Assert.Equal(0m, restoredSchedule.PaidAmount);
        Assert.Null(restoredSchedule.ReservedByHandoffId);
        var restoredLoan = Assert.IsType<EmployeeLoan>(restoredSchedule.EmployeeLoan);
        Assert.Equal(450m, restoredLoan.RemainingBalance);
        Assert.Equal(0m, restoredLoan.TotalPaidAmount);
    }

    [Fact]
    public async Task JournalReversalHttpUsesRealHandoffAndGlServicesAndPersistsAuditTransition()
    {
        var original = new GlVoucherHeader
        {
            Id = 204, BranchId = 7, FiscalYearId = 26, VoucherType = 103, VoucherNo = 7001,
            Status = 3, PostDate = DateTime.UtcNow, TotalAmount = 100m, TotalLocalDebit = 100m, TotalLocalCredit = 100m,
            SourceSystemCode = "HR_HANDOFF", SourceRefId = 44,
            Details =
            [
                new GlVoucherDetail { VoucherId = 204, LineSer = 1, BranchId = 7, CurrencyId = 9, AccountCode = "SALARY-EXP", Debit = 100m, LocalDebit = 100m, BaseDebit = 100m, ExchangeRate = 1m },
                new GlVoucherDetail { VoucherId = 204, LineSer = 3, BranchId = 7, CurrencyId = 9, AccountCode = "SALARY-PAYABLE", Credit = 100m, LocalCredit = 100m, BaseCredit = 100m, ExchangeRate = 1m }
            ]
        };
        var voucherStore = new List<GlVoucherHeader> { original };
        var voucherRepository = new Mock<IGlVoucherRepository>();
        voucherRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => voucherStore.SingleOrDefault(v => v.Id == id));
        voucherRepository.Setup(r => r.GetBySourceAsync(7, It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long branchId, string sourceType, long sourceId, CancellationToken _) =>
                (IReadOnlyList<GlVoucherHeader>)voucherStore.Where(v => v.BranchId == branchId && v.SourceSystemCode == sourceType && v.SourceRefId == sourceId).ToList());
        voucherRepository.Setup(r => r.GetVoucherTypeByCodeAsync(103, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlVoucherType { TypeCode = 103, TypeKey = "JOURNAL", SerialResetPolicy = "MONTHLY" });
        voucherRepository.Setup(r => r.GenerateNextSerialNoAsync(7, It.IsAny<int>(), It.IsAny<int>(), 103, "MONTHLY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(7002);
        voucherRepository.Setup(r => r.AddVoucherAsync(It.IsAny<GlVoucherHeader>(), It.IsAny<CancellationToken>()))
            .Callback<GlVoucherHeader, CancellationToken>((voucher, _) => { voucher.Id = 205; voucherStore.Add(voucher); })
            .Returns(Task.CompletedTask);
        voucherRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var glVoucherService = new GlVoucherService(voucherRepository.Object, Mock.Of<IGlAccountRepository>(),
            Mock.Of<IGlCostCenterRepository>(), Mock.Of<IGlFiscalPeriodRepository>(), Mock.Of<IGlAccountBalanceRepository>(),
            Mock.Of<IArSubledgerRepository>(), Mock.Of<IApSubledgerRepository>(), Mock.Of<ICurrentTenantContext>(),
            NullLogger<GlVoucherService>.Instance);
        accountingHandoffServiceFactory = sp => new HrAccountingHandoffService(
            sp.GetRequiredService<OracleDbContext>(), voucherRepository.Object, Mock.Of<IHrPayrollPostingService>(),
            Mock.Of<IReceiptPaymentVoucherService>(), glVoucherService);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 333, UserId = 12, ScreenId = 80, FeatureId = 65, BranchId = 7, IsGranted = true });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 44, CompanyId = 1, BranchId = 7, SourceType = "PAYROLL", SourceId = "99", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-PAY-1-99-1", PayloadHash = "reversal-payload",
                DeliveryStatus = "POSTED", PayPeriod = "P26", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr"
            });
            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            { HandoffId = 44, VoucherId = 204, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting" });
            db.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt
            {
                HandoffId = 44, VoucherId = 204, EventKey = "original-post-event", PayloadHash = "posted-payload",
                ReceivedAtUtc = DateTime.UtcNow, ReceivedBy = "accounting"
            });
            await db.SaveChangesAsync();
        }

        using var response = await Send("POST", "/api/accounting/hr-handoffs/44/journal-voucher/reverse",
            new { reason = "Payroll correction requested" });
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        Assert.Contains("\"reversalVoucherId\":205", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.True(original.IsReversed);
        var reversal = Assert.Single(voucherStore.Where(v => v.Id == 205));
        Assert.Equal("GL_REVERSAL", reversal.SourceSystemCode);
        Assert.Equal(204, reversal.SourceRefId);
        Assert.Equal(100m, reversal.TotalLocalDebit);
        Assert.Equal(100m, reversal.TotalLocalCredit);
        Assert.Equal(100m, reversal.Details.Single(d => d.AccountCode == "SALARY-PAYABLE").Debit);

        await using var verify = new OracleDbContext(Options(1));
        var handoff = await verify.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == 44);
        Assert.Equal("REVERSED", handoff.DeliveryStatus);
        var transition = Assert.Single(handoff.Transitions.Where(t => t.ToStatus == "REVERSED"));
        Assert.Equal("Payroll correction requested", transition.Reason);
        Assert.Equal("MOBILE_USER", transition.Actor);
    }

    [Fact]
    public async Task PreparePaymentAndConfirmPaymentRequireExplicitPayGrantOverHttp()
    {
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(s => s.PreparePaymentAsync(42, 100m, "USD", "BANK", "pay-key-1", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingHandoffPayment { Id = 10, Amount = 100m, Status = "PREPARED" });
        handoffs.Setup(s => s.ConfirmPaymentAsync(10, "ext-ref-1", "evid-ref-1", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingHandoffPayment { Id = 10, Amount = 100m, Status = "CONFIRMED" });
        accountingHandoffServiceOverride = handoffs.Object;

        using (var deniedPrepare = await Send("POST", "/api/accounting/hr-handoffs/42/payments",
            new { amount = 100, currency = "USD", method = "BANK", idempotencyKey = "pay-key-1" }))
            Assert.Equal(HttpStatusCode.Forbidden, deniedPrepare.StatusCode);

        using (var deniedConfirm = await Send("POST", "/api/accounting/hr-handoffs/payments/10/confirm",
            new { externalReference = "ext-ref-1", evidenceReference = "evid-ref-1" }))
            Assert.Equal(HttpStatusCode.Forbidden, deniedConfirm.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 306, UserId = 12, ScreenId = 80, FeatureId = 82, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var acceptedPrepare = await Send("POST", "/api/accounting/hr-handoffs/42/payments",
            new { amount = 100, currency = "USD", method = "BANK", idempotencyKey = "pay-key-1" });
        Assert.Equal(HttpStatusCode.OK, acceptedPrepare.StatusCode);
        handoffs.Verify(s => s.PreparePaymentAsync(42, 100m, "USD", "BANK", "pay-key-1", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);

        using var acceptedConfirm = await Send("POST", "/api/accounting/hr-handoffs/payments/10/confirm",
            new { externalReference = "ext-ref-1", evidenceReference = "evid-ref-1" });
        Assert.Equal(HttpStatusCode.OK, acceptedConfirm.StatusCode);
        handoffs.Verify(s => s.ConfirmPaymentAsync(10, "ext-ref-1", "evid-ref-1", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmLoanFundingRequiresExplicitFundGrantOverHttp()
    {
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(s => s.ConfirmLoanFundingAsync(42, 500m, "USD", 201L, "fund-evt-1", "ext-fund-1", "MOBILE_USER", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        accountingHandoffServiceOverride = handoffs.Object;

        using (var denied = await Send("POST", "/api/accounting/hr-handoffs/42/loan-funding-receipt",
            new { fundedAmount = 500, currency = "USD", fundingVoucherId = 201, eventKey = "fund-evt-1", externalReference = "ext-fund-1" }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysFeature { Id = 84, FeatureCode = "fund", IsActive = true });
            db.Add(new SysScreenFeature { ScreenId = 80, FeatureId = 84 });
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 307, UserId = 12, ScreenId = 80, FeatureId = 84, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var accepted = await Send("POST", "/api/accounting/hr-handoffs/42/loan-funding-receipt",
            new { fundedAmount = 500, currency = "USD", fundingVoucherId = 201, eventKey = "fund-evt-1", externalReference = "ext-fund-1" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        handoffs.Verify(s => s.ConfirmLoanFundingAsync(42, 500m, "USD", 201L, "fund-evt-1", "ext-fund-1", "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmLoanFundingHttpUsesRealServiceAndActivatesLoanFromPostedVoucher()
    {
        var voucherRepository = new Mock<IGlVoucherRepository>();
        voucherRepository.Setup(r => r.GetByIdAsync(201, It.IsAny<CancellationToken>())).ReturnsAsync(new GlVoucherHeader
        {
            Id = 201, BranchId = 7, VoucherType = 103, Status = 3, PostDate = DateTime.UtcNow,
            TotalAmount = 500m, TotalLocalDebit = 500m, TotalLocalCredit = 500m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 201, LineSer = 1, BranchId = 7, CurrencyId = 9, AccountCode = "LOAN-RECEIVABLE", Debit = 500m, LocalDebit = 500m, BaseDebit = 500m },
                new GlVoucherDetail { VoucherId = 201, LineSer = 2, BranchId = 7, CurrencyId = 9, AccountCode = "BANK", Credit = 500m, LocalCredit = 500m, BaseCredit = 500m }
            ]
        });
        var paymentVouchers = new Mock<IReceiptPaymentVoucherService>();
        paymentVouchers.Setup(s => s.GetPaymentVoucherByIdAsync(201, It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentVoucherDto
        {
            VoucherId = 201, BranchId = 7, CurrencyId = 9, Status = 3,
            Description = "Disbursement for HR-LOAN-1-55-1", TransferReference = "BANK-FUND-201"
        });
        accountingHandoffServiceFactory = sp => new HrAccountingHandoffService(
            sp.GetRequiredService<OracleDbContext>(), voucherRepository.Object, Mock.Of<IHrPayrollPostingService>(),
            paymentVouchers.Object, Mock.Of<IGlVoucherService>());

        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysFeature { Id = 84, FeatureCode = "fund", IsActive = true });
            db.Add(new SysScreenFeature { ScreenId = 80, FeatureId = 84 });
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 330, UserId = 12, ScreenId = 80, FeatureId = 84, BranchId = 7, IsGranted = true });
            db.SysCurrencies.Add(new SysCurrency { Id = 9, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 55, EmployeeCode = "SELF", CurrencyCode = "USD", PrincipalAmount = 500m,
                RemainingBalance = 500m, Status = "APPROVED", CreationUser = "hr", CreationDate = DateTime.UtcNow
            });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 42, CompanyId = 1, BranchId = 7, SourceType = "LOAN", SourceId = "55", SourceVersion = 1,
                Operation = "FUND", HandoffReference = "HR-LOAN-1-55-1", PayloadHash = "fund-payload",
                DeliveryStatus = "ACCEPTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines = [new HrAccountingHandoffLine { LineType = "PRINCIPAL", CurrencyCode = "USD", Amount = 500m, BranchId = 7, SourceReference = "LOAN:55" }]
            });
            await db.SaveChangesAsync();
        }

        using var response = await Send("POST", "/api/accounting/hr-handoffs/42/loan-funding-receipt",
            new { fundedAmount = 500, currency = "USD", fundingVoucherId = 201, eventKey = "fund-evt-201", externalReference = "BANK-FUND-201" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verify = new OracleDbContext(Options(1));
        var loan = await verify.EmployeeLoans.SingleAsync(x => x.Id == 55);
        Assert.Equal("ACTIVE", loan.Status);
        Assert.Equal(500m, loan.RemainingBalance);
        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync(x => x.Id == 42)).DeliveryStatus);
        var receipt = await verify.AccountingLoanFundingReceipts.SingleAsync(x => x.HandoffId == 42);
        Assert.Equal(201, receipt.FundingVoucherId);
        Assert.Equal("MOBILE_USER", receipt.ReceivedBy);
        voucherRepository.Verify(r => r.GetByIdAsync(201, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AccountingPaymentsHttpReturns404WhenSummaryNotFound()
    {
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(s => s.GetAccountingPaymentSummaryAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountingHandoffPaymentSummaryDto?)null);
        accountingHandoffServiceOverride = handoffs.Object;

        using (var db = new OracleDbContext(Options(1)))
        {
            db.SysUserScreenPermissions.Add(new SysUserScreenPermission
            { Id = 308, UserId = 12, ScreenId = 80, FeatureId = 62, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var notFound = await Send("GET", "/api/accounting/hr-handoffs/999/payments");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/accounting/hr-handoffs/42/journal-voucher")]
    [InlineData("POST", "/api/accounting/hr-handoffs/42/posting-receipt")]
    [InlineData("POST", "/api/accounting/hr-handoffs/42/payments")]
    [InlineData("POST", "/api/accounting/hr-handoffs/payments/10/confirm")]
    [InlineData("POST", "/api/accounting/hr-handoffs/42/loan-funding-receipt")]
    [InlineData("GET", "/api/hr/payroll/10/handoff")]
    [InlineData("GET", "/api/hr/payroll/10/handoff/payments")]
    [InlineData("POST", "/api/hr/payroll/10/handoff/withdraw")]
    public async Task AnonymousRequestsToHandoffEndpointsReturnUnauthorized(string method, string url)
    {
        using var response = await Send(method, url, company: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
