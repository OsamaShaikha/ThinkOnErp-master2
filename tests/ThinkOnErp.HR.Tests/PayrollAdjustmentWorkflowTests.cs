using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollAdjustmentWorkflowTests
{
    [Fact]
    public async Task ApprovalReloadsAdjustmentAfterAcquiringPeriodLock()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var locked = false;
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PayrollAdjustment
            {
                Id = 1, CompanyId = 7, EmployeeCode = "E1", PayPeriod = "2026-11",
                Status = locked ? "PROCESSED" : "PENDING"
            });
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(7, "2026-11", It.IsAny<Func<Task<PayrollAdjustment>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (long companyId, string period, Func<Task<PayrollAdjustment>> action, CancellationToken token) =>
            {
                locked = true;
                return await action();
            });
        var service = new PayrollAdjustmentService(repository.Object, unitOfWork.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAdjustmentAsync(1, "HR"));
        Assert.True(locked);
        repository.Verify(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>()), Times.Exactly(2));
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NewAdjustmentRequiresExplicitApproval()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var service = new PayrollAdjustmentService(repository.Object);
        var adjustment = await service.CreateAdjustmentAsync(new CreatePayrollAdjustmentDto(
            1, "E1", "2026-11", "BONUS", "EARNING", 100m, null), "HR");
        Assert.Equal("PENDING", adjustment.Status);
    }

    [Fact]
    public async Task CalculatedPayrollPreventsUnpostingEvenWithoutAnAdjustmentLink()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, EmployeeCode = "E1", PayPeriod = "2026-11", Status = "APPROVED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        repository.Setup(r => r.HasCalculatedPayrollAsync("E1", "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PayrollAdjustmentService(repository.Object).UnpostAdjustmentAsync(1, "HR"));
        Assert.Equal("APPROVED", adjustment.Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApprovedAdjustmentMustBeUnpostedBeforeDeletion()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "APPROVED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAdjustmentAsync(1));
        repository.Verify(r => r.DeleteAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
        await service.UnpostAdjustmentAsync(1, "HR");
        Assert.Equal("PENDING", adjustment.Status);
        await service.DeleteAdjustmentAsync(1);
        repository.Verify(r => r.DeleteAdjustmentAsync(adjustment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumedAdjustmentCannotBeApprovedAgain()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "PROCESSED", PayrollRunLineId = 12 };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PayrollAdjustmentService(repository.Object).ApproveAdjustmentAsync(1, "HR"));
        Assert.Equal("PROCESSED", adjustment.Status);
    }

    [Fact]
    public async Task CreateAdjustment_WhenDtoNull_ThrowsArgumentNullException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var service = new PayrollAdjustmentService(repository.Object);
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAdjustmentAsync(null!, "USER"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task CreateAdjustment_WhenAmountZeroOrNegative_ThrowsArgumentException(decimal amount)
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(1, "E1", "2026-11", "BONUS", "EARNING", amount, null);
        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(() => service.CreateAdjustmentAsync(dto, "USER"));
        Assert.Contains("Adjustment amount must be positive", ex.Message);
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("ALLOWANCE")]
    [InlineData("")]
    public async Task CreateAdjustment_WhenAdjustmentTypeInvalid_ThrowsArgumentException(string adjType)
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(1, "E1", "2026-11", "BONUS", adjType, 100m, null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAdjustmentAsync(dto, "USER"));
        Assert.Contains("Adjustment type must be EARNING or DEDUCTION", ex.Message);
    }

    [Theory]
    [InlineData(null, "2026-11", "BONUS")]
    [InlineData("E1", null, "BONUS")]
    [InlineData("E1", "2026-11", null)]
    public async Task CreateAdjustment_WhenRequiredFieldMissing_ThrowsArgumentException(string? empCode, string? payPeriod, string? componentCode)
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(1, empCode!, payPeriod!, componentCode!, "EARNING", 100m, null);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAdjustmentAsync(dto, "USER"));
    }

    [Fact]
    public async Task ApproveAdjustment_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        repository.Setup(r => r.GetAdjustmentByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollAdjustment?)null);
        var service = new PayrollAdjustmentService(repository.Object);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ApproveAdjustmentAsync(999, "HR_USER"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task ApproveAdjustment_HappyPath_SetsApprovedStatusAndUser()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment
        {
            Id = 5,
            CompanyId = 1,
            EmployeeCode = "E1",
            PayPeriod = "2026-11",
            Status = "PENDING"
        };
        repository.Setup(r => r.GetAdjustmentByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adjustment);

        var service = new PayrollAdjustmentService(repository.Object);
        var approved = await service.ApproveAdjustmentAsync(5, "APPROVER_HR");

        Assert.Equal("APPROVED", approved.Status);
        Assert.Equal("APPROVER_HR", approved.UpdateUser);
        Assert.NotNull(approved.UpdateDate);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, "APPROVED")]
    [InlineData(false, "REJECTED")]
    public async Task DecideAdjustment_RequiresAndPersistsReasonAndActor(bool approve, string expectedStatus)
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 501, CompanyId = 3, EmployeeCode = "E1", PayPeriod = "2026-11", Status = "PENDING" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(501, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);

        var result = await new PayrollAdjustmentService(repository.Object).DecideAdjustmentAsync(501, approve, "  Reviewed against attendance  ", "HR_APPROVER");

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("Reviewed against attendance", result.DecisionReason);
        Assert.Equal("HR_APPROVER", result.UpdateUser);
        Assert.NotNull(result.UpdateDate);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DecideAdjustment_RejectsMissingReason(string? reason)
    {
        var service = new PayrollAdjustmentService(new Mock<IPayrollAdjustmentRepository>().Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.DecideAdjustmentAsync(1, true, reason!, "HR"));
    }

    [Fact]
    public async Task RejectAdjustment_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        repository.Setup(r => r.GetAdjustmentByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollAdjustment?)null);
        var service = new PayrollAdjustmentService(repository.Object);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RejectAdjustmentAsync(999, "HR_USER"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task RejectAdjustment_WhenNotPending_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 6, Status = "APPROVED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAdjustmentAsync(6, "HR_USER"));
        Assert.Contains("Only a pending adjustment can be rejected", ex.Message);
    }

    [Fact]
    public async Task RejectAdjustment_HappyPath_SetsRejectedStatusAndUser()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment
        {
            Id = 7,
            CompanyId = 1,
            EmployeeCode = "E1",
            PayPeriod = "2026-11",
            Status = "PENDING"
        };
        repository.Setup(r => r.GetAdjustmentByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adjustment);

        var service = new PayrollAdjustmentService(repository.Object);
        var rejected = await service.RejectAdjustmentAsync(7, "REJECTER_HR");

        Assert.Equal("REJECTED", rejected.Status);
        Assert.Equal("REJECTER_HR", rejected.UpdateUser);
        Assert.NotNull(rejected.UpdateDate);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnpostAdjustment_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        repository.Setup(r => r.GetAdjustmentByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollAdjustment?)null);
        var service = new PayrollAdjustmentService(repository.Object);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UnpostAdjustmentAsync(999, "HR_USER"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task UnpostAdjustment_WhenStatusNotApproved_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 8, Status = "PENDING" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnpostAdjustmentAsync(8, "HR_USER"));
        Assert.Contains("Only an approved adjustment can be unposted", ex.Message);
    }

    [Fact]
    public async Task DeleteAdjustment_WhenStatusIsApproved_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 9, Status = "APPROVED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAdjustmentAsync(9));
        Assert.Contains("Only pending or rejected adjustments can be deleted", ex.Message);
    }

    [Fact]
    public async Task DeleteAdjustment_WhenStatusIsRejected_Succeeds()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 10, Status = "REJECTED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);
        await service.DeleteAdjustmentAsync(10);
        repository.Verify(r => r.DeleteAdjustmentAsync(adjustment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAdjustmentsAsync_ReturnsMappedDtoList()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustments = new List<PayrollAdjustment>
        {
            new() { Id = 1, CompanyId = 1, EmployeeCode = "E1", PayPeriod = "2026-11", ComponentCode = "BONUS", AdjustmentType = "EARNING", Amount = 150m, Status = "APPROVED" },
            new() { Id = 2, CompanyId = 1, EmployeeCode = "E2", PayPeriod = "2026-11", ComponentCode = "DEDUCT", AdjustmentType = "DEDUCTION", Amount = 50m, Status = "PENDING" }
        };
        repository.Setup(r => r.GetAdjustmentsAsync(1, null, "2026-11", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adjustments);

        var service = new PayrollAdjustmentService(repository.Object);
        var results = await service.GetAdjustmentsAsync(1, null, "2026-11", null);

        Assert.Equal(2, results.Count);
        Assert.Equal("BONUS", results[0].ComponentCode);
        Assert.Equal(150m, results[0].Amount);
        Assert.Equal("DEDUCT", results[1].ComponentCode);
        Assert.Equal(50m, results[1].Amount);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(1, "E1", "2026-11", "BONUS", "EARNING", 100m, null);

        await service.CreateAdjustmentAsync(dto, "USER", token);

        repository.Verify(r => r.AddAdjustmentAsync(It.IsAny<PayrollAdjustment>(), token), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var adjustment = new PayrollAdjustment { Id = 1, CompanyId = 1, EmployeeCode = "E1", PayPeriod = "2026-11", Status = "PENDING" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, token)).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        await service.ApproveAdjustmentAsync(1, "HR", token);

        repository.Verify(r => r.UpdateAdjustmentAsync(adjustment, token), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task RejectAdjustmentAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var adjustment = new PayrollAdjustment { Id = 1, CompanyId = 1, EmployeeCode = "E1", PayPeriod = "2026-11", Status = "PENDING" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, token)).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        await service.RejectAdjustmentAsync(1, "HR", token);

        repository.Verify(r => r.UpdateAdjustmentAsync(adjustment, token), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task UnpostAdjustmentAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var adjustment = new PayrollAdjustment { Id = 1, CompanyId = 1, EmployeeCode = "E1", PayPeriod = "2026-11", Status = "APPROVED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, token)).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        await service.UnpostAdjustmentAsync(1, "HR", token);

        repository.Verify(r => r.UpdateAdjustmentAsync(adjustment, token), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task GetAdjustmentsAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        repository.Setup(r => r.GetAdjustmentsAsync(1, "E1", "2026-11", "PENDING", token))
            .ReturnsAsync(new List<PayrollAdjustment>());
        var service = new PayrollAdjustmentService(repository.Object);

        await service.GetAdjustmentsAsync(1, "E1", "2026-11", "PENDING", token);

        repository.Verify(r => r.GetAdjustmentsAsync(1, "E1", "2026-11", "PENDING", token), Times.Once);
    }

    [Fact]
    public async Task GetAdjustmentByIdAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var service = new PayrollAdjustmentService(repository.Object);

        await service.GetAdjustmentByIdAsync(1, token);

        repository.Verify(r => r.GetAdjustmentByIdAsync(1, token), Times.Once);
    }

    [Fact]
    public async Task DeleteAdjustmentAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var adjustment = new PayrollAdjustment { Id = 1, CompanyId = 1, EmployeeCode = "E1", PayPeriod = "2026-11", Status = "PENDING" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, token)).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        await service.DeleteAdjustmentAsync(1, token);

        repository.Verify(r => r.DeleteAdjustmentAsync(adjustment, token), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(token), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAdjustment_WhenCompanyIdZeroOrNegative_ThrowsArgumentException(long companyId)
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(companyId, "E1", "2026-11", "BONUS", "EARNING", 100m, null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAdjustmentAsync(dto, "USER"));
        Assert.Contains("Company, employee, payroll period and salary component are required", ex.Message);
    }

    [Theory]
    [InlineData("   ", "2026-11", "BONUS")]
    [InlineData("E1", "   ", "BONUS")]
    [InlineData("E1", "2026-11", "   ")]
    public async Task CreateAdjustment_WhenRequiredFieldWhitespace_ThrowsArgumentException(string empCode, string payPeriod, string compCode)
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(1, empCode, payPeriod, compCode, "EARNING", 100m, null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAdjustmentAsync(dto, "USER"));
        Assert.Contains("Company, employee, payroll period and salary component are required", ex.Message);
    }

    [Fact]
    public async Task CreateAdjustment_NormalizesAdjustmentTypeCase()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        PayrollAdjustment? captured = null;
        repository.Setup(r => r.AddAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollAdjustment, CancellationToken>((adj, _) => captured = adj)
            .Returns(Task.CompletedTask);

        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(1, "E1", "2026-11", "BONUS", " earning  ", 100m, "A note");
        await service.CreateAdjustmentAsync(dto, "USER");

        Assert.NotNull(captured);
        Assert.Equal("EARNING", captured.AdjustmentType);
        Assert.Equal("A note", captured.Description);
        Assert.Equal("USER", captured.CreationUser);
    }

    [Fact]
    public async Task ApproveAdjustment_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "APPROVED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAdjustmentAsync(1, "HR"));
        Assert.Contains("Only a pending adjustment can be approved", ex.Message);
    }

    [Fact]
    public async Task ApproveAdjustment_WhenRejected_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "REJECTED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAdjustmentAsync(1, "HR"));
        Assert.Contains("Only a pending adjustment can be approved", ex.Message);
    }

    [Fact]
    public async Task RejectAdjustment_WhenAlreadyRejected_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "REJECTED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAdjustmentAsync(1, "HR"));
        Assert.Contains("Only a pending adjustment can be rejected", ex.Message);
    }

    [Fact]
    public async Task RejectAdjustment_WhenProcessed_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "PROCESSED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAdjustmentAsync(1, "HR"));
        Assert.Contains("This adjustment has been consumed by payroll", ex.Message);
    }

    [Fact]
    public async Task UnpostAdjustment_WhenPending_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "PENDING" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnpostAdjustmentAsync(1, "HR"));
        Assert.Contains("Only an approved adjustment can be unposted", ex.Message);
    }

    [Fact]
    public async Task UnpostAdjustment_WhenRejected_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "REJECTED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnpostAdjustmentAsync(1, "HR"));
        Assert.Contains("Only an approved adjustment can be unposted", ex.Message);
    }

    [Fact]
    public async Task UnpostAdjustment_WhenProcessed_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "PROCESSED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnpostAdjustmentAsync(1, "HR"));
        Assert.Contains("This adjustment has been consumed by payroll", ex.Message);
    }

    [Fact]
    public async Task DeleteAdjustment_WhenNotFound_ReturnsQuietlyWithoutCallingDelete()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        repository.Setup(r => r.GetAdjustmentByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollAdjustment?)null);
        var service = new PayrollAdjustmentService(repository.Object);

        await service.DeleteAdjustmentAsync(999);

        repository.Verify(r => r.DeleteAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAdjustment_WhenProcessed_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, Status = "PROCESSED" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        var service = new PayrollAdjustmentService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAdjustmentAsync(1));
        Assert.Contains("This adjustment has been consumed by payroll", ex.Message);
    }

    [Fact]
    public async Task DeleteAdjustment_WhenHasCalculatedPayroll_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustment = new PayrollAdjustment { Id = 1, EmployeeCode = "E1", PayPeriod = "2026-11", Status = "PENDING" };
        repository.Setup(r => r.GetAdjustmentByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(adjustment);
        repository.Setup(r => r.HasCalculatedPayrollAsync("E1", "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var service = new PayrollAdjustmentService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAdjustmentAsync(1));
        Assert.Contains("Payroll is already calculated for this employee and period", ex.Message);
    }

    [Fact]
    public async Task GetAdjustmentsAsync_WhenEmployeeAndComponentNull_ReturnsFallbackCodes()
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var adjustments = new List<PayrollAdjustment>
        {
            new() { Id = 1, CompanyId = 1, EmployeeCode = "E99", PayPeriod = "2026-11", ComponentCode = "RAW_CODE", AdjustmentType = "EARNING", Amount = 120m, Status = "PENDING", Employee = null, Component = null }
        };
        repository.Setup(r => r.GetAdjustmentsAsync(1, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adjustments);

        var service = new PayrollAdjustmentService(repository.Object);
        var dtos = await service.GetAdjustmentsAsync(1, null, null, null);

        var dto = Assert.Single(dtos);
        Assert.Equal("E99", dto.EmployeeName);
        Assert.Equal("RAW_CODE", dto.ComponentName);
    }

    [Fact]
    public async Task CreateAdjustment_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var repository = new Mock<IPayrollAdjustmentRepository>();
        repository.Setup(r => r.AddAdjustmentAsync(It.IsAny<PayrollAdjustment>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(1, "E1", "2026-11", "BONUS", "EARNING", 100m, "Test bonus");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateAdjustmentAsync(dto, "HR_USER", cts.Token));
    }

    [Theory]
    [InlineData("  earning  ", "EARNING")]
    [InlineData("  deduction  ", "DEDUCTION")]
    public async Task CreateAdjustment_TrimsAndUpperCasesAdjustmentType(string inputType, string expectedType)
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        PayrollAdjustment? saved = null;
        repository.Setup(r => r.AddAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollAdjustment, CancellationToken>((adj, _) => saved = adj)
            .Returns(Task.CompletedTask);

        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(1, "E1", "2026-11", "BONUS", inputType, 100m, null);
        var result = await service.CreateAdjustmentAsync(dto, "HR_USER");

        Assert.Equal(expectedType, result.AdjustmentType);
        Assert.NotNull(saved);
        Assert.Equal(expectedType, saved!.AdjustmentType);
    }

    [Theory]
    [InlineData("BONUS")]
    [InlineData("ALLOWANCE")]
    [InlineData("OTHER")]
    [InlineData("REDUCTION")]
    public async Task CreateAdjustment_ThrowsArgumentException_WhenAdjustmentTypeInvalid(string invalidType)
    {
        var repository = new Mock<IPayrollAdjustmentRepository>();
        var service = new PayrollAdjustmentService(repository.Object);
        var dto = new CreatePayrollAdjustmentDto(1, "E1", "2026-11", "BONUS", invalidType, 100m, null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAdjustmentAsync(dto, "HR_USER"));
        Assert.Contains("Adjustment type must be EARNING or DEDUCTION", ex.Message);
    }
}
