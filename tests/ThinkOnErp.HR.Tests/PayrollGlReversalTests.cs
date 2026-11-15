using System.Text.Json;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollGlReversalTests
{
    private static readonly ReversePayrollGlDto Request = new(new DateTime(2027, 1, 5), "Correct posting");
    private static async Task<PayrollPostingTests.Fixture> Posted(string serial = "MONTHLY")
    {
        var fixture = new PayrollPostingTests.Fixture(serialPolicy: serial);
        await fixture.Service.PostPayrollToGlAsync(7, "HR");
        return fixture;
    }

    [Fact]
    public async Task ReversalCreatesExactInverseInRequestedYearAndRetainsAuditAndRetry()
    {
        var f = await Posted(); var original = f.Journals.Single();
        foreach (var d in original.Details)
        { d.CostCenterCode = "MAIN"; d.CostCenterMgrCode = "MGR"; d.CostCenterMnrCode = "MINOR"; d.PartyType = "EMPLOYEE"; d.PartyCode = "E1"; }
        var details = JsonSerializer.Serialize(original.Details);
        var result = await f.Service.ReversePayrollGlVoucherAsync(7, Request, "ACCOUNTANT");
        var reversal = f.Journals.Single(v => v.SourceSystemCode == "HR_PAYROLL_REVERSAL");
        Assert.Equal(original.Id, reversal.SourceRefId); Assert.Equal(original.Id, result.JournalVoucherId);
        Assert.Equal(13, reversal.FiscalYearId); Assert.Equal(2027, reversal.VoucherYear); Assert.Equal(1, reversal.VoucherMonth);
        Assert.Equal(Request.ReversalDate, reversal.VoucherDate); Assert.Equal(3, reversal.Status);
        Assert.Equal("Payroll GL reversal: Correct posting", reversal.Description);
        Assert.Equal("ACCOUNTANT", reversal.PostUser); Assert.True(original.IsReversed); Assert.Equal(3, original.Status);
        Assert.Equal(details, JsonSerializer.Serialize(original.Details));
        Assert.Equal("APPROVED", result.Status); Assert.Equal(790m, result.TotalNetSalary);
        Assert.Equal(1140m, reversal.TotalAmount);
        foreach (var d in original.Details)
        {
            var inverse = reversal.Details.Single(r => r.LineSer == d.LineSer);
            Assert.Equal(d.Debit, inverse.Credit); Assert.Equal(d.Credit, inverse.Debit);
            Assert.Equal(d.LocalDebit, inverse.LocalCredit); Assert.Equal(d.LocalCredit, inverse.LocalDebit);
            Assert.Equal(d.BaseDebit, inverse.BaseCredit); Assert.Equal(d.BaseCredit, inverse.BaseDebit);
            Assert.Equal(d.AccountCode, inverse.AccountCode); Assert.Equal(d.CurrencyId, inverse.CurrencyId);
            Assert.Equal(d.ExchangeRate, inverse.ExchangeRate); Assert.Equal(d.BranchId, inverse.BranchId);
            Assert.Equal(d.CostCenterCode, inverse.CostCenterCode); Assert.Equal(d.CostCenterMgrCode, inverse.CostCenterMgrCode);
            Assert.Equal(d.CostCenterMnrCode, inverse.CostCenterMnrCode); Assert.Equal(d.PartyType, inverse.PartyType); Assert.Equal(d.PartyCode, inverse.PartyCode);
        }
        await f.Service.ReversePayrollGlVoucherAsync(7, Request, "RETRY");
        Assert.Equal(2, f.Journals.Count); Assert.Equal("ACCOUNTANT", original.ReverseUser);
        f.Gl.Verify(g => g.GenerateNextSerialNoAsync(3, 2027, 1, 4, "MONTHLY", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory] [InlineData("MONTHLY")] [InlineData("YEARLY")] [InlineData("CONTINUOUS")]
    public async Task PostingUsesConfiguredSerialPolicyAndIdenticalRetryHasNoWrites(string policy)
    {
        var f = await Posted(policy); var first = await f.Service.PostPayrollToGlAsync(7, "RETRY");
        Assert.Single(f.Journals); Assert.Equal(99, first.JournalVoucherId);
        f.Gl.Verify(g => g.GenerateNextSerialNoAsync(3, 2026, 12, 4, policy, It.IsAny<CancellationToken>()), Times.Once);
        f.Gl.Verify(g => g.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepostAfterProvenReversalStartsNewLinkedCycleAndRetainsPriorHistory()
    {
        var f = await Posted(); await f.Service.ReversePayrollGlVoucherAsync(7, Request, "HR");
        // Reposting retains the payroll's configured posting date; its ERP window must resolve again.
        await f.Service.PostPayrollToGlAsync(7, "HR");
        Assert.Equal(3, f.Journals.Count); Assert.Equal(101, f.Run.JournalVoucherId);
        Assert.Equal("POSTED_TO_GL", f.Run.Status);
        await f.Service.ReversePayrollGlVoucherAsync(7, Request with { Reason = "Second cycle" }, "HR");
        Assert.Equal(4, f.Journals.Count);
        Assert.Equal(2, f.Journals.Count(v => v.SourceSystemCode == "HR_PAYROLL_REVERSAL"));
    }

    [Theory] [InlineData("date")] [InlineData("reason")] [InlineData("proof")]
    public async Task RetryCannotChangeReversalOrAcceptTamperedInverse(string change)
    {
        var f = await Posted(); await f.Service.ReversePayrollGlVoucherAsync(7, Request, "HR");
        var request = change == "date" ? Request with { ReversalDate = Request.ReversalDate.AddDays(1) } :
            change == "reason" ? Request with { Reason = "Different reason" } : Request;
        if (change == "proof") f.Journals.Last().Details[0].AccountCode = "OTHER";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ReversePayrollGlVoucherAsync(7, request, "HR"));
        Assert.Equal(2, f.Journals.Count);
    }

    [Theory] [InlineData("missing")] [InlineData("branch")] [InlineData("source")] [InlineData("reference")]
    [InlineData("status")] [InlineData("empty")] [InlineData("settlement")] [InlineData("unbalanced")]
    public async Task InvalidOriginalCannotBeReversedOrUnlinked(string issue)
    {
        var f = await Posted(); var original = f.Journals.Single();
        switch (issue)
        {
            case "missing": f.Journals.Clear(); break;
            case "branch": original.BranchId = 4; break;
            case "source": original.SourceSystemCode = "OTHER"; break;
            case "reference": original.SourceRefId = 8; break;
            case "status": original.Status = 4; break;
            case "empty": original.Details.Clear(); break;
            case "settlement": original.Details[0].IsSettlement = true; break;
            case "unbalanced": original.Details[0].Debit += 1; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ReversePayrollGlVoucherAsync(7, Request, "HR"));
        Assert.Equal("POSTED_TO_GL", f.Run.Status); Assert.Equal(99, f.Run.JournalVoucherId); Assert.False(original.IsReversed);
    }

    [Theory] [InlineData("PAID")] [InlineData("CALCULATED")] [InlineData("CANCELLED")]
    public async Task UnsupportedPayrollStateCannotReverse(string status)
    {
        var f = await Posted(); f.Run.Status = status;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ReversePayrollGlVoucherAsync(7, Request, "HR"));
        Assert.Single(f.Journals); Assert.False(f.Journals[0].IsReversed);
    }

    [Fact]
    public async Task ClosedTargetPeriodRejectsBeforeSerialFlagsAndWrites()
    {
        var f = await Posted(); f.Gl.Invocations.Clear();
        f.Posting.Setup(p => p.ResolveReversalAsync(3, Request.ReversalDate, 4, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Closed ERP period"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ReversePayrollGlVoucherAsync(7, Request, "HR"));
        f.Gl.Verify(g => g.AddVoucherAsync(It.IsAny<GlVoucherHeader>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Gl.Verify(g => g.GenerateNextSerialNoAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(f.Journals[0].IsReversed); Assert.Equal("POSTED_TO_GL", f.Run.Status);
    }

    [Theory] [InlineData("missing-date")] [InlineData("missing-reason")] [InlineData("before-original")]
    public async Task ExplicitDateAndReasonAreRequired(string issue)
    {
        var f = await Posted(); var request = issue == "missing-date" ? Request with { ReversalDate = default } :
            issue == "missing-reason" ? Request with { Reason = " " } : Request with { ReversalDate = new DateTime(2026, 12, 1) };
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ReversePayrollGlVoucherAsync(7, request, "HR"));
        Assert.Single(f.Journals);
    }

    [Theory] [InlineData("unlinked")] [InlineData("duplicate")] [InlineData("legacy-reversed")] [InlineData("draft-history")]
    public async Task PostingRejectsUnprovenOrDuplicateHistory(string issue)
    {
        var f = await Posted(); var original = f.Journals.Single();
        if (issue == "unlinked") { f.Run.Status = "APPROVED"; f.Run.JournalVoucherId = null; }
        if (issue == "duplicate") f.Journals.Add(new GlVoucherHeader { Id = 500, BranchId = 3, SourceSystemCode = "HR_PAYROLL", SourceRefId = 7, Status = 3 });
        if (issue == "legacy-reversed") { f.Run.Status = "APPROVED"; original.IsReversed = true; }
        if (issue == "draft-history") f.Journals.Add(new GlVoucherHeader { Id = 500, BranchId = 3, SourceSystemCode = "HR_PAYROLL", SourceRefId = 7, Status = 1 });
        var count = f.Journals.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.PostPayrollToGlAsync(7, "HR"));
        Assert.Equal(count, f.Journals.Count);
    }

    [Fact]
    public async Task PostingAndReversalUseFinancialPeriodLockRatherThanGenericTransaction()
    {
        var uow = new Mock<IHrUnitOfWork>(MockBehavior.Strict);
        uow.Setup(u => u.ExecutePayrollFinancialAsync(1, "2026-11", It.IsAny<Func<Task<PostGlVoucherResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((long c, string p, Func<Task<PostGlVoucherResultDto>> action, CancellationToken ct) => action());
        uow.Setup(u => u.ExecutePayrollFinancialAsync(1, "2026-11", It.IsAny<Func<Task<PayrollRun>>>(), It.IsAny<CancellationToken>()))
            .Returns((long c, string p, Func<Task<PayrollRun>> action, CancellationToken ct) => action());
        var f = new PayrollPostingTests.Fixture(unitOfWork: uow.Object);
        f.Repository.Setup(r => r.GetPayrollRunSnapshotAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(f.Run);
        await f.Service.PostPayrollToGlAsync(7, "HR"); await f.Service.ReversePayrollGlVoucherAsync(7, Request, "HR");
        Assert.Equal(2, uow.Invocations.Count);
    }

    [Fact]
    public async Task PostingRechecksRunStateInsideTheFinancialLock()
    {
        var uow = new Mock<IHrUnitOfWork>(MockBehavior.Strict);
        var f = new PayrollPostingTests.Fixture(unitOfWork: uow.Object);
        f.Repository.Setup(r => r.GetPayrollRunSnapshotAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(f.Run);
        uow.Setup(u => u.ExecutePayrollFinancialAsync(1, "2026-11", It.IsAny<Func<Task<PostGlVoucherResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((long c, string p, Func<Task<PostGlVoucherResultDto>> action, CancellationToken ct) => { f.Run.Status = "PAID"; return action(); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.PostPayrollToGlAsync(7, "HR"));
        Assert.Empty(f.Journals);
        f.Gl.Verify(g => g.GenerateNextSerialNoAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
