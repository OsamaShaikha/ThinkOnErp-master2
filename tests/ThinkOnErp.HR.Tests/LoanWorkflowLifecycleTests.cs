using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class LoanWorkflowLifecycleTests
{
    private readonly Mock<ILoanRepository> _loanRepoMock;
    private readonly LoanDeductionService _service;

    public LoanWorkflowLifecycleTests()
    {
        _loanRepoMock = new Mock<ILoanRepository>();
        _service = new LoanDeductionService(_loanRepoMock.Object);
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenValidPending_DefersInstallmentAndAppendsNewSchedule()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            EmployeeCode = "EMP001",
            Status = "ACTIVE",
            PrincipalAmount = 1000m,
            RemainingBalance = 1000m,
            TotalInstallments = 2,
            CurrencyCode = "JOD",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 3, 1),
            Schedules = new List<LoanRepaymentSchedule>()
        };

        var sch1 = new LoanRepaymentSchedule
        {
            Id = 10,
            EmployeeLoanId = 1,
            InstallmentNo = 1,
            PayPeriod = "2026-01",
            ScheduledAmount = 500m,
            PaidAmount = 0m,
            Status = "PENDING",
            EmployeeLoan = loan
        };

        var sch2 = new LoanRepaymentSchedule
        {
            Id = 11,
            EmployeeLoanId = 1,
            InstallmentNo = 2,
            PayPeriod = "2026-02",
            ScheduledAmount = 500m,
            PaidAmount = 0m,
            Status = "PENDING",
            EmployeeLoan = loan
        };

        loan.Schedules.Add(sch1);
        loan.Schedules.Add(sch2);

        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sch1);

        var result = await _service.SkipInstallmentAsync(10, "Financial emergency", "admin");

        Assert.Equal("SKIPPED", result.Status);
        Assert.Equal(3, loan.Schedules.Count);
        Assert.Equal(3, loan.TotalInstallments);

        var newSchedule = loan.Schedules.Last();
        Assert.Equal(3, newSchedule.InstallmentNo);
        Assert.Equal("2026-03", newSchedule.PayPeriod);
        Assert.Equal(500m, newSchedule.ScheduledAmount);
        Assert.Equal("PENDING", newSchedule.Status);

        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenNotPending_ThrowsInvalidOperationException()
    {
        var sch = new LoanRepaymentSchedule
        {
            Id = 10,
            Status = "PAID",
            ScheduledAmount = 500m
        };

        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sch);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SkipInstallmentAsync(10, "Defer reason", "admin"));
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenReservedByHandoff_ThrowsInvalidOperationException()
    {
        var sch = new LoanRepaymentSchedule
        {
            Id = 10,
            Status = "PENDING",
            ReservedByHandoffId = 999
        };

        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sch);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SkipInstallmentAsync(10, "Defer reason", "admin"));
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenLoanNotActive_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan { Id = 1, Status = "PENDING" };
        var sch = new LoanRepaymentSchedule
        {
            Id = 10,
            Status = "PENDING",
            EmployeeLoanId = 1,
            EmployeeLoan = loan
        };

        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sch);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SkipInstallmentAsync(10, "Defer reason", "admin"));
    }

    [Fact]
    public async Task CancelLoanAsync_WhenPendingOrApproved_SetsCancelledAndCancelsSchedules()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            Status = "APPROVED",
            TotalPaidAmount = 0m,
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 10, Status = "PENDING" },
                new() { Id = 11, Status = "PENDING" }
            }
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var result = await _service.CancelLoanAsync(1, "Employee withdrew application", "admin");

        Assert.Equal("CANCELLED", result.Status);
        Assert.All(result.Schedules, s => Assert.Equal("CANCELLED", s.Status));
        Assert.Contains("Employee withdrew application", result.Notes);

        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelLoanAsync_WhenActive_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan { Id = 1, Status = "ACTIVE" };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLoanAsync(1, "Cancel reason", "admin"));
    }

    [Fact]
    public async Task CancelLoanAsync_WhenHasRecordedPayments_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            Status = "APPROVED",
            TotalPaidAmount = 100m
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLoanAsync(1, "Cancel reason", "admin"));
    }

    [Fact]
    public async Task CancelLoanAsync_WhenReservedByHandoff_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            Status = "APPROVED",
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 10, Status = "PENDING", ReservedByHandoffId = 88 }
            }
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLoanAsync(1, "Cancel reason", "admin"));
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenPendingOrApproved_SetsCancelled()
    {
        var advance = new EmployeeAdvance
        {
            Id = 2,
            Status = "PENDING",
            DeductedAmount = 0m,
            AdvanceAmount = 300m,
            RemainingBalance = 300m
        };

        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var result = await _service.CancelAdvanceAsync(2, "No longer needed", "admin");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Contains("No longer needed", result.Reason);

        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(advance, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenActive_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance { Id = 2, Status = "ACTIVE" };

        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelAdvanceAsync(2, "Cancel reason", "admin"));
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenHasDeductions_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance
        {
            Id = 2,
            Status = "APPROVED",
            DeductedAmount = 50m
        };

        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelAdvanceAsync(2, "Cancel reason", "admin"));
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_WhenFullBalance_SettlesLoanAndMarksSchedulesPaid()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            Status = "ACTIVE",
            PrincipalAmount = 600m,
            TotalPaidAmount = 200m,
            RemainingBalance = 400m,
            CurrencyCode = "JOD",
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 1, InstallmentNo = 1, ScheduledAmount = 200m, PaidAmount = 200m, Status = "PAID" },
                new() { Id = 2, InstallmentNo = 2, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" },
                new() { Id = 3, InstallmentNo = 3, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" }
            }
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(400m, "REC-12345", "Settled in full via cashier receipt");
        var result = await _service.SettleLoanEarlyAsync(1, dto, "admin");

        Assert.Equal("SETTLED", result.Status);
        Assert.Equal(600m, result.TotalPaidAmount);
        Assert.Equal(0m, result.RemainingBalance);
        Assert.All(result.Schedules, s => Assert.Equal("PAID", s.Status));
        Assert.Contains("REC-12345", result.Notes);

        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_WhenPartialAmount_ReducesBalanceAndUpdatesSchedules()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            Status = "ACTIVE",
            PrincipalAmount = 600m,
            TotalPaidAmount = 0m,
            RemainingBalance = 600m,
            CurrencyCode = "JOD",
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 1, InstallmentNo = 1, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" },
                new() { Id = 2, InstallmentNo = 2, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" },
                new() { Id = 3, InstallmentNo = 3, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" }
            }
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(250m, "REC-555", "Partial payment");
        var result = await _service.SettleLoanEarlyAsync(1, dto, "admin");

        Assert.Equal("ACTIVE", result.Status);
        Assert.Equal(250m, result.TotalPaidAmount);
        Assert.Equal(350m, result.RemainingBalance);

        // Schedule 1: 200 fully paid
        Assert.Equal("PAID", result.Schedules[0].Status);
        Assert.Equal(200m, result.Schedules[0].PaidAmount);

        // Schedule 2: 50 partially paid
        Assert.Equal("PARTIAL", result.Schedules[1].Status);
        Assert.Equal(50m, result.Schedules[1].PaidAmount);

        // Schedule 3: 0 paid, still PENDING
        Assert.Equal("PENDING", result.Schedules[2].Status);
        Assert.Equal(0m, result.Schedules[2].PaidAmount);
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_WhenAmountExceedsRemaining_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            EmployeeCode = "E1",
            Status = "ACTIVE",
            RemainingBalance = 300m,
            CurrencyCode = "JOD"
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(350m, "REC-111", "Overpayment");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SettleLoanEarlyAsync(1, dto, "admin"));
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_WhenReservedByHandoff_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            EmployeeCode = "E1",
            Status = "ACTIVE",
            RemainingBalance = 300m,
            CurrencyCode = "JOD",
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 1, Status = "PENDING", ReservedByHandoffId = 444 }
            }
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(200m, "REC-111", "Payment");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SettleLoanEarlyAsync(1, dto, "admin"));
    }

    [Fact]
    public async Task SettleAdvanceEarlyAsync_WhenValid_SettlesAdvanceToRecovered()
    {
        var advance = new EmployeeAdvance
        {
            Id = 5,
            Status = "ACTIVE",
            AdvanceAmount = 500m,
            DeductedAmount = 100m,
            RemainingBalance = 400m,
            CurrencyCode = "JOD"
        };

        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var dto = new SettleAdvanceEarlyDto(400m, "REC-999", "Cash paid to finance");
        var result = await _service.SettleAdvanceEarlyAsync(5, dto, "admin");

        Assert.Equal("RECOVERED", result.Status);
        Assert.Equal(500m, result.DeductedAmount);
        Assert.Equal(0m, result.RemainingBalance);
        Assert.Contains("REC-999", result.Reason);

        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(advance, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SettleAdvanceEarlyAsync_WhenReservedByHandoff_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance
        {
            Id = 5,
            EmployeeCode = "E1",
            Status = "ACTIVE",
            AdvanceAmount = 500m,
            RemainingBalance = 500m,
            CurrencyCode = "JOD",
            ReservedByHandoffId = 77
        };

        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var dto = new SettleAdvanceEarlyDto(500m, "REC-999", "Cash paid");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SettleAdvanceEarlyAsync(5, dto, "admin"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SkipInstallmentAsync_WhenReasonNullOrWhitespace_ThrowsArgumentException(string? reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SkipInstallmentAsync(101, reason!, "admin"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelLoanAsync_WhenReasonNullOrWhitespace_ThrowsArgumentException(string? reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelLoanAsync(10, reason!, "admin"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelAdvanceAsync_WhenReasonNullOrWhitespace_ThrowsArgumentException(string? reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelAdvanceAsync(5, reason!, "admin"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task SettleLoanEarlyAsync_WhenAmountZeroOrNegative_ThrowsArgumentException(decimal amount)
    {
        var dto = new SettleLoanEarlyDto(amount, "REF", "Notes");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SettleLoanEarlyAsync(10, dto, "admin"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task SettleAdvanceEarlyAsync_WhenAmountZeroOrNegative_ThrowsArgumentException(decimal amount)
    {
        var dto = new SettleAdvanceEarlyDto(amount, "REF", "Notes");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SettleAdvanceEarlyAsync(5, dto, "admin"));
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_WhenLoanNotActive_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan
        {
            Id = 10,
            Status = "PENDING",
            PrincipalAmount = 1000m,
            RemainingBalance = 1000m
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(500m, "REC-123", "Cash");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SettleLoanEarlyAsync(10, dto, "admin"));
    }

    [Fact]
    public async Task CreateLoanApplicationAsync_GeneratesMonthlySchedulesAndSetsPendingStatus()
    {
        var dto = new CreateLoanApplicationDto(
            "EMP001",
            "PERSONAL",
            1200m,
            3,
            new DateTime(2026, 1, 1),
            "Personal emergency",
            "USD");

        var loan = await _service.CreateLoanApplicationAsync(dto, "admin");

        Assert.Equal("EMP001", loan.EmployeeCode);
        Assert.Equal("PENDING", loan.Status);
        Assert.Equal(1200m, loan.PrincipalAmount);
        Assert.Equal(1200m, loan.RemainingBalance);
        Assert.Equal(0m, loan.TotalPaidAmount);
        Assert.Equal("USD", loan.CurrencyCode);
        Assert.Equal(400m, loan.MonthlyInstallmentAmount);
        Assert.Equal(3, loan.Schedules.Count);
        Assert.Equal("2026-01", loan.Schedules[0].PayPeriod);
        Assert.Equal("2026-02", loan.Schedules[1].PayPeriod);
        Assert.Equal("2026-03", loan.Schedules[2].PayPeriod);
        Assert.Equal(1200m, loan.Schedules.Sum(s => s.ScheduledAmount));

        _loanRepoMock.Verify(r => r.AddLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveLoanAsync_WhenPending_SetsStatusToApproved()
    {
        var loan = new EmployeeLoan
        {
            Id = 25,
            EmployeeCode = "EMP001",
            Status = "PENDING",
            PrincipalAmount = 1000m,
            CurrencyCode = "JOD"
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var approved = await _service.ApproveLoanAsync(25, "hr_manager");

        Assert.Equal("APPROVED", approved.Status);
        Assert.Equal("hr_manager", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovalDate);
        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveLoanAsync_WhenAlreadyApproved_ReturnsLoanIdempotently()
    {
        var loan = new EmployeeLoan
        {
            Id = 26,
            EmployeeCode = "EMP001",
            Status = "APPROVED",
            ApprovedBy = "original_approver",
            CurrencyCode = "JOD"
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(26, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var approved = await _service.ApproveLoanAsync(26, "hr_manager");

        Assert.Equal("APPROVED", approved.Status);
        Assert.Equal("original_approver", approved.ApprovedBy);
        _loanRepoMock.Verify(r => r.UpdateLoanAsync(It.IsAny<EmployeeLoan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveLoanAsync_WhenNotPending_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan
        {
            Id = 27,
            Status = "ACTIVE"
        };

        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(27, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveLoanAsync(27, "admin"));
    }

    [Fact]
    public async Task ApproveLoanAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeLoan?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ApproveLoanAsync(999, "admin"));
    }

    [Fact]
    public async Task CreateAdvanceRequestAsync_SetsPendingStatusAndRemainingBalance()
    {
        var dto = new CreateAdvanceRequestDto(
            "EMP002",
            500m,
            "2026-06",
            "Medical expense",
            "JOD");

        var advance = await _service.CreateAdvanceRequestAsync(dto, "admin");

        Assert.Equal("EMP002", advance.EmployeeCode);
        Assert.Equal("PENDING", advance.Status);
        Assert.Equal(500m, advance.AdvanceAmount);
        Assert.Equal(500m, advance.RemainingBalance);
        Assert.Equal(0m, advance.DeductedAmount);
        Assert.Equal("JOD", advance.CurrencyCode);
        Assert.Equal("2026-06", advance.TargetPayPeriod);

        _loanRepoMock.Verify(r => r.AddAdvanceAsync(advance, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAdvanceAsync_WhenPending_SetsStatusToApproved()
    {
        var advance = new EmployeeAdvance
        {
            Id = 35,
            EmployeeCode = "EMP002",
            Status = "PENDING",
            AdvanceAmount = 300m,
            CurrencyCode = "JOD"
        };

        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(35, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var approved = await _service.ApproveAdvanceAsync(35, "hr_manager");

        Assert.Equal("APPROVED", approved.Status);
        Assert.Equal("hr_manager", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovalDate);
        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(advance, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAdvanceAsync_WhenAlreadyApproved_ReturnsAdvanceIdempotently()
    {
        var advance = new EmployeeAdvance
        {
            Id = 36,
            EmployeeCode = "EMP002",
            Status = "APPROVED",
            ApprovedBy = "first_approver",
            CurrencyCode = "JOD"
        };

        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(36, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var approved = await _service.ApproveAdvanceAsync(36, "hr_manager");

        Assert.Equal("APPROVED", approved.Status);
        Assert.Equal("first_approver", approved.ApprovedBy);
        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(It.IsAny<EmployeeAdvance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAdvanceAsync_WhenNotPending_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance
        {
            Id = 37,
            Status = "ACTIVE"
        };

        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(37, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveAdvanceAsync(37, "admin"));
    }

    [Fact]
    public async Task ApproveAdvanceAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeAdvance?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ApproveAdvanceAsync(999, "admin"));
    }

    [Fact]
    public async Task GetLoansAsync_DelegatesToRepository()
    {
        var loans = new List<EmployeeLoan> { new() { Id = 1, EmployeeCode = "EMP001" } };
        _loanRepoMock.Setup(r => r.GetLoansAsync("EMP001", "ACTIVE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(loans);

        var result = await _service.GetLoansAsync("EMP001", "ACTIVE");

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
        _loanRepoMock.Verify(r => r.GetLoansAsync("EMP001", "ACTIVE", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAdvancesAsync_DelegatesToRepository()
    {
        var advances = new List<EmployeeAdvance> { new() { Id = 2, EmployeeCode = "EMP002" } };
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("EMP002", "2026-06", It.IsAny<CancellationToken>()))
            .ReturnsAsync(advances);

        var result = await _service.GetAdvancesAsync("EMP002", "2026-06");

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
        _loanRepoMock.Verify(r => r.GetAdvancesAsync("EMP002", "2026-06", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLoanApplicationAsync_UnevenInstallments_CorrectsFinalInstallmentToMatchPrincipalExactly()
    {
        var dto = new CreateLoanApplicationDto(
            "EMP001",
            "PERSONAL",
            1000m,
            3,
            new DateTime(2026, 1, 1),
            "Three installments",
            "jod");

        var loan = await _service.CreateLoanApplicationAsync(dto, "admin");

        Assert.Equal("JOD", loan.CurrencyCode);
        Assert.Equal(3, loan.Schedules.Count);
        Assert.Equal(333m, loan.Schedules[0].ScheduledAmount);
        Assert.Equal(333m, loan.Schedules[1].ScheduledAmount);
        // Final installment absorbs the remainder at the configured currency precision.
        Assert.Equal(334m, loan.Schedules[2].ScheduledAmount);
        Assert.Equal(1000m, loan.Schedules.Sum(s => s.ScheduledAmount));
    }

    [Fact]
    public async Task CreateLoanApplicationAsync_WhenSingleInstallment_SetsScheduledAmountEqualToPrincipal()
    {
        var dto = new CreateLoanApplicationDto(
            "EMP001",
            "PERSONAL",
            750m,
            1,
            new DateTime(2026, 1, 1),
            "Single installment loan",
            "USD");

        var loan = await _service.CreateLoanApplicationAsync(dto, "admin");

        Assert.Single(loan.Schedules);
        Assert.Equal(750m, loan.Schedules[0].ScheduledAmount);
        Assert.Equal("PENDING", loan.Schedules[0].Status);
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenScheduleNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoanRepaymentSchedule?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.SkipInstallmentAsync(999, "Valid reason", "admin"));
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenLastDateInDecember_AdvancesNextPeriodToJanuaryNextYear()
    {
        var loan = new EmployeeLoan
        {
            Id = 5,
            EmployeeCode = "EMP005",
            Status = "ACTIVE",
            PrincipalAmount = 1000m,
            RemainingBalance = 500m,
            TotalInstallments = 1,
            CurrencyCode = "USD",
            Schedules = new List<LoanRepaymentSchedule>()
        };

        var schDec = new LoanRepaymentSchedule
        {
            Id = 50,
            EmployeeLoanId = 5,
            InstallmentNo = 1,
            PayPeriod = "2026-12",
            ScheduledAmount = 500m,
            PaidAmount = 0m,
            Status = "PENDING",
            EmployeeLoan = loan
        };
        loan.Schedules.Add(schDec);

        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schDec);

        var result = await _service.SkipInstallmentAsync(50, "Year end delay", "admin");

        Assert.Equal("SKIPPED", result.Status);
        Assert.Equal(2, loan.Schedules.Count);
        var appended = loan.Schedules.Last();
        Assert.Equal("2027-01", appended.PayPeriod);
        Assert.Equal(2, appended.InstallmentNo);
        Assert.Equal(500m, appended.ScheduledAmount);
    }

    [Fact]
    public async Task CancelLoanAsync_WhenApproved_CancelsLoanAndAllSchedules()
    {
        var loan = new EmployeeLoan
        {
            Id = 2,
            Status = "APPROVED",
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 21, Status = "PENDING" }
            }
        };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var cancelled = await _service.CancelLoanAsync(2, "Employee resigned", "admin");

        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal("CANCELLED", cancelled.Schedules.First().Status);
        Assert.Contains("Employee resigned", cancelled.Notes);
    }

    [Fact]
    public async Task CancelLoanAsync_WhenLoanNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeLoan?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CancelLoanAsync(999, "Valid reason", "admin"));
    }

    [Fact]
    public async Task CancelLoanAsync_WhenAlreadyCancelled_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan { Id = 1, Status = "CANCELLED" };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLoanAsync(1, "Already done", "admin"));
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenApproved_CancelsAdvanceSuccessfully()
    {
        var advance = new EmployeeAdvance { Id = 2, Status = "APPROVED", AdvanceAmount = 400m };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var cancelled = await _service.CancelAdvanceAsync(2, "HR cancellation", "admin");

        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Contains("HR cancellation", cancelled.Reason);
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenAdvanceNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeAdvance?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CancelAdvanceAsync(999, "Valid reason", "admin"));
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenAlreadyCancelled_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance { Id = 1, Status = "CANCELLED" };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelAdvanceAsync(1, "Cancel again", "admin"));
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenReservedByHandoff_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance { Id = 1, Status = "PENDING", ReservedByHandoffId = 1001 };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelAdvanceAsync(1, "Cancel attempt", "admin"));
        Assert.Contains("Cannot cancel an advance while it is reserved by a payroll handoff", ex.Message);
    }

    [Fact]
    public async Task SettleAdvanceEarlyAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.SettleAdvanceEarlyAsync(1, null!, "admin"));
    }

    [Fact]
    public async Task SettleAdvanceEarlyAsync_WhenAdvanceNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeAdvance?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.SettleAdvanceEarlyAsync(999, new SettleAdvanceEarlyDto(100m, "REF1", null, null), "admin"));
    }

    [Fact]
    public async Task SettleAdvanceEarlyAsync_WhenStatusNotActive_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance { Id = 1, Status = "PENDING", RemainingBalance = 500m };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SettleAdvanceEarlyAsync(1, new SettleAdvanceEarlyDto(100m, "REF1", null, null), "admin"));
        Assert.Contains("Only ACTIVE advances can be settled", ex.Message);
    }

    [Fact]
    public async Task SettleAdvanceEarlyAsync_WhenAmountExceedsRemainingBalance_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance { Id = 1, Status = "ACTIVE", RemainingBalance = 200m, EmployeeCode = "E1", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SettleAdvanceEarlyAsync(1, new SettleAdvanceEarlyDto(250m, "REF1", null, null), "admin"));
        Assert.Contains("cannot exceed remaining advance balance", ex.Message);
    }

    [Fact]
    public async Task SettleAdvanceEarlyAsync_PartialSettlement_LeavesStatusActiveAndReducesRemainingBalance()
    {
        var advance = new EmployeeAdvance
        {
            Id = 1,
            Status = "ACTIVE",
            AdvanceAmount = 500m,
            RemainingBalance = 500m,
            DeductedAmount = 0m,
            CurrencyCode = "USD"
        };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var settled = await _service.SettleAdvanceEarlyAsync(1, new SettleAdvanceEarlyDto(200m, "REC-01", "Direct cashier receipt", DateTime.UtcNow), "cashier");

        Assert.Equal("ACTIVE", settled.Status);
        Assert.Equal(300m, settled.RemainingBalance);
        Assert.Equal(200m, settled.DeductedAmount);
        Assert.Contains("EARLY SETTLEMENT: 200 USD", settled.Reason);
        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(advance, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLoanApplicationAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var dto = new CreateLoanApplicationDto("E1", "PERSONAL", 1200m, 12, new DateTime(2026, 1, 1), Notes: "Note", CurrencyCode: "JOD");

        await _service.CreateLoanApplicationAsync(dto, "HR", token);

        _loanRepoMock.Verify(r => r.AddLoanAsync(It.IsAny<EmployeeLoan>(), token), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task ApproveLoanAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var loan = new EmployeeLoan { Id = 1, Status = "PENDING", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, token)).ReturnsAsync(loan);

        await _service.ApproveLoanAsync(1, "HR", token);

        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, token), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task CreateAdvanceRequestAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var dto = new CreateAdvanceRequestDto("E1", 300m, "2026-11", Reason: "Advance note", CurrencyCode: "JOD");

        await _service.CreateAdvanceRequestAsync(dto, "HR", token);

        _loanRepoMock.Verify(r => r.AddAdvanceAsync(It.IsAny<EmployeeAdvance>(), token), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task ApproveAdvanceAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var advance = new EmployeeAdvance { Id = 1, Status = "PENDING", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, token)).ReturnsAsync(advance);

        await _service.ApproveAdvanceAsync(1, "HR", token);

        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(advance, token), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task GetLoansAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        _loanRepoMock.Setup(r => r.GetLoansAsync("E1", "ACTIVE", token)).ReturnsAsync(new List<EmployeeLoan>());

        await _service.GetLoansAsync("E1", "ACTIVE", token);

        _loanRepoMock.Verify(r => r.GetLoansAsync("E1", "ACTIVE", token), Times.Once);
    }

    [Fact]
    public async Task GetAdvancesAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-11", token)).ReturnsAsync(new List<EmployeeAdvance>());

        await _service.GetAdvancesAsync("E1", "2026-11", token);

        _loanRepoMock.Verify(r => r.GetAdvancesAsync("E1", "2026-11", token), Times.Once);
    }

    [Fact]
    public async Task SkipInstallmentAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var loan = new EmployeeLoan
        {
            Id = 1, Status = "ACTIVE", Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 5, EmployeeLoanId = 1, InstallmentNo = 1, PayPeriod = "2026-10", Status = "PENDING", ScheduledAmount = 100m }
            }
        };
        loan.Schedules[0].EmployeeLoan = loan;
        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(5, token)).ReturnsAsync(loan.Schedules[0]);

        await _service.SkipInstallmentAsync(5, "Medical leave", "HR", token);

        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, token), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task CancelLoanAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var loan = new EmployeeLoan { Id = 1, Status = "PENDING" };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, token)).ReturnsAsync(loan);

        await _service.CancelLoanAsync(1, "Employee request", "HR", token);

        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, token), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task CancelAdvanceAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var advance = new EmployeeAdvance { Id = 1, Status = "PENDING" };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, token)).ReturnsAsync(advance);

        await _service.CancelAdvanceAsync(1, "No longer needed", "HR", token);

        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(advance, token), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var loan = new EmployeeLoan { Id = 1, Status = "ACTIVE", RemainingBalance = 500m, EmployeeCode = "E1", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, token)).ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(200m, "REF-10", null);
        await _service.SettleLoanEarlyAsync(1, dto, "HR", token);

        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, token), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task SettleAdvanceEarlyAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var advance = new EmployeeAdvance { Id = 1, Status = "ACTIVE", RemainingBalance = 300m, EmployeeCode = "E1", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, token)).ReturnsAsync(advance);

        var dto = new SettleAdvanceEarlyDto(150m, "REF-20", null);
        await _service.SettleAdvanceEarlyAsync(1, dto, "HR", token);

        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(advance, token), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        _loanRepoMock.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-11", token))
            .ReturnsAsync(new List<LoanRepaymentSchedule>());
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-11", token))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", 1000m, null, false, token);

        Assert.Equal(0m, result.TotalScheduledDeduction);
        _loanRepoMock.Verify(r => r.GetPendingSchedulesForPeriodAsync("2026-11", token), Times.Once);
        _loanRepoMock.Verify(r => r.GetAdvancesAsync("E1", "2026-11", token), Times.Once);
    }

    [Fact]
    public async Task ApproveLoanAsync_WhenAlreadyApproved_ReturnsExistingLoanWithoutUpdatingOrSaving()
    {
        var loan = new EmployeeLoan { Id = 2, Status = "APPROVED", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        var result = await _service.ApproveLoanAsync(2, "HR");

        Assert.Same(loan, result);
        _loanRepoMock.Verify(r => r.UpdateLoanAsync(It.IsAny<EmployeeLoan>(), It.IsAny<CancellationToken>()), Times.Never);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAdvanceAsync_WhenAlreadyApproved_ReturnsExistingAdvanceWithoutUpdatingOrSaving()
    {
        var advance = new EmployeeAdvance { Id = 3, Status = "APPROVED", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(advance);

        var result = await _service.ApproveAdvanceAsync(3, "HR");

        Assert.Same(advance, result);
        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(It.IsAny<EmployeeAdvance>(), It.IsAny<CancellationToken>()), Times.Never);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelLoanAsync_WhenReasonWhitespace_ThrowsArgumentException(string reason)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CancelLoanAsync(1, reason, "HR"));
        Assert.Contains("reason is required to cancel a loan", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelAdvanceAsync_WhenReasonWhitespace_ThrowsArgumentException(string reason)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CancelAdvanceAsync(1, reason, "HR"));
        Assert.Contains("reason is required to cancel an advance", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SkipInstallmentAsync_WhenReasonWhitespace_ThrowsArgumentException(string reason)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.SkipInstallmentAsync(1, reason, "HR"));
        Assert.Contains("reason is required to skip/defer an installment", ex.Message);
    }

    [Fact]
    public async Task CreateLoanApplicationAsync_WhenZeroInstallments_RejectsScheduleWithoutPositiveInstallments()
    {
        EmployeeLoan? captured = null;
        _loanRepoMock.Setup(r => r.AddLoanAsync(It.IsAny<EmployeeLoan>(), It.IsAny<CancellationToken>()))
            .Callback<EmployeeLoan, CancellationToken>((l, _) => captured = l)
            .Returns(Task.CompletedTask);

        var dto = new CreateLoanApplicationDto("E1", "PERSONAL", 500m, 0, new DateTime(2026, 1, 1), Notes: "Zero installments", CurrencyCode: "  usd  ");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.CreateLoanApplicationAsync(dto, "HR"));
        Assert.Null(captured);
    }

    [Fact]
    public async Task CreateAdvanceRequestAsync_WhenCurrencyCodeWhitespace_RejectsRequest()
    {
        EmployeeAdvance? captured = null;
        _loanRepoMock.Setup(r => r.AddAdvanceAsync(It.IsAny<EmployeeAdvance>(), It.IsAny<CancellationToken>()))
            .Callback<EmployeeAdvance, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var dto = new CreateAdvanceRequestDto("E1", 250m, "2026-11", Reason: "Advance note", CurrencyCode: "   ");
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAdvanceRequestAsync(dto, "HR"));
        Assert.Null(captured);
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SettleLoanEarlyAsync(1, null!, "HR"));
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_WhenLoanNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeLoan?)null);

        var dto = new SettleLoanEarlyDto(100m, "REF", null, null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.SettleLoanEarlyAsync(999, dto, "HR"));
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_WhenStatusNotActive_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan { Id = 1, Status = "PENDING", RemainingBalance = 500m };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(100m, "REF", null, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SettleLoanEarlyAsync(1, dto, "HR"));
        Assert.Contains("Only ACTIVE loans can be settled", ex.Message);
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenParentLoanStatusNotActive_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan { Id = 1, Status = "PENDING" };
        var schedule = new LoanRepaymentSchedule { Id = 10, EmployeeLoanId = 1, Status = "PENDING", EmployeeLoan = loan };
        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(schedule);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SkipInstallmentAsync(10, "Reason", "HR"));
        Assert.Contains("Only ACTIVE loans can have installments skipped", ex.Message);
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_WhenAmountExceedsRemainingBalance_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan { Id = 1, Status = "ACTIVE", RemainingBalance = 400m, EmployeeCode = "E1", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(500m, "REF", null, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SettleLoanEarlyAsync(1, dto, "HR"));
        Assert.Contains("cannot exceed remaining loan balance", ex.Message);
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_FullSettlement_SetsStatusSettled()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            Status = "ACTIVE",
            PrincipalAmount = 600m,
            TotalPaidAmount = 200m,
            RemainingBalance = 400m,
            CurrencyCode = "JOD",
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 1, InstallmentNo = 1, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" },
                new() { Id = 2, InstallmentNo = 2, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" }
            }
        };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(400m, "FULL-REC", "Early full payoff", new DateTime(2026, 11, 10));
        var settled = await _service.SettleLoanEarlyAsync(1, dto, "HR");

        Assert.Equal("SETTLED", settled.Status);
        Assert.Equal(0m, settled.RemainingBalance);
        Assert.Equal(600m, settled.TotalPaidAmount);
        Assert.All(settled.Schedules, s => Assert.Equal("PAID", s.Status));
        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SettleLoanEarlyAsync_PartialSettlementAcrossMultipleSchedules_UpdatesStatusAndAmounts()
    {
        var loan = new EmployeeLoan
        {
            Id = 1,
            Status = "ACTIVE",
            PrincipalAmount = 600m,
            TotalPaidAmount = 0m,
            RemainingBalance = 600m,
            CurrencyCode = "USD",
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 1, InstallmentNo = 1, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" },
                new() { Id = 2, InstallmentNo = 2, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" },
                new() { Id = 3, InstallmentNo = 3, ScheduledAmount = 200m, PaidAmount = 0m, Status = "PENDING" }
            }
        };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        var dto = new SettleLoanEarlyDto(350m, "PART-REC", null, null);
        var settled = await _service.SettleLoanEarlyAsync(1, dto, "HR");

        Assert.Equal("ACTIVE", settled.Status);
        Assert.Equal(250m, settled.RemainingBalance);
        Assert.Equal(350m, settled.TotalPaidAmount);
        Assert.Equal("PAID", settled.Schedules[0].Status);
        Assert.Equal(200m, settled.Schedules[0].PaidAmount);
        Assert.Equal("PARTIAL", settled.Schedules[1].Status);
        Assert.Equal(150m, settled.Schedules[1].PaidAmount);
        Assert.Equal("PENDING", settled.Schedules[2].Status);
        Assert.Equal(0m, settled.Schedules[2].PaidAmount);
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenScheduleStatusNotPending_ThrowsInvalidOperationException()
    {
        var schedule = new LoanRepaymentSchedule { Id = 10, EmployeeLoanId = 1, Status = "PAID" };
        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(schedule);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SkipInstallmentAsync(10, "Reason", "HR"));
        Assert.Contains("Only PENDING installments can be skipped", ex.Message);
    }

    [Fact]
    public async Task CancelLoanAsync_WhenStatusActive_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan { Id = 1, Status = "ACTIVE", TotalPaidAmount = 0m };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelLoanAsync(1, "Reason", "HR"));
        Assert.Contains("Active or funded loans must be reversed via Accounting", ex.Message);
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenHasRecordedDeductions_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance { Id = 1, Status = "PENDING", DeductedAmount = 50m };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(advance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelAdvanceAsync(1, "Reason", "HR"));
        Assert.Contains("Cannot cancel an advance that has recorded deductions", ex.Message);
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenStatusActive_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance { Id = 1, Status = "ACTIVE", DeductedAmount = 0m };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(advance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelAdvanceAsync(1, "Reason", "HR"));
        Assert.Contains("Active or funded advances must be reversed via Accounting", ex.Message);
    }
}

