using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.SaveBranchAttendanceLocation;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetBranchAttendanceLocation;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/attendance/branches/{branchId:long}/location"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class BranchAttendanceLocationsController : HrControllerBase
{
    private readonly IMediator _mediator;

    public BranchAttendanceLocationsController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet, HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<BranchAttendanceLocationDto?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BranchAttendanceLocationDto?>>> Get(long branchId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBranchAttendanceLocationQuery(branchId), ct);
        if (response.Data?.Version != null)
        {
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        }
        return FromResponse(response);
    }

    [HttpPost, HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<BranchAttendanceLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BranchAttendanceLocationDto>), StatusCodes.Status412PreconditionFailed)]
    public Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>> Create(
        long branchId, [FromBody] WriteBranchAttendanceLocationDto dto, CancellationToken ct)
        => Save(branchId, dto, null, ct);

    [HttpPut, HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<BranchAttendanceLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BranchAttendanceLocationDto>), StatusCodes.Status412PreconditionFailed)]
    public Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>> Update(
        long branchId, [FromBody] WriteBranchAttendanceLocationDto dto,
        [FromHeader(Name = "If-Match")] string version, CancellationToken ct)
        => Save(branchId, dto, version, ct);

    private async Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>> Save(
        long branchId, WriteBranchAttendanceLocationDto dto, string? version, CancellationToken ct)
    {
        var response = await _mediator.Send(new SaveBranchAttendanceLocationCommand(branchId, dto, version, CurrentUsername), ct);
        if (response.Data?.Version != null)
        {
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        }
        return FromResponse(response);
    }
}
