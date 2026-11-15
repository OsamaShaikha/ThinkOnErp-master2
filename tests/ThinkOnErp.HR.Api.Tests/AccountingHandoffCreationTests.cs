using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class AccountingHandoffCreationTests
{
    [Fact]
    public async Task RepeatedApprovalCreatesOneImmutableHandoffAndOneOutboxEvent()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        long firstId;
        string firstHash;
        await using (var db = harness.CreateDb())
        {
            var created = await harness.CreateService(db).CreateApprovedPayrollHandoffAsync(9, "approver");
            firstId = created.Id;
            firstHash = created.PayloadHash;
        }

        await using (var db = harness.CreateDb())
        {
            var repeated = await harness.CreateService(db).CreateApprovedPayrollHandoffAsync(9, "approver");
            Assert.Equal(firstId, repeated.Id);
            Assert.Equal(firstHash, repeated.PayloadHash);
        }

        await using var verify = harness.CreateDb();
        var handoff = await verify.HrAccountingHandoffs.Include(x => x.Lines).Include(x => x.Transitions).SingleAsync();
        Assert.Equal("QUEUED", handoff.DeliveryStatus);
        Assert.Equal(new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc), handoff.SourceApprovedAtUtc);
        Assert.Contains(handoff.Lines, line => line.LineType == "CONTROL:GROSS" && line.Amount == 100m);
        Assert.Contains(handoff.Lines, line => line.LineType == "CONTROL:NET_PAY" && line.Amount == 100m);
        var initialTransition = Assert.Single(handoff.Transitions);
        Assert.Equal("INITIAL", initialTransition.FromStatus);
        Assert.Equal("QUEUED", initialTransition.ToStatus);
        Assert.Single(await verify.HrAccountingOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task PayrollHandoffPayloadHashIsStableWhenComponentEnumerationOrderChanges()
    {
        using var orderedHarness = new HandoffCreationHarness();
        await orderedHarness.SeedAsync(loanDeduction: 10m, advanceBalance: 10m);
        string expectedHash;
        string[] expectedLineOrder;
        await using (var db = orderedHarness.CreateDb())
        {
            var handoff = await orderedHarness.CreateService(db).CreateApprovedPayrollHandoffAsync(9, "approver");
            expectedHash = handoff.PayloadHash;
            expectedLineOrder = handoff.Lines.Select(l => l.LineType).ToArray();
        }

        using var reorderedHarness = new HandoffCreationHarness();
        await reorderedHarness.SeedAsync(loanDeduction: 10m, advanceBalance: 10m);
        await using var reorderedDb = reorderedHarness.CreateDb();
        var run = await reorderedDb.PayrollRuns.Include(r => r.Lines).ThenInclude(l => l.Components).SingleAsync(r => r.Id == 9);
        run.Lines.Single().Components.Reverse();

        var reordered = await reorderedHarness.CreateService(reorderedDb).CreateApprovedPayrollHandoffAsync(9, "approver");

        Assert.Equal(expectedHash, reordered.PayloadHash);
        Assert.Equal(expectedLineOrder, reordered.Lines.Select(l => l.LineType).ToArray());
    }

    [Fact]
    public async Task PayrollHeaderMismatchDoesNotCreateHandoffOrOutbox()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync(totalGross: 99m);

        await using (var db = harness.CreateDb())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(9, "approver"));
        }

        await using var verify = harness.CreateDb();
        Assert.Empty(await verify.HrAccountingHandoffs.ToListAsync());
        Assert.Empty(await verify.HrAccountingOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task LoanDeductionIsSnapshottedAndReservedByTheCreatedHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync(loanDeduction: 10m, advanceBalance: 10m);

        await using (var db = harness.CreateDb())
        {
            var handoff = await harness.CreateService(db).CreateApprovedPayrollHandoffAsync(9, "approver");
            Assert.Contains(handoff.Lines, line => line.LineType == "REFERENCE:ADVANCE" && line.Amount == 10m &&
                line.SourceReference == "ADVANCE:41:PAYROLL_LINE:501");
            await using var verify = harness.CreateDb();
            Assert.Equal(handoff.Id, (await verify.EmployeeAdvances.SingleAsync()).ReservedByHandoffId);
        }
    }

    [Fact]
    public async Task UnfundedPayrollLoanDeductionDoesNotCreateHandoffOrReservation()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync(loanDeduction: 10m, advanceBalance: 5m);

        await using (var db = harness.CreateDb())
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db).CreateApprovedPayrollHandoffAsync(9, "approver"));

        await using var verify = harness.CreateDb();
        Assert.Empty(await verify.HrAccountingHandoffs.ToListAsync());
        Assert.Empty(await verify.HrAccountingOutboxMessages.ToListAsync());
        Assert.Null((await verify.EmployeeAdvances.SingleAsync()).ReservedByHandoffId);
    }

    [Fact]
    public async Task ApprovedEndOfServiceSettlementCreatesPayoutHandoffAndOutbox()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.SysCurrencies.Add(new SysCurrency { Id = 10, ShortNameEn = "JOD", CurrencyNameEn = "Jordanian Dinar", CurrencyNameLocal = "دينار أردني" });
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EndOfServiceSettlements.Add(new EndOfServiceSettlement
            {
                Id = 99,
                CompanyId = 3,
                BranchId = 2,
                EmployeeCode = "E-501",
                Status = "APPROVED",
                NetSettlementAmount = 3500m,
                CurrencyCode = "JOD",
                ApprovedAtUtc = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var handoff = await harness.CreateService(db).CreateApprovedFundingHandoffAsync("EOS", 99, "approver");
            Assert.Equal("QUEUED", handoff.DeliveryStatus);
            Assert.Equal("HR-EOS-3-99-1", handoff.HandoffReference);
            Assert.Contains(handoff.Lines, line => line.LineType == "EOS_SETTLEMENT" && line.Amount == 3500m && line.CurrencyCode == "JOD");
        }

        await using var verify = harness.CreateDb();
        var outbox = await verify.HrAccountingOutboxMessages.SingleAsync(m => m.EventType == "EOS_SETTLEMENT_PAYOUT_REQUEST");
        Assert.Equal("PENDING", outbox.Status);
    }

    [Fact]
    public async Task DraftOrZeroNetEndOfServiceSettlementRejectsPayoutHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.SysCurrencies.Add(new SysCurrency { Id = 10, ShortNameEn = "JOD", CurrencyNameEn = "Jordanian Dinar", CurrencyNameLocal = "دينار أردني" });
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EndOfServiceSettlements.Add(new EndOfServiceSettlement
            {
                Id = 100,
                CompanyId = 3,
                BranchId = 2,
                EmployeeCode = "E-501",
                Status = "DRAFT",
                NetSettlementAmount = 3500m,
                CurrencyCode = "JOD"
            });
            db.EndOfServiceSettlements.Add(new EndOfServiceSettlement
            {
                Id = 101,
                CompanyId = 3,
                BranchId = 2,
                EmployeeCode = "E-501",
                Status = "APPROVED",
                NetSettlementAmount = 0m,
                CurrencyCode = "JOD"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedFundingHandoffAsync("EOS", 100, "approver"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedFundingHandoffAsync("EOS", 101, "approver"));
        }
    }

    [Fact]
    public async Task MixedCurrenciesAcrossPayrollLinesRejectsHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 10,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                CorrectionVersion = 1,
                TotalGrossSalary = 200m,
                TotalNetSalary = 200m,
                ApprovalDate = DateTime.UtcNow,
                ApprovedBy = "approver",
                Lines =
                [
                    new PayrollRunLine { Id = 601, EmployeeCode = "E-1", BranchId = 2, GrossSalary = 100m, NetPay = 100m, CurrencyCode = "USD", Status = "APPROVED" },
                    new PayrollRunLine { Id = 602, EmployeeCode = "E-2", BranchId = 2, GrossSalary = 100m, NetPay = 100m, CurrencyCode = "JOD", Status = "APPROVED" }
                ]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(10, "approver"));
            Assert.Contains("explicit currency", ex.Message);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOrWhitespaceCurrencyOnPayrollLineRejectsHandoff(string currency)
    {
        using var harness = new HandoffCreationHarness();
        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 11,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                CorrectionVersion = 1,
                TotalGrossSalary = 100m,
                TotalNetSalary = 100m,
                ApprovalDate = DateTime.UtcNow,
                ApprovedBy = "approver",
                Lines =
                [
                    new PayrollRunLine { Id = 603, EmployeeCode = "E-1", BranchId = 2, GrossSalary = 100m, NetPay = 100m, CurrencyCode = currency, Status = "APPROVED" }
                ]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(11, "approver"));
            Assert.Contains("explicit currency", ex.Message);
        }
    }

    [Fact]
    public async Task NegativeNetPayOnPayrollLineRejectsHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 12,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                CorrectionVersion = 1,
                TotalGrossSalary = 100m,
                TotalNetSalary = -20m,
                TotalOtherDeductions = 120m,
                ApprovalDate = DateTime.UtcNow,
                ApprovedBy = "approver",
                Lines =
                [
                    new PayrollRunLine
                    {
                        Id = 604,
                        EmployeeCode = "E-1",
                        BranchId = 2,
                        GrossSalary = 100m,
                        NetPay = -20m,
                        OtherDeductions = 120m,
                        CurrencyCode = "USD",
                        Status = "APPROVED"
                    }
                ]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(12, "approver"));
            Assert.Contains("negative net pay", ex.Message);
        }
    }

    [Fact]
    public async Task NegativeGrossSalaryOnPayrollLineRejectsHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 13,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                CorrectionVersion = 1,
                TotalGrossSalary = -50m,
                TotalNetSalary = -50m,
                ApprovalDate = DateTime.UtcNow,
                ApprovedBy = "approver",
                Lines =
                [
                    new PayrollRunLine
                    {
                        Id = 605,
                        EmployeeCode = "E-1",
                        BranchId = 2,
                        GrossSalary = -50m,
                        NetPay = -50m,
                        CurrencyCode = "USD",
                        Status = "APPROVED"
                    }
                ]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(13, "approver"));
            Assert.Contains("negative net pay", ex.Message);
        }
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("CALCULATED")]
    [InlineData("CANCELLED")]
    public async Task UnapprovedPayrollRunRejectsHandoff(string status)
    {
        using var harness = new HandoffCreationHarness();
        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 14,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = status,
                CorrectionVersion = 1,
                TotalGrossSalary = 100m,
                TotalNetSalary = 100m,
                ApprovalDate = DateTime.UtcNow,
                ApprovedBy = "approver",
                Lines =
                [
                    new PayrollRunLine { Id = 606, EmployeeCode = "E-1", BranchId = 2, GrossSalary = 100m, NetPay = 100m, CurrencyCode = "USD", Status = status }
                ]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(14, "approver"));
            Assert.Contains("Only an approved payroll run", ex.Message);
        }
    }

    [Fact]
    public async Task PayrollRunWithoutApprovalDateRejectsHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 15,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                CorrectionVersion = 1,
                TotalGrossSalary = 100m,
                TotalNetSalary = 100m,
                ApprovalDate = null,
                ApprovedBy = "approver",
                Lines =
                [
                    new PayrollRunLine { Id = 607, EmployeeCode = "E-1", BranchId = 2, GrossSalary = 100m, NetPay = 100m, CurrencyCode = "USD", Status = "APPROVED" }
                ]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(15, "approver"));
            Assert.Contains("Only an approved payroll run", ex.Message);
        }
    }

    [Fact]
    public async Task EmptyPayrollRunRejectsHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 16,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                CorrectionVersion = 1,
                TotalGrossSalary = 0m,
                TotalNetSalary = 0m,
                ApprovalDate = DateTime.UtcNow,
                ApprovedBy = "approver",
                Lines = []
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(16, "approver"));
            Assert.Contains("empty payroll run", ex.Message);
        }
    }

    [Fact]
    public async Task InactivePayrollBranchRejectsHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = false });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 17,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                CorrectionVersion = 1,
                TotalGrossSalary = 100m,
                TotalNetSalary = 100m,
                ApprovalDate = DateTime.UtcNow,
                ApprovedBy = "approver",
                Lines =
                [
                    new PayrollRunLine { Id = 608, EmployeeCode = "E-1", BranchId = 2, GrossSalary = 100m, NetPay = 100m, CurrencyCode = "USD", Status = "APPROVED" }
                ]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(17, "approver"));
            Assert.Contains("inactive", ex.Message);
        }
    }

    [Fact]
    public async Task PayrollHeaderDeductionOrTaxMismatchRejectsHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 18,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                CorrectionVersion = 1,
                TotalGrossSalary = 100m,
                TotalNetSalary = 90m,
                TotalIncomeTax = 10m,
                TotalOtherDeductions = 5m, // Mismatch: lines have 0 other deductions
                ApprovalDate = DateTime.UtcNow,
                ApprovedBy = "approver",
                Lines =
                [
                    new PayrollRunLine
                    {
                        Id = 609,
                        EmployeeCode = "E-1",
                        BranchId = 2,
                        GrossSalary = 100m,
                        NetPay = 90m,
                        IncomeTaxWithheld = 10m,
                        OtherDeductions = 0m,
                        CurrencyCode = "USD",
                        Status = "APPROVED"
                    }
                ]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateApprovedPayrollHandoffAsync(18, "approver"));
            Assert.Contains("totals do not match", ex.Message);
        }
    }

    [Fact]
    public async Task CreateApprovedPayrollHandoff_NonExistentRun_ThrowsKeyNotFoundException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateApprovedPayrollHandoffAsync(9999, "approver"));
    }

    [Fact]
    public async Task CreateApprovedPayrollHandoff_WithLoanScheduleDeduction_SnapshotsAndReservesScheduleInstallment()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync(loanDeduction: 20m);

        await using (var db = harness.CreateDb())
        {
            var loan = new EmployeeLoan
            {
                Id = 51,
                EmployeeCode = "E-501",
                CurrencyCode = "USD",
                PrincipalAmount = 240m,
                TotalInstallments = 12,
                MonthlyInstallmentAmount = 20m,
                Status = "ACTIVE",
                RemainingBalance = 240m
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 81,
                EmployeeLoanId = 51,
                PayPeriod = "2026-11",
                InstallmentNo = 1,
                ScheduledAmount = 20m,
                PaidAmount = 0m,
                Status = "PENDING"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var handoff = await harness.CreateService(db).CreateApprovedPayrollHandoffAsync(9, "approver");
            Assert.Contains(handoff.Lines, line => line.LineType == "REFERENCE:SCHEDULE" && line.Amount == 20m &&
                line.SourceReference == "SCHEDULE:81:PAYROLL_LINE:501");

            await using var verify = harness.CreateDb();
            var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 81);
            Assert.Equal(handoff.Id, schedule.ReservedByHandoffId);
        }
    }

    [Fact]
    public async Task CreateApprovedPayrollHandoff_WithMultipleLoansAndAdvances_ReservesBothAccurately()
    {
        using var harness = new HandoffCreationHarness();
        // total deduction = 25m, advance covers 10m, schedule covers 15m
        await harness.SeedAsync(loanDeduction: 25m, advanceBalance: 10m);

        await using (var db = harness.CreateDb())
        {
            var loan = new EmployeeLoan
            {
                Id = 52,
                EmployeeCode = "E-501",
                CurrencyCode = "USD",
                PrincipalAmount = 150m,
                TotalInstallments = 10,
                MonthlyInstallmentAmount = 15m,
                Status = "ACTIVE",
                RemainingBalance = 150m
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 82,
                EmployeeLoanId = 52,
                PayPeriod = "2026-11",
                InstallmentNo = 1,
                ScheduledAmount = 15m,
                PaidAmount = 0m,
                Status = "PENDING"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var handoff = await harness.CreateService(db).CreateApprovedPayrollHandoffAsync(9, "approver");

            Assert.Contains(handoff.Lines, line => line.LineType == "REFERENCE:ADVANCE" && line.Amount == 10m);
            Assert.Contains(handoff.Lines, line => line.LineType == "REFERENCE:SCHEDULE" && line.Amount == 15m);

            await using var verify = harness.CreateDb();
            var advance = await verify.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 82);
            Assert.Equal(handoff.Id, advance.ReservedByHandoffId);
            Assert.Equal(handoff.Id, schedule.ReservedByHandoffId);
        }
    }

    [Fact]
    public async Task CreateApprovedPayrollHandoff_AdvanceCurrencyMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync(loanDeduction: 10m);

        await using (var db = harness.CreateDb())
        {
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 42,
                EmployeeCode = "E-501",
                AdvanceAmount = 10m,
                CurrencyCode = "EUR", // Mismatches USD payroll
                TargetPayPeriod = "2026-11",
                RemainingBalance = 10m,
                Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedPayrollHandoffAsync(9, "approver"));
        Assert.Contains("needs an explicit matching currency", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedPayrollHandoff_LoanCurrencyMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync(loanDeduction: 10m);

        await using (var db = harness.CreateDb())
        {
            var loan = new EmployeeLoan
            {
                Id = 53,
                EmployeeCode = "E-501",
                CurrencyCode = "EUR", // Mismatches USD payroll
                PrincipalAmount = 100m,
                TotalInstallments = 10,
                MonthlyInstallmentAmount = 10m,
                Status = "ACTIVE",
                RemainingBalance = 100m
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 83,
                EmployeeLoanId = 53,
                PayPeriod = "2026-11",
                InstallmentNo = 1,
                ScheduledAmount = 10m,
                PaidAmount = 0m,
                Status = "PENDING"
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedPayrollHandoffAsync(9, "approver"));
        Assert.Contains("needs an explicit matching currency", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_InvalidSourceType_ThrowsArgumentException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateApprovedFundingHandoffAsync("BONUS", 1, "approver"));
        Assert.Contains("Funding source must be LOAN, ADVANCE, or EOS", ex.Message);
    }

    [Theory]
    [InlineData("LOAN")]
    [InlineData("ADVANCE")]
    [InlineData("EOS")]
    public async Task CreateApprovedFundingHandoff_SourceNotFound_ThrowsKeyNotFoundException(string sourceType)
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateApprovedFundingHandoffAsync(sourceType, 9999, "approver"));
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_PendingLoan_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 55,
                EmployeeCode = "E-501",
                CurrencyCode = "USD",
                PrincipalAmount = 100m,
                Status = "PENDING"
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("LOAN", 55, "approver"));
        Assert.Contains("Only an HR-approved loan can be sent for funding", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_PendingAdvance_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 45,
                EmployeeCode = "E-501",
                CurrencyCode = "USD",
                AdvanceAmount = 100m,
                Status = "PENDING"
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("ADVANCE", 45, "approver"));
        Assert.Contains("Only an HR-approved advance can be sent for funding", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_ApprovedLoan_CreatesHandoffAndOutboxAndIsIdempotent()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 56,
                EmployeeCode = "E-501",
                CurrencyCode = "USD",
                PrincipalAmount = 500m,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        long handoffId;
        await using (var db = harness.CreateDb())
        {
            var handoff = await harness.CreateService(db).CreateApprovedFundingHandoffAsync("LOAN", 56, "approver");
            handoffId = handoff.Id;
            Assert.Equal("QUEUED", handoff.DeliveryStatus);
            Assert.Equal("HR-LOAN-3-56-1", handoff.HandoffReference);
            Assert.Contains(handoff.Lines, l => l.LineType == "PRINCIPAL" && l.Amount == 500m);
        }

        // Idempotent retry returns existing handoff
        await using (var db = harness.CreateDb())
        {
            var replay = await harness.CreateService(db).CreateApprovedFundingHandoffAsync("LOAN", 56, "approver");
            Assert.Equal(handoffId, replay.Id);
        }

        await using var verify = harness.CreateDb();
        var outbox = await verify.HrAccountingOutboxMessages.SingleAsync(m => m.EventType == "LOAN_FUNDING_REQUEST");
        Assert.Equal("PENDING", outbox.Status);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_ApprovedAdvance_CreatesHandoffAndOutboxAndIsIdempotent()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 46,
                EmployeeCode = "E-501",
                CurrencyCode = "USD",
                AdvanceAmount = 150m,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        long handoffId;
        await using (var db = harness.CreateDb())
        {
            var handoff = await harness.CreateService(db).CreateApprovedFundingHandoffAsync("ADVANCE", 46, "approver");
            handoffId = handoff.Id;
            Assert.Equal("QUEUED", handoff.DeliveryStatus);
            Assert.Equal("HR-ADVANCE-3-46-1", handoff.HandoffReference);
            Assert.Contains(handoff.Lines, l => l.LineType == "PRINCIPAL" && l.Amount == 150m);
        }

        // Idempotent retry returns existing handoff
        await using (var db = harness.CreateDb())
        {
            var replay = await harness.CreateService(db).CreateApprovedFundingHandoffAsync("ADVANCE", 46, "approver");
            Assert.Equal(handoffId, replay.Id);
        }

        await using var verify = harness.CreateDb();
        var outbox = await verify.HrAccountingOutboxMessages.SingleAsync(m => m.EventType == "LOAN_FUNDING_REQUEST");
        Assert.Equal("PENDING", outbox.Status);
    }

    [Fact]
    public async Task CreateApprovedPayrollHandoffAsync_PropagatesCancellationToken()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateApprovedPayrollHandoffAsync(9, "approver", cts.Token));
    }

    [Fact]
    public async Task CreateApprovedFundingHandoffAsync_PropagatesCancellationToken()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateApprovedFundingHandoffAsync("LOAN", 1, "approver", cts.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("BONUS")]
    [InlineData("SALARY")]
    [InlineData("INVALID_SOURCE")]
    public async Task CreateApprovedFundingHandoff_InvalidOrWhitespaceSourceType_ThrowsArgumentException(string sourceType)
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateApprovedFundingHandoffAsync(sourceType, 1, "approver"));
        Assert.Contains("Funding source must be LOAN, ADVANCE, or EOS", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_WhenEmployeeHasNoBranch_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.Employees.Add(new Employee { EmployeeCode = "E-NO-BRANCH", BranchId = null, NameEn = "No Branch", NameLocal = "بلا فرع" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 60,
                EmployeeCode = "E-NO-BRANCH",
                CurrencyCode = "USD",
                PrincipalAmount = 100m,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("LOAN", 60, "approver"));
        Assert.Contains("A branch is required for funding requests", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_WhenBranchIsInactive_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.SysBranches.Add(new SysBranch { Id = 77, CompanyId = 3, IsActive = false });
            db.Employees.Add(new Employee { EmployeeCode = "E-INACTIVE-BRANCH", BranchId = 77, NameEn = "Emp", NameLocal = "موظف" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 61,
                EmployeeCode = "E-INACTIVE-BRANCH",
                CurrencyCode = "USD",
                PrincipalAmount = 100m,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("LOAN", 61, "approver"));
        Assert.Contains("Funding branch is missing or inactive", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_WhenCurrencyEmptyOrWhitespace_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.Employees.Add(new Employee { EmployeeCode = "E-EMPTY-CURR", BranchId = 2, NameEn = "Emp", NameLocal = "موظف" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 62,
                EmployeeCode = "E-EMPTY-CURR",
                CurrencyCode = "   ",
                PrincipalAmount = 100m,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("LOAN", 62, "approver"));
        Assert.Contains("An explicit currency is required", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_WhenCurrencyNotFoundInSysCurrencies_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.Employees.Add(new Employee { EmployeeCode = "E-UNKNOWN-CURR", BranchId = 2, NameEn = "Emp", NameLocal = "موظف" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 63,
                EmployeeCode = "E-UNKNOWN-CURR",
                CurrencyCode = "XYZ",
                PrincipalAmount = 100m,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("LOAN", 63, "approver"));
        Assert.Contains("Funding currency must match one unique ERP currency code", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_WhenCurrencyAmbiguousInSysCurrencies_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.SysCurrencies.Add(new SysCurrency { Id = 201, ShortNameEn = "USD", CurrencyNameEn = "US Dollar 1" });
            db.SysCurrencies.Add(new SysCurrency { Id = 202, ShortNameEn = "USD", CurrencyNameEn = "US Dollar 2" });
            db.Employees.Add(new Employee { EmployeeCode = "E-AMBIG-CURR", BranchId = 2, NameEn = "Emp", NameLocal = "موظف" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 64,
                EmployeeCode = "E-AMBIG-CURR",
                CurrencyCode = "USD",
                PrincipalAmount = 100m,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("LOAN", 64, "approver"));
        Assert.Contains("Funding currency must match one unique ERP currency code", ex.Message);
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    [InlineData("ACTIVE")]
    [InlineData("PAID")]
    public async Task CreateApprovedFundingHandoff_WhenLoanHasNonApprovedStatus_ThrowsInvalidOperationException(string status)
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.Employees.Add(new Employee { EmployeeCode = "E-STATUS-LOAN", BranchId = 2, NameEn = "Emp", NameLocal = "موظف" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 65,
                EmployeeCode = "E-STATUS-LOAN",
                CurrencyCode = "USD",
                PrincipalAmount = 100m,
                Status = status
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("LOAN", 65, "approver"));
        Assert.Contains("Only an HR-approved loan can be sent for funding", ex.Message);
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    [InlineData("ACTIVE")]
    [InlineData("RECOVERED")]
    public async Task CreateApprovedFundingHandoff_WhenAdvanceHasNonApprovedStatus_ThrowsInvalidOperationException(string status)
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.Employees.Add(new Employee { EmployeeCode = "E-STATUS-ADV", BranchId = 2, NameEn = "Emp", NameLocal = "موظف" });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 66,
                EmployeeCode = "E-STATUS-ADV",
                CurrencyCode = "USD",
                AdvanceAmount = 100m,
                Status = status
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("ADVANCE", 66, "approver"));
        Assert.Contains("Only an HR-approved advance can be sent for funding", ex.Message);
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    [InlineData("SETTLED")]
    public async Task CreateApprovedFundingHandoff_WhenEndOfServiceHasNonApprovedStatus_ThrowsInvalidOperationException(string status)
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EndOfServiceSettlements.Add(new EndOfServiceSettlement
            {
                Id = 120,
                CompanyId = 3,
                BranchId = 2,
                EmployeeCode = "E-501",
                Status = status,
                NetSettlementAmount = 1000m,
                CurrencyCode = "USD"
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("EOS", 120, "approver"));
        Assert.Contains("Only an HR-approved end of service settlement can be sent for payout", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task CreateApprovedFundingHandoff_WhenEndOfServiceNegativeOrZero_ThrowsInvalidOperationException(decimal netAmount)
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EndOfServiceSettlements.Add(new EndOfServiceSettlement
            {
                Id = 121,
                CompanyId = 3,
                BranchId = 2,
                EmployeeCode = "E-501",
                Status = "APPROVED",
                NetSettlementAmount = netAmount,
                CurrencyCode = "USD"
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("EOS", 121, "approver"));
        Assert.Contains("net amount must be greater than zero", ex.Message);
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_ReversedFundingHandoff_IncrementsSourceVersionAndCreatesNewHandoff()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 70,
                EmployeeCode = "E-501",
                CurrencyCode = "USD",
                PrincipalAmount = 600m,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow
            });
            // Simulate prior version 1 handoff that was REVERSED
            var priorHandoff = new HrAccountingHandoff
            {
                Id = 501,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "LOAN",
                SourceId = "70",
                SourceVersion = 1,
                Operation = "FUND",
                DeliveryStatus = "REVERSED",
                HandoffReference = "HR-LOAN-3-70-1",
                PayloadHash = "PRIOR_HASH",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "approver"
            };
            db.HrAccountingHandoffs.Add(priorHandoff);
            // Simulate complete prior funding receipt reversal
            db.AccountingLoanFundingReceipts.Add(new AccountingLoanFundingReceipt
            {
                Id = 301,
                HandoffId = 501,
                SourceType = "LOAN",
                SourceId = "70",
                SourceVersion = 1,
                FundedAmount = 600m,
                CurrencyCode = "USD",
                FundingVoucherId = 8801,
                EventKey = "RECEIPT-KEY-001",
                ExternalReference = "DISB-REF-001",
                ReceivedAtUtc = DateTime.UtcNow.AddDays(-1),
                ReceivedBy = "accountant",
                ReversalVoucherId = 8802,
                ReversalEventKey = "REV-KEY-001",
                ReversalExternalReference = "REV-REF-001",
                ReversalEvidenceReference = "REV-EVID-001",
                ReversalReason = "Disbursement cancelled by bank",
                ReversedAtUtc = DateTime.UtcNow.AddHours(-1),
                ReversedBy = "accountant"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var newHandoff = await harness.CreateService(db).CreateApprovedFundingHandoffAsync("LOAN", 70, "approver");
            Assert.Equal(2, newHandoff.SourceVersion);
            Assert.Equal("HR-LOAN-3-70-2", newHandoff.HandoffReference);
            Assert.Equal("QUEUED", newHandoff.DeliveryStatus);
            Assert.Contains(newHandoff.Lines, l => l.LineType == "PRINCIPAL" && l.Amount == 600m);
        }
    }

    [Fact]
    public async Task CreateApprovedFundingHandoff_ReversedFundingWithoutCompleteReversalReceipt_ThrowsInvalidOperationException()
    {
        using var harness = new HandoffCreationHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            db.Employees.Add(new Employee { EmployeeCode = "E-501", BranchId = 2, NameEn = "John Doe", NameLocal = "جون دو" });
            db.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = 71,
                EmployeeCode = "E-501",
                CurrencyCode = "USD",
                PrincipalAmount = 600m,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow
            });
            // Simulate prior version 1 handoff marked REVERSED
            var priorHandoff = new HrAccountingHandoff
            {
                Id = 502,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "LOAN",
                SourceId = "71",
                SourceVersion = 1,
                Operation = "FUND",
                DeliveryStatus = "REVERSED",
                HandoffReference = "HR-LOAN-3-71-1",
                PayloadHash = "PRIOR_HASH",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "approver"
            };
            db.HrAccountingHandoffs.Add(priorHandoff);
            // Missing reversal metadata in receipt (ReversalVoucherId is null)
            db.AccountingLoanFundingReceipts.Add(new AccountingLoanFundingReceipt
            {
                Id = 302,
                HandoffId = 502,
                SourceType = "LOAN",
                SourceId = "71",
                SourceVersion = 1,
                FundedAmount = 600m,
                CurrencyCode = "USD",
                FundingVoucherId = 8803,
                EventKey = "RECEIPT-KEY-002",
                ExternalReference = "DISB-REF-002",
                ReceivedAtUtc = DateTime.UtcNow.AddDays(-1),
                ReceivedBy = "accountant",
                ReversalVoucherId = null // Incomplete reversal
            });
            await db.SaveChangesAsync();
        }

        await using var dbCall = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(dbCall).CreateApprovedFundingHandoffAsync("LOAN", 71, "approver"));
        Assert.Contains("A new funding version cannot be created until the prior funding reversal receipt is complete", ex.Message);
    }

    private sealed class HandoffCreationHarness : IDisposable
    {
        private readonly string storeName = Guid.NewGuid().ToString();
        private readonly DbContextOptions<OracleDbContext> options;

        public HandoffCreationHarness()
        {
            options = new DbContextOptionsBuilder<OracleDbContext>()
                .UseInMemoryDatabase(storeName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        public OracleDbContext CreateDb() => new(options);

        public HrAccountingHandoffService CreateService(OracleDbContext db) => new(
            db,
            Mock.Of<IGlVoucherRepository>(),
            Mock.Of<IHrPayrollPostingService>(),
            Mock.Of<IReceiptPaymentVoucherService>(),
            Mock.Of<IGlVoucherService>());

        public async Task SeedAsync(decimal totalGross = 100m, decimal loanDeduction = 0m, decimal advanceBalance = 0m)
        {
            await using var db = CreateDb();
            db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
            db.PayrollRuns.Add(new PayrollRun
            {
                Id = 9,
                PayPeriod = "2026-11",
                RunDate = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc),
                BranchId = 2,
                Status = "APPROVED",
                CorrectionVersion = 1,
                TotalGrossSalary = totalGross,
                TotalNetSalary = 100m - loanDeduction,
                TotalOtherDeductions = loanDeduction,
                ApprovalDate = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc),
                ApprovedBy = "approver",
                Lines =
                [
                    new PayrollRunLine
                    {
                        Id = 501,
                        EmployeeCode = "E-501",
                        BranchId = 2,
                        TotalEarnings = 100m,
                        GrossSalary = 100m,
                        NetPay = 100m - loanDeduction,
                        OtherDeductions = loanDeduction,
                        TotalDeductions = loanDeduction,
                        CurrencyCode = "USD",
                        Status = "APPROVED",
                        Components =
                        [
                            new PayrollRunLineComponent
                            {
                                Id = 700,
                                ComponentCode = "BASE",
                                ComponentType = "EARNING",
                                ComponentNameEn = "Base salary",
                                Amount = 100m
                            },
                            .. (loanDeduction > 0 ? new[] { new PayrollRunLineComponent
                            {
                                Id = 701,
                                ComponentCode = "LOAN_DEDUCTION",
                                ComponentType = "DEDUCTION",
                                ComponentNameEn = "Loan deduction",
                                Amount = loanDeduction
                            } } : Array.Empty<PayrollRunLineComponent>())
                        ]
                    }
                ]
            });
            if (advanceBalance > 0)
                db.EmployeeAdvances.Add(new EmployeeAdvance
                {
                    Id = 41, EmployeeCode = "E-501", AdvanceAmount = advanceBalance, CurrencyCode = "USD",
                    TargetPayPeriod = "2026-11", RemainingBalance = advanceBalance, Status = "ACTIVE"
                });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
