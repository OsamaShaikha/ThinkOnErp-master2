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
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> CreateAdjustment(
        [FromBody] CreatePayrollAdjustmentDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _adjustmentService.CreateAdjustmentAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment created successfully."));
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> ApproveAdjustment(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _adjustmentService.ApproveAdjustmentAsync(id, user, cancellationToken);
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment approved successfully."));
    }

    [HttpPost("{id:long}/reject")]
    [ProducesResponseType(typeof(ApiResponse<PayrollAdjustment>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PayrollAdjustment>>> RejectAdjustment(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _adjustmentService.RejectAdjustmentAsync(id, user, cancellationToken);
        return Ok(ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment rejected successfully."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>), StatusCodes.Status200OK)]
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
    public async Task<ActionResult<ApiResponse<bool>>> DeleteAdjustment(
        long id,
        CancellationToken cancellationToken)
    {
        await _adjustmentService.DeleteAdjustmentAsync(id, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(true, "Adjustment deleted successfully."));
    }
}
