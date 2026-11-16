using System;
using System.Collections.Generic;
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
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.CreateAttendancePolicy;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.DeactivateAttendancePolicy;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendancePolicies;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendancePolicyById;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/attendance/policies")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class AttendancePoliciesController : HrControllerBase
{
    private readonly IMediator _mediator;

    public AttendancePoliciesController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttendancePolicy>>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AttendancePolicy>>>> GetPolicies(CancellationToken ct)
        => FromResponse(await _mediator.Send(new GetAttendancePoliciesQuery(GetTenant().CompanyId), ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AttendancePolicy>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendancePolicy>>> GetPolicy(long id, CancellationToken ct)
        => FromResponse(await _mediator.Send(new GetAttendancePolicyByIdQuery(id, GetTenant().CompanyId), ct));

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AttendancePolicy>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendancePolicy>>> CreatePolicy(
        [FromBody] CreateAttendancePolicyDto request, CancellationToken ct)
        => FromResponse(await _mediator.Send(new CreateAttendancePolicyCommand(request, GetTenant().CompanyId, CurrentUsername), ct));

    [HttpPost("{id:long}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<AttendancePolicy>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendancePolicy>>> Deactivate(long id, CancellationToken ct)
        => FromResponse(await _mediator.Send(new DeactivateAttendancePolicyCommand(id, GetTenant().CompanyId, CurrentUsername), ct));

    private TenantRequestContext GetTenant()
        => HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var value) && value is TenantRequestContext tenant && tenant.CompanyId > 0
            ? tenant
            : throw new InvalidOperationException("Trusted tenant context is required.");
}
