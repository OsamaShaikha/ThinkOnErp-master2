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
[Route("api/hr/job-grades")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class JobGradesController : ControllerBase
{
    private readonly IJobGradeService _jobGradeService;

    public JobGradesController(IJobGradeService jobGradeService)
    {
        _jobGradeService = jobGradeService ?? throw new ArgumentNullException(nameof(jobGradeService));
    }

    /// <summary>
    /// Retrieves all job grades.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<JobGradeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<JobGradeDto>>>> GetAll([FromQuery] bool activeOnly = true)
    {
        var list = await _jobGradeService.GetAllAsync(activeOnly);
        return Ok(ApiResponse<List<JobGradeDto>>.CreateSuccess(list, "Job grades retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Retrieves a job grade by code.
    /// </summary>
    [HttpGet("{gradeCode}")]
    [ProducesResponseType(typeof(ApiResponse<JobGradeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JobGradeDto>>> GetByCode(string gradeCode)
    {
        var grade = await _jobGradeService.GetByCodeAsync(gradeCode);
        if (grade == null)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Job grade ({gradeCode}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<JobGradeDto>.CreateSuccess(grade, "Job grade retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Creates a new job grade.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<JobGradeDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<JobGradeDto>>> Create([FromBody] CreateJobGradeDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var created = await _jobGradeService.CreateAsync(dto, username);
        return CreatedAtAction(nameof(GetByCode), new { gradeCode = created.GradeCode }, ApiResponse<JobGradeDto>.CreateSuccess(created, "Job grade created successfully.", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Updates an existing job grade.
    /// </summary>
    [HttpPut("{gradeCode}")]
    [ProducesResponseType(typeof(ApiResponse<JobGradeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<JobGradeDto>>> Update(string gradeCode, [FromBody] UpdateJobGradeDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var updated = await _jobGradeService.UpdateAsync(gradeCode, dto, username);
        return Ok(ApiResponse<JobGradeDto>.CreateSuccess(updated, "Job grade updated successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Deletes a job grade.
    /// </summary>
    [HttpDelete("{gradeCode}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string gradeCode)
    {
        var success = await _jobGradeService.DeleteAsync(gradeCode);
        if (!success)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Job grade ({gradeCode}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { deleted = true }, "Job grade deleted successfully.", StatusCodes.Status200OK));
    }
}
