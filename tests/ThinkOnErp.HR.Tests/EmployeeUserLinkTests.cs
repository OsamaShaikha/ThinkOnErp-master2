using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class EmployeeUserLinkTests
{
    private readonly Mock<IEmployeeRepository> _repository = new();
    private readonly Employee _employee = new() { EmployeeCode = "EMP1", BranchId = 7 };
    private EmployeeService Service => new(_repository.Object);

    public EmployeeUserLinkTests()
    {
        _repository.Setup(r => r.GetEmployeeByCodeAsync("EMP1", It.IsAny<CancellationToken>())).ReturnsAsync(_employee);
        _repository.Setup(r => r.IsUserAvailableForEmployeeAsync(12, 7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task Link_PersistsAccountAndAuditFields()
    {
        var result = await Service.LinkEmployeeUserAsync("EMP1", 12, "admin");
        Assert.Equal(12L, result.UserId);
        Assert.Equal("admin", _employee.UpdateUser);
        Assert.NotNull(_employee.UpdateDate);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Link_RejectsAccountAlreadyAssignedToAnotherEmployee()
    {
        _repository.Setup(r => r.GetEmployeeByUserIdAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "EMP2", UserId = 12 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.LinkEmployeeUserAsync("EMP1", 12, "admin"));
        Assert.Null(_employee.UserId);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Link_AllowsSameAccountForSameEmployee()
    {
        _employee.UserId = 12;
        _repository.Setup(r => r.GetEmployeeByUserIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(_employee);
        Assert.Equal(12L, (await Service.LinkEmployeeUserAsync("EMP1", 12, "admin")).UserId);
    }

    [Fact]
    public async Task Link_RejectsMissingInactiveOrWrongBranchUser()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.LinkEmployeeUserAsync("EMP1", 99, "admin"));
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Link_RejectsInvalidUserId(long userId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.LinkEmployeeUserAsync("EMP1", userId, "admin"));
    }

    [Theory]
    [InlineData("TERMINATED", true)]
    [InlineData("SUSPENDED", true)]
    [InlineData("ACTIVE", false)]
    public async Task Link_RejectsInactiveEmployee(string status, bool active)
    {
        _employee.EmploymentStatus = status;
        _employee.IsActive = active;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.LinkEmployeeUserAsync("EMP1", 12, "admin"));
    }

    [Fact]
    public async Task Unlink_PreservesEmployeeAndWorksForTerminatedEmployee()
    {
        _employee.UserId = 12;
        _employee.EmploymentStatus = "TERMINATED";
        Assert.Null((await Service.LinkEmployeeUserAsync("EMP1", null, "admin")).UserId);
        Assert.Equal("EMP1", _employee.EmployeeCode);
        _repository.Verify(r => r.DeleteEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Link_RejectsMissingEmployee()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.LinkEmployeeUserAsync("missing", 12, "admin"));
    }

    [Fact]
    public async Task Lookup_ReturnsOnlyEmployeeLinkedToRequestedUser()
    {
        _employee.UserId = 12;
        _repository.Setup(r => r.GetEmployeeByUserIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(_employee);
        Assert.Equal("EMP1", (await Service.GetEmployeeUserLinkAsync(12))?.EmployeeCode);
        Assert.Null(await Service.GetEmployeeUserLinkAsync(99));
    }
}
