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
using ThinkOnErp.Application.Features.Hr.Organization.Commands.CreateDepartment;
using ThinkOnErp.Application.Features.Hr.Organization.Commands.CreatePosition;
using ThinkOnErp.Application.Features.Hr.Organization.Commands.UpdateDepartment;
using ThinkOnErp.Application.Features.Hr.Organization.Commands.UpdatePosition;
using ThinkOnErp.Application.Features.Hr.Organization.Queries.GetDepartmentByCode;
using ThinkOnErp.Application.Features.Hr.Organization.Queries.GetDepartments;
using ThinkOnErp.Application.Features.Hr.Organization.Queries.GetOrganizationReferenceIntegrity;
using ThinkOnErp.Application.Features.Hr.Organization.Queries.GetPositionByCode;
using ThinkOnErp.Application.Features.Hr.Organization.Queries.GetPositions;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/organization"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class OrganizationController : HrControllerBase
{
    private readonly IMediator _mediator;

    public OrganizationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("integrity"), Authorize(Policy = "AdminOnly"), HrPermission("hr-employees", "configure")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationReferenceIntegritySummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OrganizationReferenceIntegritySummary>>> ReferenceIntegrity(CancellationToken ct)
        => FromResponse(await _mediator.Send(new GetOrganizationReferenceIntegrityQuery(), ct));

    [HttpGet("departments"), HrPermission("hr-employees", "view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<HrDepartment>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HrDepartment>>>> Departments(CancellationToken ct)
        => FromResponse(await _mediator.Send(new GetDepartmentsQuery(), ct));

    [HttpGet("positions"), HrPermission("hr-employees", "view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<HrPosition>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HrPosition>>>> Positions([FromQuery] string? departmentCode, CancellationToken ct)
        => FromResponse(await _mediator.Send(new GetPositionsQuery(departmentCode), ct));

    [HttpGet("departments/{code}"), HrPermission("hr-employees", "view")]
    [ProducesResponseType(typeof(ApiResponse<HrDepartment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HrDepartment>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HrDepartment>>> Department(string code, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetDepartmentByCodeQuery(code), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }

    [HttpGet("positions/{code}"), HrPermission("hr-employees", "view")]
    [ProducesResponseType(typeof(ApiResponse<HrPosition>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HrPosition>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HrPosition>>> Position(string code, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPositionByCodeQuery(code), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }

    [HttpPost("departments/{code}"), HrPermission("hr-employees", "configure")]
    [ProducesResponseType(typeof(ApiResponse<HrDepartment>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<HrDepartment>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HrDepartment>>> CreateDepartment(string code, [FromBody] DepartmentWrite value, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateDepartmentCommand(code, value, CurrentUsername), ct);
        if (response.Success && response.Data != null)
        {
            Response.Headers.ETag = '"' + response.Data.Version + '"';
            return CreatedAtAction(nameof(Department), new { code = response.Data.DepartmentCode }, response);
        }
        return FromResponse(response);
    }

    [HttpPost("positions/{code}"), HrPermission("hr-employees", "configure")]
    [ProducesResponseType(typeof(ApiResponse<HrPosition>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<HrPosition>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HrPosition>>> CreatePosition(string code, [FromBody] PositionWrite value, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreatePositionCommand(code, value, CurrentUsername), ct);
        if (response.Success && response.Data != null)
        {
            Response.Headers.ETag = '"' + response.Data.Version + '"';
            return CreatedAtAction(nameof(Position), new { code = response.Data.PositionCode }, response);
        }
        return FromResponse(response);
    }

    [HttpPut("departments/{code}"), HrPermission("hr-employees", "configure")]
    [ProducesResponseType(typeof(ApiResponse<HrDepartment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HrDepartment>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<HrDepartment>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<HrDepartment>>> UpdateDepartment(string code, [FromBody] DepartmentWrite value, [FromHeader(Name = "If-Match")] string version, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateDepartmentCommand(code, value, version, CurrentUsername), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }

    [HttpPut("positions/{code}"), HrPermission("hr-employees", "configure")]
    [ProducesResponseType(typeof(ApiResponse<HrPosition>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HrPosition>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<HrPosition>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<HrPosition>>> UpdatePosition(string code, [FromBody] PositionWrite value, [FromHeader(Name = "If-Match")] string version, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdatePositionCommand(code, value, version, CurrentUsername), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }
}
