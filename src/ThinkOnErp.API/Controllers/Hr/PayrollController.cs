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
[Route("api/hr/payroll")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class PayrollController : ControllerBase
{
    private readonly IPayrollService _payrollService;
    private readonly IPayrollExplanationService _explanationService;
    private readonly ILogger<PayrollController> _logger;

    public PayrollController(
        IPayrollService payrollService,
        IPayrollExplanationService explanationService,
        ILogger<PayrollController> logger)
    {
        _payrollService = payrollService;
        _explanationService = explanationService;
        _logger = logger;
    }

    [HttpPost("calculate")]
    [ProducesResponseType(typeof(ApiResponse<PayrollCalculationResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>> CalculatePayroll(
        [FromBody] CreatePayrollRunDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _payrollService.CreateAndCalculatePayrollRunAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "Payroll run calculated successfully."));
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<PayrollRun>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PayrollRun>>> ApprovePayroll(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _payrollService.ApprovePayrollRunAsync(id, user, cancellationToken);
        return Ok(ApiResponse<PayrollRun>.CreateSuccess(result, "Payroll run approved successfully."));
    }

    [HttpPost("{id:long}/post-to-gl")]
    [ProducesResponseType(typeof(ApiResponse<PostGlVoucherResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PostGlVoucherResultDto>>> PostToGl(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _payrollService.PostPayrollToGlAsync(id, user, cancellationToken);
        return Ok(ApiResponse<PostGlVoucherResultDto>.CreateSuccess(result, "Payroll successfully posted to General Ledger."));
    }

    [HttpPost("{id:long}/reverse-gl")]
    [ProducesResponseType(typeof(ApiResponse<PayrollRun>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PayrollRun>>> ReverseGl(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _payrollService.ReversePayrollGlVoucherAsync(id, user, cancellationToken);
        return Ok(ApiResponse<PayrollRun>.CreateSuccess(result, "Payroll GL voucher reversed successfully."));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollCalculationResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>> GetPayrollRun(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _payrollService.GetPayrollRunByIdAsync(id, cancellationToken);
        if (result == null) return NotFound(ApiResponse<PayrollCalculationResultDto>.CreateFailure("Payroll run not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "Payroll run retrieved successfully."));
    }

    [HttpGet("run-lines/{lineId:long}/explanation")]
    [ProducesResponseType(typeof(ApiResponse<PayrollExplanationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PayrollExplanationDto>>> GetCalculationExplanation(
        long lineId,
        CancellationToken cancellationToken)
    {
        var result = await _explanationService.GetExplanationAsync(lineId, cancellationToken);
        if (result == null) return NotFound(ApiResponse<PayrollExplanationDto>.CreateFailure("Calculation explanation snapshot not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PayrollExplanationDto>.CreateSuccess(result, "Calculation explanation retrieved successfully."));
    }

    [HttpGet("periods")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollPeriod>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PayrollPeriod>>>> GetPeriods(
        [FromQuery] long companyId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var result = await _payrollService.GetPeriodsAsync(companyId, year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollPeriod>>.CreateSuccess(result, "Payroll periods retrieved successfully."));
    }

    [HttpPost("periods")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriod>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PayrollPeriod>>> CreatePeriod(
        [FromBody] PayrollPeriod period,
        CancellationToken cancellationToken)
    {
        period.CreationUser = User.Identity?.Name ?? "SYSTEM";
        period.CreationDate = DateTime.UtcNow;
        var result = await _payrollService.CreatePeriodAsync(period, cancellationToken);
        return Ok(ApiResponse<PayrollPeriod>.CreateSuccess(result, "Payroll period created successfully."));
    }
}
