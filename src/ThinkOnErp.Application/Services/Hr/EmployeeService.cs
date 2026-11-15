using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.AddDependent;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.AssignSalaryStructure;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.CreateEmployee;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.CreateSalaryComponent;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.DeleteDependent;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.DeleteEmployee;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.LinkEmployeeUser;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployee;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployeeBank;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployeeIdentity;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployeeWork;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetActiveSalaryStructure;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetEmployeeByCode;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetEmployees;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetMyEmployeeLink;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetMyTeam;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetSalaryComponents;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.Employees.")]
public interface IEmployeeService
{
    Task<EmployeeDetailsDto> UpdateEmployeeBankAsync(string employeeCode, UpdateEmployeeBankDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeDetailsDto> UpdateEmployeeIdentityAsync(string employeeCode, UpdateEmployeeIdentityDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeDetailsDto> UpdateEmployeeWorkAsync(string employeeCode, UpdateEmployeeWorkDto dto, string user, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<EmployeeTeamMemberDto> Items, int TotalCount)> GetDirectReportsAsync(long userId, bool activeOnly, string? search, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<EmployeeSummaryDto> Items, int TotalCount)> GetEmployeesPagedAsync(
        string? searchKeyword = null, string? departmentCode = null, string? status = null, long? branchId = null,
        int pageIndex = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<EmployeeDetailsDto?> GetEmployeeByCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<EmployeeUserLinkDto?> GetEmployeeUserLinkAsync(long userId, CancellationToken cancellationToken = default);
    Task<EmployeeUserLinkDto> LinkEmployeeUserAsync(string employeeCode, long? userId, string user, CancellationToken cancellationToken = default);
    Task<EmployeeDetailsDto> CreateEmployeeAsync(CreateEmployeeDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeDetailsDto> UpdateEmployeeAsync(string employeeCode, UpdateEmployeeDto dto, string user, CancellationToken cancellationToken = default);
    Task DeleteEmployeeAsync(string employeeCode, string user, CancellationToken cancellationToken = default);
    Task<DependentDto> AddDependentAsync(string employeeCode, CreateDependentDto dto, string user, CancellationToken cancellationToken = default);
    Task DeleteDependentAsync(long dependentId, string user, CancellationToken cancellationToken = default);
    Task<SalaryStructureDetailsDto> AssignSalaryStructureAsync(string employeeCode, AssignSalaryStructureDto dto, string user, CancellationToken cancellationToken = default);
    Task<SalaryStructureDetailsDto?> GetActiveSalaryStructureAsync(string employeeCode, DateTime? asOfDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalaryComponentDto>> GetSalaryComponentsAsync(CancellationToken cancellationToken = default);
    Task<SalaryComponentDto> CreateSalaryComponentAsync(CreateSalaryComponentDto dto, string user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Transitional facade. All logic lives in the Features/Hr/Employees handlers; this class only
/// constructs them, so existing unit tests and HrSchemaTool verification scripts keep exercising
/// the real rules. It is no longer registered in DI. Delete it once those callers use the handlers.
/// </summary>
/// <remarks>
/// Constructor contract kept from the old service: a null <c>fields</c> means a trusted internal
/// caller (no field checks); a null <c>unitOfWork</c> refuses protected writes, except trusted
/// salary assignment, which historically ran without a lock.
/// </remarks>
[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.Employees. Kept only for existing tests and HrSchemaTool.")]
public sealed class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _repository;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly EmployeeFieldGuard _fields;

    public EmployeeService(IEmployeeRepository employeeRepo, IHrUnitOfWork? unitOfWork = null, EmployeeFieldAccess? fields = null)
    {
        _repository = employeeRepo;
        _unitOfWork = unitOfWork;
        _fields = EmployeeFieldGuard.From(fields);
    }

    private IHrUnitOfWork Unit => _unitOfWork ?? new UnavailableHrUnitOfWork();

    public async Task<(IReadOnlyList<EmployeeTeamMemberDto> Items, int TotalCount)> GetDirectReportsAsync(
        long userId, bool activeOnly, string? search, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var page = Data(await new GetMyTeamQueryHandler(_repository)
            .Handle(new GetMyTeamQuery(userId, activeOnly, search, pageIndex, pageSize), cancellationToken));
        return (page.Items, (int)page.TotalCount);
    }

    public async Task<(IReadOnlyList<EmployeeSummaryDto> Items, int TotalCount)> GetEmployeesPagedAsync(
        string? searchKeyword = null, string? departmentCode = null, string? status = null, long? branchId = null,
        int pageIndex = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var page = Data(await new GetEmployeesQueryHandler(_repository, _fields)
            .Handle(new GetEmployeesQuery(searchKeyword, departmentCode, status, branchId, pageIndex, pageSize), cancellationToken));
        return (page.Items, (int)page.TotalCount);
    }

    public async Task<EmployeeDetailsDto?> GetEmployeeByCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
        => DataOrNull(await new GetEmployeeByCodeQueryHandler(_repository, _fields).Handle(new GetEmployeeByCodeQuery(employeeCode), cancellationToken));

    public async Task<EmployeeUserLinkDto?> GetEmployeeUserLinkAsync(long userId, CancellationToken cancellationToken = default)
        => DataOrNull(await new GetMyEmployeeLinkQueryHandler(_repository).Handle(new GetMyEmployeeLinkQuery(userId), cancellationToken));

    public async Task<EmployeeUserLinkDto> LinkEmployeeUserAsync(string employeeCode, long? userId, string user, CancellationToken cancellationToken = default)
        => Data(await new LinkEmployeeUserCommandHandler(_repository, Unit).Handle(new LinkEmployeeUserCommand(employeeCode, userId, user), cancellationToken));

    public async Task<EmployeeDetailsDto> CreateEmployeeAsync(CreateEmployeeDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateEmployeeCommandHandler(_repository, Unit, _fields).Handle(new CreateEmployeeCommand(dto, user), cancellationToken));

    public async Task<EmployeeDetailsDto> UpdateEmployeeAsync(string employeeCode, UpdateEmployeeDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new UpdateEmployeeCommandHandler(_repository, Unit, _fields).Handle(new UpdateEmployeeCommand(employeeCode, dto, user), cancellationToken));

    public async Task<EmployeeDetailsDto> UpdateEmployeeWorkAsync(string employeeCode, UpdateEmployeeWorkDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new UpdateEmployeeWorkCommandHandler(_repository, Unit, _fields).Handle(new UpdateEmployeeWorkCommand(employeeCode, dto, user), cancellationToken));

    public async Task<EmployeeDetailsDto> UpdateEmployeeBankAsync(string employeeCode, UpdateEmployeeBankDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new UpdateEmployeeBankCommandHandler(_repository, Unit, _fields).Handle(new UpdateEmployeeBankCommand(employeeCode, dto, user), cancellationToken));

    public async Task<EmployeeDetailsDto> UpdateEmployeeIdentityAsync(string employeeCode, UpdateEmployeeIdentityDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new UpdateEmployeeIdentityCommandHandler(_repository, Unit, _fields).Handle(new UpdateEmployeeIdentityCommand(employeeCode, dto, user), cancellationToken));

    public async Task DeleteEmployeeAsync(string employeeCode, string user, CancellationToken cancellationToken = default)
        => await new DeleteEmployeeCommandHandler(_repository, Unit, _fields).Handle(new DeleteEmployeeCommand(employeeCode, user), cancellationToken);

    public async Task<DependentDto> AddDependentAsync(string employeeCode, CreateDependentDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new AddDependentCommandHandler(_repository, Unit, _fields).Handle(new AddDependentCommand(employeeCode, dto, user), cancellationToken));

    public async Task DeleteDependentAsync(long dependentId, string user, CancellationToken cancellationToken = default)
        => await new DeleteDependentCommandHandler(_repository, Unit, _fields).Handle(new DeleteDependentCommand(dependentId, user), cancellationToken);

    public async Task<SalaryStructureDetailsDto> AssignSalaryStructureAsync(string employeeCode, AssignSalaryStructureDto dto, string user, CancellationToken cancellationToken = default)
    {
        IHrUnitOfWork unit = _unitOfWork ?? (_fields.IsEnforced ? new UnavailableHrUnitOfWork() : new PassThroughHrUnitOfWork());
        return Data(await new AssignSalaryStructureCommandHandler(_repository, unit, _fields)
            .Handle(new AssignSalaryStructureCommand(employeeCode, dto, user), cancellationToken));
    }

    public async Task<SalaryStructureDetailsDto?> GetActiveSalaryStructureAsync(string employeeCode, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
        => DataOrNull(await new GetActiveSalaryStructureQueryHandler(_repository, _fields).Handle(new GetActiveSalaryStructureQuery(employeeCode, asOfDate), cancellationToken));

    public async Task<IReadOnlyList<SalaryComponentDto>> GetSalaryComponentsAsync(CancellationToken cancellationToken = default)
        => Data(await new GetSalaryComponentsQueryHandler(_repository).Handle(new GetSalaryComponentsQuery(), cancellationToken));

    public async Task<SalaryComponentDto> CreateSalaryComponentAsync(CreateSalaryComponentDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateSalaryComponentCommandHandler(_repository).Handle(new CreateSalaryComponentCommand(dto, user), cancellationToken));

    private static T Data<T>(ApiResponse<T> response) => response.Data!;

    private static T? DataOrNull<T>(ApiResponse<T> response) where T : class => response.Success ? response.Data : null;
}
