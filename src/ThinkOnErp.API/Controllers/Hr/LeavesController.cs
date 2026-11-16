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
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CancelLeaveRequest;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateBalanceMigrationDraft;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateLeaveCashoutDraft;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateLeaveType;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateOrUpdateLeavePolicy;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateYearEndCloseDraft;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.DeleteLeaveType;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.GrantOrAdjustBalance;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostBalanceMigration;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostLeaveCashout;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostYearEndClose;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ProcessLeaveRequest;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ReverseBalanceMigration;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ReverseLeaveCashout;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ReverseManualBalanceMovement;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunDailyAccrual;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunMonthlyAccrual;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunYearEndRollover;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunYearlyAllocation;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.SubmitLeaveRequest;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.UnpostYearEndClose;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.UpdateLeaveSettings;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.UpdateLeaveType;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.CalculateLeaveDays;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetBalanceMigration;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetEmployeeBalances;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveCashoutDraft;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveMovements;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeavePolicies;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveRequests;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveSettings;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveTypeByCode;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveTypes;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetYearEndClose;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/leaves")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class LeavesController : HrControllerBase
{
    private readonly IMediator _mediator;

    public LeavesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeaveTypeDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaveTypeDto>>>> GetLeaveTypes(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GetLeaveTypesQuery(activeOnly), cancellationToken));

    [HttpGet("types/{code}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> GetLeaveTypeByCode(
        string code,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GetLeaveTypeByCodeQuery(code), cancellationToken));

    [HttpPost("types")]
    [ProducesResponseType(typeof(ApiResponse<LeaveTypeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "create")]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> CreateLeaveType(
        [FromBody] CreateLeaveTypeDto dto,
        CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(new CreateLeaveTypeCommand(dto, CurrentUsername), cancellationToken);
        return response.Success && response.StatusCode == StatusCodes.Status201Created
            ? StatusCode(StatusCodes.Status201Created, response)
            : FromResponse(response);
    }

    [HttpPut("types/{code}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> UpdateLeaveType(
        string code,
        [FromBody] UpdateLeaveTypeDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new UpdateLeaveTypeCommand(code, dto, CurrentUsername), cancellationToken));

    [HttpDelete("types/{code}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HrPermission("hr-leaves", "delete")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteLeaveType(
        string code,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new DeleteLeaveTypeCommand(code, CurrentUsername), cancellationToken));

    [HttpGet("requests")]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<LeaveRequestDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<LeaveRequestDto>>>> GetLeaveRequests(
        [FromQuery] string? employeeCode,
        [FromQuery] string? leaveTypeCode,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GetLeaveRequestsQuery(
            employeeCode, leaveTypeCode, status, fromDate, toDate, pageIndex, pageSize), cancellationToken));

    [HttpGet("calculate-days")]
    [ProducesResponseType(typeof(ApiResponse<LeaveDaysCalculationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<LeaveDaysCalculationDto>>> CalculateLeaveDays(
        [FromQuery] string employeeCode,
        [FromQuery] string leaveTypeCode,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] long? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var targetCompanyId = ResolveCompanyId(companyId);
        return FromResponse(await _mediator.Send(new CalculateLeaveDaysQuery(
            employeeCode, leaveTypeCode, startDate, endDate, targetCompanyId), cancellationToken));
    }

    [HttpPost("requests")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> SubmitLeaveRequest(
        [FromBody] SubmitLeaveRequestDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new SubmitLeaveRequestCommand(
            dto, CurrentUsername, ResolveCompanyId(null)), cancellationToken));

    [HttpPost("requests/{id:long}/process")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "approve")]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> ProcessLeaveRequest(
        long id,
        [FromBody] ProcessLeaveRequestDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new ProcessLeaveRequestCommand(id, dto, CurrentUsername), cancellationToken));

    [HttpPost("requests/{employeeCode}/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit", selfService: true)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> CancelLeaveRequest(
        string employeeCode,
        long id,
        [FromBody] CancelLeaveRequestDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new CancelLeaveRequestCommand(employeeCode, id, dto, CurrentUsername), cancellationToken));

    [HttpGet("balances/{employeeCode}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeaveBalanceDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaveBalanceDto>>>> GetEmployeeBalances(
        string employeeCode,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetEmployeeBalancesQuery(employeeCode, year), cancellationToken));

    [HttpGet("balances/{employeeCode}/movements")]
    [ProducesResponseType(typeof(ApiResponse<LeaveMovementPageDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<LeaveMovementPageDto>>> GetMovements(string employeeCode,
        [FromQuery] string? leaveTypeCode = null, [FromQuery] DateTime? cycleStart = null,
        [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GetLeaveMovementsQuery(
            employeeCode, leaveTypeCode, cycleStart, page, size), cancellationToken));

    [HttpPost("balances/grant")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceDto>>> GrantOrAdjustBalance(
        [FromBody] GrantLeaveBalanceDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GrantOrAdjustBalanceCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("balances/{employeeCode}/movements/{movementId:long}/reverse")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceDto>>> ReverseManualBalanceMovement(
        string employeeCode, long movementId, [FromBody] ReverseLeaveBalanceMovementDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new ReverseManualBalanceMovementCommand(
            employeeCode, movementId, dto, CurrentUsername), cancellationToken));

    [HttpPost("balances/accrue-monthly")]
    [ProducesResponseType(typeof(ApiResponse<LeaveAccrualResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>> RunMonthlyAccrual(
        [FromBody] MonthlyLeaveAccrualDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new RunMonthlyAccrualCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("balances/accrue-daily")]
    [ProducesResponseType(typeof(ApiResponse<LeaveAccrualResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>> RunDailyAccrual(
        [FromBody] DailyLeaveAccrualDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new RunDailyAccrualCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("balances/yearly-grant")]
    [ProducesResponseType(typeof(ApiResponse<LeaveAccrualResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>> RunYearlyAllocation(
        [FromBody] YearlyLeaveAllocationDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new RunYearlyAllocationCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("balances/year-end-rollover")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRolloverResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "post")]
    public async Task<ActionResult<ApiResponse<LeaveRolloverResultDto>>> RunYearEndRollover(
        [FromBody] YearEndRolloverDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new RunYearEndRolloverCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("balances/year-end-close/draft")]
    [ProducesResponseType(typeof(ApiResponse<LeaveYearCloseDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveYearCloseDto>>> CreateYearEndCloseDraft(
        [FromBody] YearEndRolloverDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new CreateYearEndCloseDraftCommand(dto, CurrentUsername), cancellationToken));

    [HttpGet("balances/year-end-close/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveYearCloseDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<LeaveYearCloseDto>>> GetYearEndClose(long id, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GetYearEndCloseQuery(id), cancellationToken));

    [HttpPost("balances/year-end-close/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<LeaveYearCloseDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "post")]
    public async Task<ActionResult<ApiResponse<LeaveYearCloseDto>>> PostYearEndClose(long id, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new PostYearEndCloseCommand(id, CurrentUsername), cancellationToken));

    [HttpPost("balances/year-end-close/{id:long}/unpost")]
    [ProducesResponseType(typeof(ApiResponse<LeaveYearCloseDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "reverse")]
    public async Task<ActionResult<ApiResponse<LeaveYearCloseDto>>> UnpostYearEndClose(long id, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new UnpostYearEndCloseCommand(id, CurrentUsername), cancellationToken));

    [HttpPost("balances/cashout/draft")]
    [ProducesResponseType(typeof(ApiResponse<LeaveCashoutSettlementDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveCashoutSettlementDto>>> CreateLeaveCashoutDraft(
        [FromBody] CreateLeaveCashoutSettlementDto dto, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new CreateLeaveCashoutDraftCommand(dto, CurrentUsername), cancellationToken));

    [HttpGet("balances/cashout/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveCashoutSettlementDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<LeaveCashoutSettlementDto>>> GetLeaveCashoutDraft(long id, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GetLeaveCashoutDraftQuery(id), cancellationToken));

    [HttpPost("balances/cashout/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<LeaveCashoutSettlementDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "post")]
    public async Task<ActionResult<ApiResponse<LeaveCashoutSettlementDto>>> PostLeaveCashout(long id, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new PostLeaveCashoutCommand(id, CurrentUsername), cancellationToken));

    [HttpPost("balances/cashout/{id:long}/reverse")]
    [ProducesResponseType(typeof(ApiResponse<LeaveCashoutSettlementDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "reverse")]
    public async Task<ActionResult<ApiResponse<LeaveCashoutSettlementDto>>> ReverseLeaveCashout(long id,
        [FromBody] ReverseLeaveCashoutSettlementDto dto, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new ReverseLeaveCashoutCommand(id, dto, CurrentUsername), cancellationToken));

    [HttpPost("balances/migration/draft")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceMigrationDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "configure")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceMigrationDto>>> CreateBalanceMigrationDraft(
        [FromBody] CreateLeaveBalanceMigrationDto dto, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new CreateBalanceMigrationDraftCommand(dto, CurrentUsername), cancellationToken));

    [HttpGet("balances/migration/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceMigrationDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceMigrationDto>>> GetBalanceMigration(long id, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GetBalanceMigrationQuery(id), cancellationToken));

    [HttpPost("balances/migration/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceMigrationDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "post")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceMigrationDto>>> PostBalanceMigration(long id, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new PostBalanceMigrationCommand(id, CurrentUsername), cancellationToken));

    [HttpPost("balances/migration/{id:long}/reverse")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceMigrationDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "reverse")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceMigrationDto>>> ReverseBalanceMigration(long id, CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new ReverseBalanceMigrationCommand(id, CurrentUsername), cancellationToken));

    [HttpGet("policies")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>>> GetLeavePolicies(
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GetLeavePoliciesQuery(), cancellationToken));

    [HttpGet("settings")]
    [ProducesResponseType(typeof(ApiResponse<HrLeaveSettingsDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<HrLeaveSettingsDto>>> GetLeaveSettings(CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new GetLeaveSettingsQuery(), cancellationToken));

    [HttpPut("settings")]
    [ProducesResponseType(typeof(ApiResponse<HrLeaveSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "configure")]
    public async Task<ActionResult<ApiResponse<HrLeaveSettingsDto>>> UpdateLeaveSettings(
        [FromBody] UpdateHrLeaveSettingsDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new UpdateLeaveSettingsCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("policies")]
    [ProducesResponseType(typeof(ApiResponse<LeavePolicyDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "create")]
    public async Task<ActionResult<ApiResponse<LeavePolicyDetailsDto>>> CreateOrUpdateLeavePolicy(
        [FromBody] CreateLeavePolicyDto dto,
        CancellationToken cancellationToken = default)
        => FromResponse(await _mediator.Send(new CreateOrUpdateLeavePolicyCommand(dto, CurrentUsername), cancellationToken));

    private long ResolveCompanyId(long? companyId)
    {
        if (companyId.HasValue)
            return companyId.Value;

        if (HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var value) &&
            value is TenantRequestContext tenant)
            return tenant.CompanyId;

        throw new InvalidOperationException("The tenant context is required to resolve the leave calculation company.");
    }
}
