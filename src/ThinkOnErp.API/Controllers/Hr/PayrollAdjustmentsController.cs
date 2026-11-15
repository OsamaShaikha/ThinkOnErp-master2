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
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.ApproveAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.CreateAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.CreateOvertimeAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.DeleteAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.PostAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.RejectAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.UnpostAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Queries.GetAdjustmentById;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Queries.GetAdjustments;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/adjustments")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class PayrollAdjustmentsController : HrControllerBase
{
    private readonly IMediator _mediator;

    public PayrollAdjustmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "create")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> CreateAdjustment(
        [FromBody] CreatePayrollAdjustmentDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CreateAdjustmentCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("overtime")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "create")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> CreateOvertimeAdjustment(
        [FromBody] CreateOvertimePayrollAdjustmentDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CreateOvertimeAdjustmentCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "approve")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> ApproveAdjustment(
        long id,
        [FromBody] PayrollAdjustmentDecisionDto decision,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new ApproveAdjustmentCommand(id, decision.Reason, CurrentUsername, decision.BranchId), cancellationToken));

    [HttpPost("{id:long}/reject")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "reject")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> RejectAdjustment(
        long id,
        [FromBody] PayrollAdjustmentDecisionDto decision,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new RejectAdjustmentCommand(id, decision.Reason, CurrentUsername, decision.BranchId), cancellationToken));

    [HttpPost("{id:long}/post")]
    [HrPermission("hr-payroll-adjustments", "post")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> PostAdjustment(
        long id,
        [FromBody] PayrollAdjustmentDecisionDto decision,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new PostAdjustmentCommand(id, decision.Reason, CurrentUsername, decision.BranchId), cancellationToken));

    [HttpPost("{id:long}/unpost")]
    [HrPermission("hr-payroll-adjustments", "reverse")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> UnpostAdjustment(
        long id,
        [FromQuery] long? branchId,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new UnpostAdjustmentCommand(id, CurrentUsername, branchId, reason), cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>>> GetAdjustments(
        [FromQuery] long companyId,
        [FromQuery] string? employeeCode,
        [FromQuery] string? payPeriod,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetAdjustmentsQuery(companyId, employeeCode, payPeriod, status), cancellationToken));

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "view")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> GetAdjustmentById(
        long id,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetAdjustmentByIdQuery(id), cancellationToken));

    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "delete")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteAdjustment(
        long id,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new DeleteAdjustmentCommand(id), cancellationToken));
}
