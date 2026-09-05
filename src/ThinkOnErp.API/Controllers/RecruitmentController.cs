using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr/recruitment")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class RecruitmentController : ControllerBase
{
    private readonly IRecruitmentService _recruitmentService;

    public RecruitmentController(IRecruitmentService recruitmentService)
    {
        _recruitmentService = recruitmentService ?? throw new ArgumentNullException(nameof(recruitmentService));
    }

    // Requisitions
    [HttpGet("requisitions")]
    [ProducesResponseType(typeof(List<JobRequisitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobRequisitionDto>>> GetAllRequisitions([FromQuery] string? status = null)
    {
        var list = await _recruitmentService.GetAllRequisitionsAsync(status);
        return Ok(list);
    }

    [HttpGet("requisitions/{code}")]
    [ProducesResponseType(typeof(JobRequisitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobRequisitionDto>> GetRequisitionByCode(string code)
    {
        var req = await _recruitmentService.GetRequisitionByCodeAsync(code);
        if (req == null)
        {
            return NotFound(new { message = $"Job requisition '{code}' not found." });
        }
        return Ok(req);
    }

    [HttpPost("requisitions")]
    [ProducesResponseType(typeof(JobRequisitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobRequisitionDto>> CreateRequisition([FromBody] CreateJobRequisitionDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _recruitmentService.CreateRequisitionAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetRequisitionByCode), new { code = created.RequisitionCode }, created);
    }

    [HttpPost("requisitions/{code}/approve")]
    [ProducesResponseType(typeof(JobRequisitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobRequisitionDto>> ApproveRequisition(string code)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _recruitmentService.ApproveRequisitionAsync(code, currentUser);
        return Ok(result);
    }

    // Candidates
    [HttpGet("candidates")]
    [ProducesResponseType(typeof(List<CandidateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CandidateDto>>> GetAllCandidates()
    {
        var list = await _recruitmentService.GetAllCandidatesAsync();
        return Ok(list);
    }

    [HttpGet("candidates/{code}")]
    [ProducesResponseType(typeof(CandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateDto>> GetCandidateByCode(string code)
    {
        var candidate = await _recruitmentService.GetCandidateByCodeAsync(code);
        if (candidate == null)
        {
            return NotFound(new { message = $"Candidate '{code}' not found." });
        }
        return Ok(candidate);
    }

    [HttpPost("candidates")]
    [ProducesResponseType(typeof(CandidateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CandidateDto>> CreateCandidate([FromBody] CreateCandidateDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _recruitmentService.CreateCandidateAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetCandidateByCode), new { code = created.CandidateCode }, created);
    }

    [HttpPatch("applications/{id:long}/stage")]
    [ProducesResponseType(typeof(CandidateApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateApplicationDto>> UpdateApplicationStage(long id, [FromBody] UpdateApplicationStageDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _recruitmentService.UpdateApplicationStageAsync(id, dto, currentUser);
        return Ok(result);
    }

    [HttpPost("applications/{id:long}/hire")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDto>> HireCandidate(long id, [FromBody] HireCandidateDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var employee = await _recruitmentService.HireCandidateAsync(id, dto, currentUser);
        return Ok(employee);
    }

    // Onboarding Tasks
    [HttpGet("employees/{employeeCode}/onboarding-tasks")]
    [ProducesResponseType(typeof(List<OnboardingTaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OnboardingTaskDto>>> GetOnboardingTasks(string employeeCode)
    {
        var list = await _recruitmentService.GetOnboardingTasksAsync(employeeCode);
        return Ok(list);
    }

    [HttpPost("onboarding-tasks")]
    [ProducesResponseType(typeof(OnboardingTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnboardingTaskDto>> CreateOnboardingTask([FromBody] CreateOnboardingTaskDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _recruitmentService.CreateOnboardingTaskAsync(dto, currentUser);
        return Ok(created);
    }

    [HttpPost("onboarding-tasks/{id:long}/complete")]
    [ProducesResponseType(typeof(OnboardingTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnboardingTaskDto>> CompleteOnboardingTask(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _recruitmentService.CompleteOnboardingTaskAsync(id, currentUser);
        return Ok(result);
    }
}
