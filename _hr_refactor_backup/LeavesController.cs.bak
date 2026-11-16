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
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/leaves")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class LeavesController : ControllerBase
{
    private readonly ILeaveService _leaveService;
    private readonly ILogger<LeavesController> _logger;

    public LeavesController(
        ILeaveService leaveService,
        ILogger<LeavesController> logger)
    {
        _leaveService = leaveService;
        _logger = logger;
    }

    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeaveTypeDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaveTypeDto>>>> GetLeaveTypes(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetLeaveTypesAsync(activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LeaveTypeDto>>.CreateSuccess(result, "Leave types retrieved successfully."));
    }

    [HttpGet("types/{code}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> GetLeaveTypeByCode(
        string code,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetLeaveTypeByCodeAsync(code, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<LeaveTypeDto>.CreateFailure($"Leave type with code '{code}' not found."));
        }
        return Ok(ApiResponse<LeaveTypeDto>.CreateSuccess(result));
    }

    [HttpPost("types")]
    [ProducesResponseType(typeof(ApiResponse<LeaveTypeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "create")]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> CreateLeaveType(
        [FromBody] CreateLeaveTypeDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.CreateLeaveTypeAsync(dto, user, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<LeaveTypeDto>.CreateSuccess(result, "Leave type created successfully."));
    }

    [HttpPut("types/{code}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> UpdateLeaveType(
        string code,
        [FromBody] UpdateLeaveTypeDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.UpdateLeaveTypeAsync(code, dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveTypeDto>.CreateSuccess(result, "Leave type updated successfully."));
    }

    [HttpDelete("types/{code}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HrPermission("hr-leaves", "delete")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteLeaveType(
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var success = await _leaveService.DeleteLeaveTypeAsync(code, user, cancellationToken);
        if (!success)
        {
            return NotFound(ApiResponse<bool>.CreateFailure($"Leave type with code '{code}' not found."));
        }
        return Ok(ApiResponse<bool>.CreateSuccess(true, "Leave type deactivated successfully."));
    }

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
    {
        var (items, totalCount) = await _leaveService.GetLeaveRequestsPagedAsync(
            employeeCode, leaveTypeCode, status, fromDate, toDate, pageIndex, pageSize, cancellationToken);

        var paged = new PagedResultDto<LeaveRequestDto>(items, totalCount, pageIndex, pageSize);
        return Ok(ApiResponse<PagedResultDto<LeaveRequestDto>>.CreateSuccess(paged, "Leave requests retrieved successfully."));
    }

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
        var result = await _leaveService.CalculateLeaveDaysAsync(
            employeeCode, leaveTypeCode, startDate, endDate, targetCompanyId, cancellationToken);
        return Ok(ApiResponse<LeaveDaysCalculationDto>.CreateSuccess(result, "Leave days calculated successfully."));
    }

    private long ResolveCompanyId(long? companyId)
    {
        if (companyId.HasValue)
            return companyId.Value;

        if (HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var value) &&
            value is TenantRequestContext tenant)
            return tenant.CompanyId;

        throw new InvalidOperationException("The tenant context is required to resolve the leave calculation company.");
    }

    [HttpPost("requests")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> SubmitLeaveRequest(
        [FromBody] SubmitLeaveRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.SubmitLeaveRequestAsync(dto, user, ResolveCompanyId(null), cancellationToken);
        return Ok(ApiResponse<LeaveRequestDto>.CreateSuccess(result, "Leave request submitted successfully."));
    }

    [HttpPost("requests/{id:long}/process")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "approve")]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> ProcessLeaveRequest(
        long id,
        [FromBody] ProcessLeaveRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.ProcessLeaveRequestAsync(id, dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveRequestDto>.CreateSuccess(result, "Leave request processed successfully."));
    }

    [HttpPost("requests/{employeeCode}/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit", selfService: true)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> CancelLeaveRequest(
        string employeeCode,
        long id,
        [FromBody] CancelLeaveRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.CancelLeaveRequestAsync(employeeCode, id, dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveRequestDto>.CreateSuccess(result, "Leave request cancelled and any posted balance usage reversed."));
    }

    [HttpGet("balances/{employeeCode}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeaveBalanceDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaveBalanceDto>>>> GetEmployeeBalances(
        string employeeCode,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var result = await _leaveService.GetEmployeeBalancesAsync(employeeCode, year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LeaveBalanceDto>>.CreateSuccess(result, "Leave balances retrieved successfully."));
    }

    [HttpGet("balances/{employeeCode}/movements")]
    [ProducesResponseType(typeof(ApiResponse<LeaveMovementPageDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<LeaveMovementPageDto>>> GetMovements(string employeeCode,
        [FromQuery] string? leaveTypeCode = null, [FromQuery] DateTime? cycleStart = null,
        [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetMovementsAsync(employeeCode, leaveTypeCode, cycleStart, page, size, cancellationToken);
        return Ok(ApiResponse<LeaveMovementPageDto>.CreateSuccess(result));
    }

    [HttpPost("balances/grant")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceDto>>> GrantOrAdjustBalance(
        [FromBody] GrantLeaveBalanceDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.GrantOrAdjustBalanceAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveBalanceDto>.CreateSuccess(result, "Leave balance granted/adjusted successfully."));
    }

    [HttpPost("balances/{employeeCode}/movements/{movementId:long}/reverse")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceDto>>> ReverseManualBalanceMovement(
        string employeeCode, long movementId, [FromBody] ReverseLeaveBalanceMovementDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.ReverseManualBalanceMovementAsync(employeeCode, movementId, dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveBalanceDto>.CreateSuccess(result, "Manual leave-balance adjustment reversed."));
    }

    [HttpPost("balances/accrue-monthly")]
    [ProducesResponseType(typeof(ApiResponse<LeaveAccrualResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>> RunMonthlyAccrual(
        [FromBody] MonthlyLeaveAccrualDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.RunMonthlyAccrualAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveAccrualResultDto>.CreateSuccess(result, "Monthly leave accrual completed successfully."));
    }

    [HttpPost("balances/accrue-daily")]
    [ProducesResponseType(typeof(ApiResponse<LeaveAccrualResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>> RunDailyAccrual(
        [FromBody] DailyLeaveAccrualDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.RunDailyAccrualAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveAccrualResultDto>.CreateSuccess(result, "Daily leave accrual completed successfully."));
    }

    [HttpPost("balances/yearly-grant")]
    [ProducesResponseType(typeof(ApiResponse<LeaveAccrualResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>> RunYearlyAllocation(
        [FromBody] YearlyLeaveAllocationDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.RunYearlyAllocationAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveAccrualResultDto>.CreateSuccess(result, "Yearly leave grant completed successfully."));
    }

    [HttpPost("balances/year-end-rollover")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRolloverResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "post")]
    public async Task<ActionResult<ApiResponse<LeaveRolloverResultDto>>> RunYearEndRollover(
        [FromBody] YearEndRolloverDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.RunYearEndRolloverAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveRolloverResultDto>.CreateSuccess(result, "Year-end leave rollover completed successfully."));
    }

    [HttpPost("balances/year-end-close/draft")]
    [ProducesResponseType(typeof(ApiResponse<LeaveYearCloseDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveYearCloseDto>>> CreateYearEndCloseDraft(
        [FromBody] YearEndRolloverDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.CreateLeaveYearCloseDraftAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveYearCloseDto>.CreateSuccess(result, "Leave-year close draft prepared."));
    }

    [HttpGet("balances/year-end-close/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveYearCloseDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<LeaveYearCloseDto>>> GetYearEndClose(long id, CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetLeaveYearCloseAsync(id, cancellationToken);
        return Ok(ApiResponse<LeaveYearCloseDto>.CreateSuccess(result, "Leave-year close retrieved."));
    }

    [HttpPost("balances/year-end-close/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<LeaveYearCloseDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "post")]
    public async Task<ActionResult<ApiResponse<LeaveYearCloseDto>>> PostYearEndClose(long id, CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.PostLeaveYearCloseAsync(id, user, cancellationToken);
        return Ok(ApiResponse<LeaveYearCloseDto>.CreateSuccess(result, "Leave-year close posted."));
    }

    [HttpPost("balances/year-end-close/{id:long}/unpost")]
    [ProducesResponseType(typeof(ApiResponse<LeaveYearCloseDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "reverse")]
    public async Task<ActionResult<ApiResponse<LeaveYearCloseDto>>> UnpostYearEndClose(long id, CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.UnpostLeaveYearCloseAsync(id, user, cancellationToken);
        return Ok(ApiResponse<LeaveYearCloseDto>.CreateSuccess(result, "Leave-year close unposted."));
    }

    [HttpPost("balances/cashout/draft")]
    [ProducesResponseType(typeof(ApiResponse<LeaveCashoutSettlementDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "edit")]
    public async Task<ActionResult<ApiResponse<LeaveCashoutSettlementDto>>> CreateLeaveCashoutDraft(
        [FromBody] CreateLeaveCashoutSettlementDto dto, CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.CreateLeaveCashoutSettlementDraftAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveCashoutSettlementDto>.CreateSuccess(result, "Leave cashout settlement draft prepared for review."));
    }

    [HttpGet("balances/cashout/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveCashoutSettlementDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<LeaveCashoutSettlementDto>>> GetLeaveCashoutDraft(long id, CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetLeaveCashoutSettlementAsync(id, cancellationToken);
        return Ok(ApiResponse<LeaveCashoutSettlementDto>.CreateSuccess(result));
    }

    [HttpPost("balances/cashout/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<LeaveCashoutSettlementDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "post")]
    public async Task<ActionResult<ApiResponse<LeaveCashoutSettlementDto>>> PostLeaveCashout(long id, CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.PostLeaveCashoutSettlementAsync(id, user, cancellationToken);
        return Ok(ApiResponse<LeaveCashoutSettlementDto>.CreateSuccess(result, "Leave cashout payroll adjustments queued for review."));
    }

    [HttpPost("balances/cashout/{id:long}/reverse")]
    [ProducesResponseType(typeof(ApiResponse<LeaveCashoutSettlementDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "reverse")]
    public async Task<ActionResult<ApiResponse<LeaveCashoutSettlementDto>>> ReverseLeaveCashout(long id,
        [FromBody] ReverseLeaveCashoutSettlementDto dto, CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.ReverseLeaveCashoutSettlementAsync(id, dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveCashoutSettlementDto>.CreateSuccess(result, "Leave cashout settlement reversed."));
    }

    [HttpPost("balances/migration/draft")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceMigrationDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "configure")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceMigrationDto>>> CreateBalanceMigrationDraft(
        [FromBody] CreateLeaveBalanceMigrationDto dto, CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.CreateLeaveBalanceMigrationDraftAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveBalanceMigrationDto>.CreateSuccess(result, "Leave-balance migration draft prepared for review."));
    }

    [HttpGet("balances/migration/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceMigrationDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceMigrationDto>>> GetBalanceMigration(long id, CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetLeaveBalanceMigrationAsync(id, cancellationToken);
        return Ok(ApiResponse<LeaveBalanceMigrationDto>.CreateSuccess(result));
    }

    [HttpPost("balances/migration/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceMigrationDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "post")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceMigrationDto>>> PostBalanceMigration(long id, CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.PostLeaveBalanceMigrationAsync(id, user, cancellationToken);
        return Ok(ApiResponse<LeaveBalanceMigrationDto>.CreateSuccess(result, "Leave-balance migration posted."));
    }

    [HttpPost("balances/migration/{id:long}/reverse")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceMigrationDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "AdminOnly")]
    [HrPermission("hr-leaves", "reverse")]
    public async Task<ActionResult<ApiResponse<LeaveBalanceMigrationDto>>> ReverseBalanceMigration(long id, CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.ReverseLeaveBalanceMigrationAsync(id, user, cancellationToken);
        return Ok(ApiResponse<LeaveBalanceMigrationDto>.CreateSuccess(result, "Leave-balance migration reversed."));
    }

    [HttpGet("policies")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>>> GetLeavePolicies(
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetLeavePoliciesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>.CreateSuccess(result, "Leave policies retrieved successfully."));
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(ApiResponse<HrLeaveSettingsDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-leaves", "view")]
    public async Task<ActionResult<ApiResponse<HrLeaveSettingsDto>>> GetLeaveSettings(CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetHrLeaveSettingsAsync(cancellationToken);
        return Ok(ApiResponse<HrLeaveSettingsDto>.CreateSuccess(result, "HR leave settings retrieved successfully."));
    }

    [HttpPut("settings")]
    [ProducesResponseType(typeof(ApiResponse<HrLeaveSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "configure")]
    public async Task<ActionResult<ApiResponse<HrLeaveSettingsDto>>> UpdateLeaveSettings(
        [FromBody] UpdateHrLeaveSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.UpdateHrLeaveSettingsAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<HrLeaveSettingsDto>.CreateSuccess(result, "HR leave settings updated successfully."));
    }

    [HttpPost("policies")]
    [ProducesResponseType(typeof(ApiResponse<LeavePolicyDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-leaves", "create")]
    public async Task<ActionResult<ApiResponse<LeavePolicyDetailsDto>>> CreateOrUpdateLeavePolicy(
        [FromBody] CreateLeavePolicyDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.CreateOrUpdateLeavePolicyAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeavePolicyDetailsDto>.CreateSuccess(result, "Leave policy saved successfully."));
    }
}

