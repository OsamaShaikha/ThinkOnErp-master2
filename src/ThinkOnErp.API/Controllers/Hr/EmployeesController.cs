using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/employees")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(
        IEmployeeService employeeService,
        ILogger<EmployeesController> logger)
    {
        _employeeService = employeeService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<EmployeeSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<EmployeeSummaryDto>>>> GetEmployees(
        [FromQuery] string? searchKeyword,
        [FromQuery] string? departmentCode,
        [FromQuery] string? status,
        [FromQuery] long? branchId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _employeeService.GetEmployeesPagedAsync(
            searchKeyword, departmentCode, status, branchId, pageIndex, pageSize, cancellationToken);

        var pagedResult = new PagedResultDto<EmployeeSummaryDto>(items, totalCount, pageIndex, pageSize);

        return Ok(ApiResponse<PagedResultDto<EmployeeSummaryDto>>.CreateSuccess(pagedResult, "Employees retrieved successfully."));
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailsDto>>> GetEmployeeByCode(
        string code,
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.GetEmployeeByCodeAsync(code, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<EmployeeDetailsDto>.CreateFailure($"Employee with code '{code}' not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<EmployeeDetailsDto>.CreateSuccess(result, "Employee retrieved successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailsDto>>> CreateEmployee(
        [FromBody] CreateEmployeeDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _employeeService.CreateEmployeeAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<EmployeeDetailsDto>.CreateSuccess(result, "Employee created successfully."));
    }

    [HttpPut("{code}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailsDto>>> UpdateEmployee(
        string code,
        [FromBody] UpdateEmployeeDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _employeeService.UpdateEmployeeAsync(code, dto, user, cancellationToken);
        return Ok(ApiResponse<EmployeeDetailsDto>.CreateSuccess(result, "Employee updated successfully."));
    }

    [HttpDelete("{code}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEmployee(
        string code,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        await _employeeService.DeleteEmployeeAsync(code, user, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(true, "Employee deactivated successfully."));
    }

    [HttpPost("{code}/dependents")]
    [ProducesResponseType(typeof(ApiResponse<DependentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DependentDto>>> AddDependent(
        string code,
        [FromBody] CreateDependentDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _employeeService.AddDependentAsync(code, dto, user, cancellationToken);
        return Ok(ApiResponse<DependentDto>.CreateSuccess(result, "Dependent added successfully."));
    }

    [HttpDelete("dependents/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteDependent(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        await _employeeService.DeleteDependentAsync(id, user, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(true, "Dependent deleted successfully."));
    }

    [HttpPost("{code}/salary-structure")]
    [ProducesResponseType(typeof(ApiResponse<SalaryStructureDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SalaryStructureDetailsDto>>> AssignSalaryStructure(
        string code,
        [FromBody] AssignSalaryStructureDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _employeeService.AssignSalaryStructureAsync(code, dto, user, cancellationToken);
        return Ok(ApiResponse<SalaryStructureDetailsDto>.CreateSuccess(result, "Salary structure assigned successfully."));
    }

    [HttpGet("{code}/salary-structure")]
    [ProducesResponseType(typeof(ApiResponse<SalaryStructureDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SalaryStructureDetailsDto>>> GetSalaryStructure(
        string code,
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.GetActiveSalaryStructureAsync(code, asOfDate, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<SalaryStructureDetailsDto>.CreateFailure($"No active salary structure found for employee '{code}'.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<SalaryStructureDetailsDto>.CreateSuccess(result, "Salary structure retrieved successfully."));
    }
}
