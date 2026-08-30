using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Banking;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/bank-reconciliations")]
[TenantScoped]
[Authorize]
public class BankReconciliationController : ControllerBase
{
    private readonly IBankingService _bankingService;
    private readonly ILogger<BankReconciliationController> _logger;

    public BankReconciliationController(IBankingService bankingService, ILogger<BankReconciliationController> logger)
    {
        _bankingService = bankingService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BankReconciliationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BankReconciliationDto>>>> GetReconciliations(
        [FromQuery] long? bankAccountId,
        [FromQuery] long? fiscalYearId,
        CancellationToken cancellationToken)
    {
        var list = await _bankingService.GetReconciliationsAsync(bankAccountId, fiscalYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BankReconciliationDto>>.CreateSuccess(
            list,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<BankReconciliationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BankReconciliationDto>>> GetReconciliationById(
        long id,
        CancellationToken cancellationToken)
    {
        var rec = await _bankingService.GetReconciliationByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<BankReconciliationDto>.CreateSuccess(
            rec,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<BankReconciliationDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<BankReconciliationDto>>> CreateReconciliation(
        [FromBody] CreateBankReconciliationDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var rec = await _bankingService.CreateReconciliationAsync(dto, username, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<BankReconciliationDto>.CreateSuccess(
            rec,
            ResponseCodes.BankReconciliationCreated,
            201));
    }

    [HttpPost("{id:long}/auto-reconcile")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<AutoReconcileResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AutoReconcileResultDto>>> AutoReconcile(
        long id,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _bankingService.AutoReconcileAsync(id, username, cancellationToken);
        return Ok(ApiResponse<AutoReconcileResultDto>.CreateSuccess(
            result,
            ResponseCodes.BankReconciliationAutoMatched,
            200));
    }

    [HttpPost("{id:long}/manual-match")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<BankReconciliationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BankReconciliationDto>>> ManualMatch(
        long id,
        [FromBody] ManualMatchLineDto matchDto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _bankingService.ManualMatchAsync(id, matchDto, username, cancellationToken);
        return Ok(ApiResponse<BankReconciliationDto>.CreateSuccess(
            result,
            ResponseCodes.OperationSuccessful,
            200));
    }

    [HttpGet("{id:long}/statement-report")]
    [ProducesResponseType(typeof(ApiResponse<BankReconciliationStatementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BankReconciliationStatementDto>>> GetStatementReport(
        long id,
        CancellationToken cancellationToken)
    {
        var report = await _bankingService.GetReconciliationStatementReportAsync(id, cancellationToken);
        return Ok(ApiResponse<BankReconciliationStatementDto>.CreateSuccess(
            report,
            ResponseCodes.DataRetrieved,
            200));
    }
}
