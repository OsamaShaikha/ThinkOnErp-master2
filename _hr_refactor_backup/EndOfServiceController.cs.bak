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
[Route("api/hr/end-of-service")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class EndOfServiceController : ControllerBase
{
    private readonly IEndOfServiceService _eosService;
    private readonly IHrAccountingHandoffService _handoffs;
    private readonly ILogger<EndOfServiceController> _logger;

    public EndOfServiceController(
        IEndOfServiceService eosService,
        IHrAccountingHandoffService handoffs,
        ILogger<EndOfServiceController> logger)
    {
        _eosService = eosService;
        _handoffs = handoffs;
        _logger = logger;
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

        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _eosService.CalculateSettlementAsync(request, user, tenant.CompanyId, cancellationToken);
        return Ok(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(result, "End of service settlement calculated successfully in DRAFT status."));
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "approve")]
    public async Task<ActionResult<ApiResponse<EndOfServiceSettlementDto>>> ApproveSettlement(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _eosService.ApproveSettlementAsync(id, user, cancellationToken);
        return Ok(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(result, "End of service settlement approved successfully and employee status updated to TERMINATED."));
    }

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

        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _eosService.CancelSettlementAsync(id, request.Reason, user, cancellationToken);
        return Ok(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(result, "End of service settlement cancelled successfully."));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "view")]
    public async Task<ActionResult<ApiResponse<EndOfServiceSettlementDto>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _eosService.GetByIdAsync(id, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<EndOfServiceSettlementDto>.CreateFailure("End of service settlement not found.", statusCode: 404));

        return Ok(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(result, "End of service settlement retrieved successfully."));
    }

    [HttpGet("employee/{employeeCode}")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceSettlementDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "view")]
    public async Task<ActionResult<ApiResponse<EndOfServiceSettlementDto>>> GetByEmployeeCode(
        string employeeCode,
        CancellationToken cancellationToken)
    {
        var result = await _eosService.GetByEmployeeCodeAsync(employeeCode, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<EndOfServiceSettlementDto>.CreateFailure($"No active end of service settlement found for employee '{employeeCode}'.", statusCode: 404));

        return Ok(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(result, "End of service settlement retrieved successfully."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-end-of-service", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>>> GetList(
        [FromQuery] long companyId,
        [FromQuery] long? branchId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _eosService.GetListAsync(companyId, branchId, status, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>.CreateSuccess(result, "End of service settlements retrieved successfully."));
    }

    [HttpPost("{id:long}/payout-request")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "approve")]
    public async Task<IActionResult> SubmitPayoutRequest(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var handoff = await _handoffs.CreateApprovedFundingHandoffAsync("EOS", id, user, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new
        {
            HandoffId = handoff.Id,
            HandoffReference = handoff.HandoffReference,
            DeliveryStatus = handoff.DeliveryStatus,
            Operation = handoff.Operation,
            Amount = handoff.Lines.FirstOrDefault()?.Amount ?? 0m,
            Currency = handoff.Lines.FirstOrDefault()?.CurrencyCode
        }, "End of service settlement payout handoff created successfully."));
    }

    [HttpGet("{id:long}/handoff-status")]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceHandoffStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EndOfServiceHandoffStatusDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-end-of-service", "view")]
    public async Task<ActionResult<ApiResponse<EndOfServiceHandoffStatusDto>>> GetHandoffStatus(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _eosService.GetHandoffStatusAsync(id, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<EndOfServiceHandoffStatusDto>.CreateFailure("No accounting handoff found for this settlement.", statusCode: StatusCodes.Status404NotFound));

        return Ok(ApiResponse<EndOfServiceHandoffStatusDto>.CreateSuccess(result, "Handoff status retrieved successfully."));
    }
}

