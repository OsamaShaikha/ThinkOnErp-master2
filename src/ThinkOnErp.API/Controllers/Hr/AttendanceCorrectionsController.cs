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
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/attendance-corrections")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class AttendanceCorrectionsController : ControllerBase
{
    private readonly IAttendanceCorrectionService _correctionService;
    private readonly ILogger<AttendanceCorrectionsController> _logger;

    public AttendanceCorrectionsController(
        IAttendanceCorrectionService correctionService,
        ILogger<AttendanceCorrectionsController> logger)
    {
        _correctionService = correctionService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AttendanceCorrectionRequest>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AttendanceCorrectionRequest>>> SubmitCorrection(
        [FromBody] AttendanceCorrectionRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _correctionService.SubmitRequestAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<AttendanceCorrectionRequest>.CreateSuccess(result, "Correction request submitted successfully."));
    }

    [HttpPost("{id:long}/process")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceCorrectionRequest>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AttendanceCorrectionRequest>>> ProcessCorrection(
        long id,
        [FromBody] ProcessAttendanceCorrectionDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _correctionService.ProcessRequestAsync(id, dto, user, cancellationToken);
        return Ok(ApiResponse<AttendanceCorrectionRequest>.CreateSuccess(result, "Correction request processed successfully."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>>> GetCorrections(
        [FromQuery] string? employeeCode,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _correctionService.GetRequestsAsync(employeeCode, status, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>.CreateSuccess(result, "Correction requests retrieved."));
    }
}
