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
[Route("api/hr/departments")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
    }

    /// <summary>
    /// Retrieves all departments in a flat list.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<DepartmentDto>>>> GetAll([FromQuery] long? branchId = null, [FromQuery] bool activeOnly = true)
    {
        var list = await _departmentService.GetAllAsync(branchId, activeOnly);
        return Ok(ApiResponse<List<DepartmentDto>>.CreateSuccess(list, "Departments retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Retrieves departments organized as a hierarchical tree.
    /// </summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentTreeNodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<DepartmentTreeNodeDto>>>> GetTree([FromQuery] long? branchId = null)
    {
        var tree = await _departmentService.GetTreeAsync(branchId);
        return Ok(ApiResponse<List<DepartmentTreeNodeDto>>.CreateSuccess(tree, "Department tree retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Retrieves details of a specific department by code.
    /// </summary>
    [HttpGet("{departmentCode}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> GetByCode(string departmentCode)
    {
        var dept = await _departmentService.GetByCodeAsync(departmentCode);
        if (dept == null)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Department ({departmentCode}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<DepartmentDto>.CreateSuccess(dept, "Department retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Creates a new department linked to Cost Center and Branch.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> Create([FromBody] CreateDepartmentDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var created = await _departmentService.CreateAsync(dto, username);
        return CreatedAtAction(nameof(GetByCode), new { departmentCode = created.DepartmentCode }, ApiResponse<DepartmentDto>.CreateSuccess(created, "Department created successfully.", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Updates an existing department.
    /// </summary>
    [HttpPut("{departmentCode}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> Update(string departmentCode, [FromBody] UpdateDepartmentDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var updated = await _departmentService.UpdateAsync(departmentCode, dto, username);
        return Ok(ApiResponse<DepartmentDto>.CreateSuccess(updated, "Department updated successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Deletes a department if it has no children or assigned employees.
    /// </summary>
    [HttpDelete("{departmentCode}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string departmentCode)
    {
        var success = await _departmentService.DeleteAsync(departmentCode);
        if (!success)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Department ({departmentCode}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { deleted = true }, "Department deleted successfully.", StatusCodes.Status200OK));
    }
}
