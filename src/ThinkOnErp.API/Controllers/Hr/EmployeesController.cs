using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.AddDependent;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.AssignSalaryStructure;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.CreateEmployee;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.DeleteDependent;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.DeleteEmployee;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.ImportEmployees;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.LinkEmployeeUser;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployee;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployeeBank;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployeeIdentity;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployeeWork;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.ExportEmployees;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetActiveSalaryStructure;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetEmployeeByCode;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetEmployeeImportTemplate;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetEmployees;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetMyEmployeeLink;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetMyTeam;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.ValidateEmployeeImport;

namespace ThinkOnErp.API.Controllers.Hr;

/// <summary>
/// HR employee master data: profile, dependents, salary structure, login link and Excel import/export.
/// Thin HTTP adapter: every endpoint dispatches one MediatR request from Features/Hr/Employees.
/// </summary>
[ApiController]
[Route("api/hr/employees")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class EmployeesController : HrControllerBase
{
    private const string InvalidFileMessage = "Please provide a valid Excel (.xlsx) file.";

    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator) => _mediator = mediator;

    // ---------- Queries ----------

    /// <summary>Paged employee list with search, department, status and branch filters.</summary>
    [HttpGet]
    [HrPermission("hr-employees", "view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<EmployeeSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<EmployeeSummaryDto>>>> GetEmployees(
        [FromQuery] string? searchKeyword,
        [FromQuery] string? departmentCode,
        [FromQuery] string? status,
        [FromQuery] long? branchId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(
            new GetEmployeesQuery(searchKeyword, departmentCode, status, branchId, pageIndex, pageSize), cancellationToken));

    /// <summary>Full employee profile; sensitive groups are hidden unless the caller has field permissions.</summary>
    [HttpGet("{code}")]
    [HrPermission("hr-employees", "view")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailsDto>>> GetEmployeeByCode(string code, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetEmployeeByCodeQuery(code), cancellationToken));

    /// <summary>Direct reports of the employee linked to the signed-in user.</summary>
    [HttpGet("me/team")]
    [HrPermission("hr-employees", "view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<EmployeeTeamMemberDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<EmployeeTeamMemberDto>>>> GetMyTeam(
        [FromQuery] bool activeOnly = true,
        [FromQuery] string? search = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        return FromResponse(await _mediator.Send(new GetMyTeamQuery(userId, activeOnly, search, pageIndex, pageSize), cancellationToken));
    }

    /// <summary>The employee record linked to the signed-in user.</summary>
    [HttpGet("me/user-link")]
    [HrPermission("hr-employees", "view", selfService: true)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeUserLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeUserLinkDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeUserLinkDto>>> GetMyEmployeeLink(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        return FromResponse(await _mediator.Send(new GetMyEmployeeLinkQuery(userId), cancellationToken));
    }

    /// <summary>Salary structure active on <paramref name="asOfDate"/> (default: today).</summary>
    [HttpGet("{code}/salary-structure")]
    [HrPermission("hr-employees", "view")]
    [ProducesResponseType(typeof(ApiResponse<SalaryStructureDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SalaryStructureDetailsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SalaryStructureDetailsDto>>> GetSalaryStructure(
        string code, [FromQuery] DateTime? asOfDate, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetActiveSalaryStructureQuery(code, asOfDate), cancellationToken));

    // ---------- Profile commands ----------

    [HttpPost]
    [HrPermission("hr-employees", "create")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailsDto>>> CreateEmployee(
        [FromBody] CreateEmployeeDto dto, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CreateEmployeeCommand(dto, CurrentUsername), cancellationToken));

    /// <summary>Replaces the full profile (requires identity and bank edit permissions).</summary>
    [HttpPut("{code}")]
    [HrPermission("hr-employees", "edit")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailsDto>>> UpdateEmployee(
        string code, [FromBody] UpdateEmployeeDto dto, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new UpdateEmployeeCommand(code, dto, CurrentUsername), cancellationToken));

    [HttpPut("{code}/work-profile")]
    [HrPermission("hr-employees", "edit")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailsDto>>> UpdateEmployeeWork(
        string code, [FromBody] UpdateEmployeeWorkDto dto, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new UpdateEmployeeWorkCommand(code, dto, CurrentUsername), cancellationToken));

    [HttpPut("{code}/bank")]
    [HrPermission("hr-employees", "edit")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailsDto>>> UpdateEmployeeBank(
        string code, [FromBody] UpdateEmployeeBankDto dto, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new UpdateEmployeeBankCommand(code, dto, CurrentUsername), cancellationToken));

    [HttpPut("{code}/identity")]
    [HrPermission("hr-employees", "edit")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailsDto>>> UpdateEmployeeIdentity(
        string code, [FromBody] UpdateEmployeeIdentityDto dto, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new UpdateEmployeeIdentityCommand(code, dto, CurrentUsername), cancellationToken));

    /// <summary>Always refused: termination goes through End of Service. Kept for API compatibility.</summary>
    [HttpDelete("{code}")]
    [HrPermission("hr-employees", "delete")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEmployee(string code, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new DeleteEmployeeCommand(code, CurrentUsername), cancellationToken));

    [HttpPut("{code}/user-link")]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-employees", "link-user")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeUserLinkDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeUserLinkDto>>> LinkEmployeeUser(
        string code, [FromBody] LinkEmployeeUserDto dto, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new LinkEmployeeUserCommand(code, dto.UserId, CurrentUsername), cancellationToken));

    // ---------- Dependents & salary ----------

    [HttpPost("{code}/dependents")]
    [HrPermission("hr-employees", "create")]
    [ProducesResponseType(typeof(ApiResponse<DependentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DependentDto>>> AddDependent(
        string code, [FromBody] CreateDependentDto dto, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new AddDependentCommand(code, dto, CurrentUsername), cancellationToken));

    [HttpDelete("dependents/{id:long}")]
    [HrPermission("hr-employees", "delete")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteDependent(long id, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new DeleteDependentCommand(id, CurrentUsername), cancellationToken));

    [HttpPost("{code}/salary-structure")]
    [HrPermission("hr-employees", "create")]
    [ProducesResponseType(typeof(ApiResponse<SalaryStructureDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SalaryStructureDetailsDto>>> AssignSalaryStructure(
        string code, [FromBody] AssignSalaryStructureDto dto, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new AssignSalaryStructureCommand(code, dto, CurrentUsername), cancellationToken));

    // ---------- Excel ----------

    [HttpGet("export")]
    [HrPermission("hr-employees", "export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportEmployees(
        [FromQuery] string? searchKeyword,
        [FromQuery] string? departmentCode,
        [FromQuery] string? status,
        [FromQuery] long? branchId,
        CancellationToken cancellationToken)
    {
        var bytes = await _mediator.Send(new ExportEmployeesQuery(searchKeyword, departmentCode, status, branchId), cancellationToken);
        return File(bytes, XlsxContentType, $"Employees_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
    }

    [HttpGet("export-template")]
    [HrPermission("hr-employees", "view")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var bytes = await _mediator.Send(new GetEmployeeImportTemplateQuery(), cancellationToken);
        return File(bytes, XlsxContentType, "Employee_Import_Template.xlsx");
    }

    [HttpPost("import/validate")]
    [Consumes("multipart/form-data")]
    [HrPermission("hr-employees", "import")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeImportResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<EmployeeImportResultDto>>> ValidateImport(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<EmployeeImportResultDto>.CreateFailure(InvalidFileMessage));

        await using var stream = file.OpenReadStream();
        return FromResponse(await _mediator.Send(new ValidateEmployeeImportQuery(stream), cancellationToken));
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [HrPermission("hr-employees", "import")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeImportResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<EmployeeImportResultDto>>> ImportEmployees(
        IFormFile? file, [FromQuery] bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<EmployeeImportResultDto>.CreateFailure(InvalidFileMessage));

        await using var stream = file.OpenReadStream();
        return FromResponse(await _mediator.Send(new ImportEmployeesCommand(stream, updateExisting, CurrentUsername), cancellationToken));
    }
}
