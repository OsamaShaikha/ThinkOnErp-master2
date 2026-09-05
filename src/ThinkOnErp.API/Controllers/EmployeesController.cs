using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr/employees")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
    }

    /// <summary>
    /// Retrieves all employees with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<EmployeeDto>>>> GetAll(
        [FromQuery] string? departmentCode = null,
        [FromQuery] long? branchId = null,
        [FromQuery] string? status = null,
        [FromQuery] bool activeOnly = true)
    {
        var list = await _employeeService.GetAllAsync(departmentCode, branchId, status, activeOnly);
        return Ok(ApiResponse<List<EmployeeDto>>.CreateSuccess(list, "Employees retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Retrieves full employee profile by employee code.
    /// </summary>
    [HttpGet("{employeeCode}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> GetByCode(string employeeCode)
    {
        var emp = await _employeeService.GetByCodeAsync(employeeCode, includeDetails: true);
        if (emp == null)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Employee ({employeeCode}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<EmployeeDto>.CreateSuccess(emp, "Employee profile retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Onboards and creates a new employee, generating an immutable HIRE event.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> Create([FromBody] CreateEmployeeDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var created = await _employeeService.CreateAsync(dto, username);
        return CreatedAtAction(nameof(GetByCode), new { employeeCode = created.EmployeeCode }, ApiResponse<EmployeeDto>.CreateSuccess(created, "Employee created and onboarded successfully.", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Updates employee master details.
    /// </summary>
    [HttpPut("{employeeCode}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> Update(string employeeCode, [FromBody] UpdateEmployeeDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var updated = await _employeeService.UpdateAsync(employeeCode, dto, username);
        return Ok(ApiResponse<EmployeeDto>.CreateSuccess(updated, "Employee updated successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Changes employee employment status and creates an auditable lifecycle event.
    /// </summary>
    [HttpPatch("{employeeCode}/status")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> ChangeStatus(string employeeCode, [FromBody] ChangeEmployeeStatusDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var updated = await _employeeService.ChangeStatusAsync(employeeCode, dto, username);
        return Ok(ApiResponse<EmployeeDto>.CreateSuccess(updated, "Employee status changed successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Records an immutable lifecycle event for an employee.
    /// </summary>
    [HttpPost("{employeeCode}/events")]
    [ProducesResponseType(typeof(ApiResponse<EmploymentEventDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<EmploymentEventDto>>> RecordEvent(string employeeCode, [FromBody] RecordEmploymentEventDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var recorded = await _employeeService.RecordEventAsync(employeeCode, dto, username);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<EmploymentEventDto>.CreateSuccess(recorded, "Employment event recorded successfully.", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Retrieves the immutable historical event trail for an employee.
    /// </summary>
    [HttpGet("{employeeCode}/events")]
    [ProducesResponseType(typeof(ApiResponse<List<EmploymentEventDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<EmploymentEventDto>>>> GetEvents(string employeeCode)
    {
        var events = await _employeeService.GetEventsAsync(employeeCode);
        return Ok(ApiResponse<List<EmploymentEventDto>>.CreateSuccess(events, "Employment events retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Retrieves dependents registered under an employee.
    /// </summary>
    [HttpGet("{employeeCode}/dependents")]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeDependentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<EmployeeDependentDto>>>> GetDependents(string employeeCode)
    {
        var dependents = await _employeeService.GetDependentsAsync(employeeCode);
        return Ok(ApiResponse<List<EmployeeDependentDto>>.CreateSuccess(dependents, "Employee dependents retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Adds a dependent for an employee.
    /// </summary>
    [HttpPost("{employeeCode}/dependents")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDependentDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<EmployeeDependentDto>>> AddDependent(string employeeCode, [FromBody] CreateEmployeeDependentDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var created = await _employeeService.AddDependentAsync(employeeCode, dto, username);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<EmployeeDependentDto>.CreateSuccess(created, "Employee dependent added successfully.", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Updates a dependent's details.
    /// </summary>
    [HttpPut("dependents/{dependentId:long}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDependentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeDependentDto>>> UpdateDependent(long dependentId, [FromBody] UpdateEmployeeDependentDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var updated = await _employeeService.UpdateDependentAsync(dependentId, dto, username);
        return Ok(ApiResponse<EmployeeDependentDto>.CreateSuccess(updated, "Employee dependent updated successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Deletes a dependent.
    /// </summary>
    [HttpDelete("dependents/{dependentId:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDependent(long dependentId)
    {
        var success = await _employeeService.DeleteDependentAsync(dependentId);
        if (!success)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Dependent ({dependentId}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { deleted = true }, "Employee dependent deleted successfully.", StatusCodes.Status200OK));
    }
}
