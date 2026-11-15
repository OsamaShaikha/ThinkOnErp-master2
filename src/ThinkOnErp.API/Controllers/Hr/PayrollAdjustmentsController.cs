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
[Route("api/hr/adjustments")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class PayrollAdjustmentsController : ControllerBase
{
    private readonly IPayrollAdjustmentService _adjustmentService;
    private readonly ILogger<PayrollAdjustmentsController> _logger;

    public PayrollAdjustmentsController(
        IPayrollAdjustmentService adjustmentService,
        ILogger<PayrollAdjustmentsController> logger)
    {
        _adjustmentService = adjustmentService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "create")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> CreateAdjustment(
        [FromBody] CreatePayrollAdjustmentDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _adjustmentService.CreateAdjustmentAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment created successfully."));
    }

    [HttpPost("overtime")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "create")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> CreateOvertimeAdjustment(
        [FromBody] CreateOvertimePayrollAdjustmentDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _adjustmentService.CreateOvertimeAdjustmentAsync(dto, User.Identity?.Name ?? "SYSTEM", cancellationToken);
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Overtime adjustment submitted for review."));
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "approve")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> ApproveAdjustment(
        long id,
        [FromBody] PayrollAdjustmentDecisionDto decision,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _adjustmentService.DecideAdjustmentAsync(id, true, decision.Reason, user, decision.BranchId, cancellationToken);
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment approved successfully."));
    }

    [HttpPost("{id:long}/reject")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "reject")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> RejectAdjustment(
        long id,
        [FromBody] PayrollAdjustmentDecisionDto decision,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _adjustmentService.DecideAdjustmentAsync(id, false, decision.Reason, user, decision.BranchId, cancellationToken);
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment rejected successfully."));
    }

    [HttpPost("{id:long}/post")]
    [HrPermission("hr-payroll-adjustments", "post")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> PostAdjustment(
        long id,
        [FromBody] PayrollAdjustmentDecisionDto decision,
        CancellationToken cancellationToken)
    {
        var result = await _adjustmentService.DecideAdjustmentAsync(id, true, decision.Reason, User.Identity?.Name ?? "SYSTEM", decision.BranchId, cancellationToken);
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment posted successfully."));
    }

    [HttpPost("{id:long}/unpost")]
    [HrPermission("hr-payroll-adjustments", "reverse")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> UnpostAdjustment(
        long id,
        [FromQuery] long? branchId,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var result = await _adjustmentService.UnpostAdjustmentAsync(id, User.Identity?.Name ?? "SYSTEM", branchId, reason, cancellationToken);
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment returned to pending."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>>> GetAdjustments(
        [FromQuery] long companyId,
        [FromQuery] string? employeeCode,
        [FromQuery] string? payPeriod,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _adjustmentService.GetAdjustmentsAsync(companyId, employeeCode, payPeriod, status, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>.CreateSuccess(result, "Adjustments retrieved successfully."));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "view")]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> GetAdjustmentById(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _adjustmentService.GetAdjustmentByIdAsync(id, cancellationToken);
        if (result == null) return NotFound(ApiResponse<PayrollAdjustment>.CreateFailure("Adjustment not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment retrieved successfully."));
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-adjustments", "delete")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteAdjustment(
        long id,
        CancellationToken cancellationToken)
    {
        await _adjustmentService.DeleteAdjustmentAsync(id, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(true, "Adjustment deleted successfully."));
    }
}
