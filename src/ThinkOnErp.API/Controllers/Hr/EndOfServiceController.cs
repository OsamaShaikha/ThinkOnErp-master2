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
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.ApproveSettlement;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CalculateSettlement;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CancelSettlement;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CreateEndOfServicePayoutRequest;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetEndOfServiceHandoffStatus;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlementByEmployeeCode;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlementById;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlements;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/end-of-service")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class EndOfServiceController : HrControllerBase
{
    private readonly IMediator _mediator;

    public EndOfServiceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("calculate")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status400BadRequest)]
    [HrPermission("hr-end-of-service", "create")]
    public async Task<ActionResult<ApiResponse<EndOfServiceSettlementDto>>> CalculateSettlement(
        [FromBody] CreateEndOfServiceRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(ApiResponse<EndOfServiceSettlementDto>.CreateFailure("Request body cannot be null", statusCode: 400));

        if (!HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var tenantValue) ||
            tenantValue is not TenantRequestContext tenant)
            return Forbid();

        return FromResponse(await _mediator.Send(
            new CalculateSettlementCommand(request, tenant.CompanyId, CurrentUsername),
            cancellationToken));
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "approve")]
    public async Task<ActionResult<ApiResponse<EndOfServiceSettlementDto>>> ApproveSettlement(
        long id,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new ApproveSettlementCommand(id, CurrentUsername), cancellationToken));

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "reverse")]
    public async Task<ActionResult<ApiResponse<EndOfServiceSettlementDto>>> CancelSettlement(
        long id,
        [FromBody] CancelEndOfServiceRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiResponse<EndOfServiceSettlementDto>.CreateFailure("A reason is required to cancel an end of service settlement.", statusCode: 400));

        return FromResponse(await _mediator.Send(new CancelSettlementCommand(id, request.Reason, CurrentUsername), cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "view")]
    public async Task<ActionResult<ApiResponse<EndOfServiceSettlementDto>>> GetById(
        long id,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetSettlementByIdQuery(id), cancellationToken));

    [HttpGet("employee/{employeeCode}")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "view")]
    public async Task<ActionResult<ApiResponse<EndOfServiceSettlementDto>>> GetByEmployeeCode(
        string employeeCode,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetSettlementByEmployeeCodeQuery(employeeCode), cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-end-of-service", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>>> GetList(
        [FromQuery] long companyId,
        [FromQuery] long? branchId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetSettlementsQuery(companyId, branchId, status), cancellationToken));

    [HttpPost("{id:long}/payout-request")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServicePayoutHandoffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServicePayoutHandoffDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServicePayoutHandoffDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "approve")]
    public async Task<ActionResult<ApiResponse<EndOfServicePayoutHandoffDto>>> SubmitPayoutRequest(
        long id,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CreateEndOfServicePayoutRequestCommand(id, CurrentUsername), cancellationToken));

    [HttpGet("{id:long}/handoff-status")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceHandoffStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceHandoffStatusDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "view")]
    public async Task<ActionResult<ApiResponse<EndOfServiceHandoffStatusDto>>> GetHandoffStatus(
        long id,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetEndOfServiceHandoffStatusQuery(id), cancellationToken));
}
