using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;
namespace ThinkOnErp.HR.Tests;
public class EmployeeProfilePayrollLockTests
{
    private readonly Mock<IEmployeeRepository> repository = new();
    private readonly Mock<IHrUnitOfWork> unit = new();
    private readonly Employee employee = new() { EmployeeCode = "E1", HireDate = new(2020, 1, 1), BranchId = 7, NationalId = "N1" };
    private readonly EmployeeDependent dependent = new() { Id = 5, EmployeeCode = "E1" };
    public EmployeeProfilePayrollLockTests()
    {
        repository.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        repository.Setup(r => r.GetDependentByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(dependent);
    }
    private static UpdateEmployeeDto Profile(DateTime? hire = null) => new("Local", "Employee", "N1", "X", null, new(1990, 1, 1), "M", "SINGLE", null, null,
        hire ?? new DateTime(2020, 1, 1), null, null, null, "FULL_TIME", "ACTIVE", null, null, 7, null, null, 0, false, null, null, null, true);
    private static CreateDependentDto Dependent() => new("Child", "Child", "CHILD", new(2010, 1, 1), "M", null, true, false);
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task RejectedPayrollGuardPrecedesEveryProfileMutation(int operation)
    {
        unit.Setup(u => u.ExecuteEmployeeProfileAsync("E1", employee.HireDate, It.IsAny<Func<Task<EmployeeDetailsDto>>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Closed payroll."));
        unit.Setup(u => u.ExecuteEmployeeProfileAsync("E1", employee.HireDate, It.IsAny<Func<Task<DependentDto>>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Closed payroll."));
        unit.Setup(u => u.ExecuteEmployeeProfileAsync("E1", employee.HireDate, It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Closed payroll."));
        var service = new EmployeeService(repository.Object, unit.Object);
        Func<Task> act = operation switch { 0 => () => service.UpdateEmployeeAsync("E1", Profile() with { IsActive = false }, "HR"),
            1 => () => service.DeleteEmployeeAsync("E1", "HR"), 2 => () => service.AddDependentAsync("E1", Dependent(), "HR"), _ => () => service.DeleteDependentAsync(5, "HR") };
        await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.True(employee.IsActive); Assert.Null(employee.TerminationDate);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.DeleteDependentAsync(It.IsAny<EmployeeDependent>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.AddDependentAsync(It.IsAny<EmployeeDependent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData(2015, 2015)] [InlineData(2025, 2020)]
    public async Task HireChangeProtectsEarlierOfOldAndNewHistory(int requestedYear, int expectedYear)
    {
        var from = new DateTime(expectedYear, 1, 1);
        unit.Setup(u => u.ExecuteEmployeeProfileAsync("E1", from, It.IsAny<Func<Task<EmployeeDetailsDto>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (string code, DateTime date, Func<Task<EmployeeDetailsDto>> action, CancellationToken ct) =>
            { Assert.Equal(2020, employee.HireDate.Year); return await action(); });
        var result = await new EmployeeService(repository.Object, unit.Object).UpdateEmployeeAsync("E1", Profile(new(requestedYear, 1, 1)), "HR");
        Assert.Equal(requestedYear, result.HireDate.Year);
        unit.Verify(u => u.ExecuteEmployeeProfileAsync("E1", from, It.IsAny<Func<Task<EmployeeDetailsDto>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact] public async Task MissingUnitOfWorkCannotSilentlyBypassProtection()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => new EmployeeService(repository.Object).UpdateEmployeeAsync("E1", Profile(), "HR"));
    [Fact] public async Task DeletingDependentRechecksOwnerAfterLock()
    {
        repository.SetupSequence(r => r.GetDependentByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(dependent).ReturnsAsync(new EmployeeDependent { Id = 5, EmployeeCode = "OTHER" });
        unit.Setup(u => u.ExecuteEmployeeProfileAsync("E1", employee.HireDate, It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((string code, DateTime date, Func<Task<bool>> action, CancellationToken ct) => action());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EmployeeService(repository.Object, unit.Object).DeleteDependentAsync(5, "HR"));
        repository.Verify(r => r.DeleteDependentAsync(It.IsAny<EmployeeDependent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact] public async Task ReversedEmploymentDatesAreRejectedBeforeLocks()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new EmployeeService(repository.Object, unit.Object).UpdateEmployeeAsync("E1", Profile() with { TerminationDate = new DateTime(2019, 1, 1) }, "HR"));
        unit.VerifyNoOtherCalls();
    }
    private static CreateEmployeeDto NewEmployee() => new(" new ", "Local", "Employee", "NEW_ID", "X", null, new(1990, 1, 1), "M", "SINGLE", null, null,
        new(2020, 1, 1), null, "FULL_TIME", "ACTIVE", null, null, 7, null, null, 0, false, null, null, null);
    [Fact] public async Task NewEmployeeGuardPrecedesCreationAndNormalizesItsIdentity()
    {
        unit.Setup(u => u.ExecuteNewEmployeeAsync(It.Is<EmployeeImportRowDto>(r => r.EmployeeCode == "NEW" && r.BranchId == 7 && r.HireDate == new DateTime(2020, 1, 1)),
            It.IsAny<Func<Task<EmployeeDetailsDto>>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Closed payroll."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EmployeeService(repository.Object, unit.Object).CreateEmployeeAsync(NewEmployee(), "HR"));
        repository.Verify(r => r.AddEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.ExistsByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact] public async Task NewEmployeeIsCreatedOnlyInsideAllowedInputTransaction()
    {
        unit.Setup(u => u.ExecuteNewEmployeeAsync(It.IsAny<EmployeeImportRowDto>(), It.IsAny<Func<Task<EmployeeDetailsDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((EmployeeImportRowDto row, Func<Task<EmployeeDetailsDto>> action, CancellationToken ct) => action());
        var result = await new EmployeeService(repository.Object, unit.Object).CreateEmployeeAsync(NewEmployee(), "HR");
        Assert.Equal("NEW", result.EmployeeCode);
        repository.Verify(r => r.AddEmployeeAsync(It.Is<Employee>(e => e.EmployeeCode == "NEW" && e.CreationUser == "HR"), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

}
