using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Pdc;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/pdc")]
[TenantScoped]
[Authorize]
public class PdcController : ControllerBase
{
    private readonly IPdcService _pdcService;
    private readonly ILogger<PdcController> _logger;

    public PdcController(IPdcService pdcService, ILogger<PdcController> logger)
    {
        _pdcService = pdcService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PdcRegisterDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PdcRegisterDto>>>> GetCheques(
        [FromQuery] PdcFilterDto filter,
        CancellationToken cancellationToken)
    {
        var cheques = await _pdcService.GetChequesAsync(filter, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PdcRegisterDto>>.CreateSuccess(
            cheques,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PdcRegisterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdcRegisterDto>>> GetChequeById(
        long id,
        CancellationToken cancellationToken)
    {
        var cheque = await _pdcService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<PdcRegisterDto>.CreateSuccess(
            cheque,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PdcRegisterDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<PdcRegisterDto>>> CreatePdc(
        [FromBody] CreatePdcDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var cheque = await _pdcService.CreatePdcAsync(dto, username, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PdcRegisterDto>.CreateSuccess(
            cheque,
            ResponseCodes.PdcIssued,
            201));
    }

    [HttpPost("{id:long}/deposit")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PdcRegisterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdcRegisterDto>>> DepositCheque(
        long id,
        [FromBody] DepositPdcDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _pdcService.DepositChequeAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<PdcRegisterDto>.CreateSuccess(
            result,
            ResponseCodes.PdcDeposited,
            200));
    }

    [HttpPost("{id:long}/clear")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PdcRegisterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdcRegisterDto>>> ClearCheque(
        long id,
        [FromBody] ClearPdcDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _pdcService.ClearChequeAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<PdcRegisterDto>.CreateSuccess(
            result,
            ResponseCodes.PdcCleared,
            200));
    }

    [HttpPost("{id:long}/bounce")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PdcRegisterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdcRegisterDto>>> BounceCheque(
        long id,
        [FromBody] BouncePdcDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _pdcService.BounceChequeAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<PdcRegisterDto>.CreateSuccess(
            result,
            ResponseCodes.PdcBounced,
            200));
    }

    [HttpPost("{id:long}/cancel")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PdcRegisterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdcRegisterDto>>> CancelCheque(
        long id,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _pdcService.CancelChequeAsync(id, username, reason, cancellationToken);
        return Ok(ApiResponse<PdcRegisterDto>.CreateSuccess(
            result,
            ResponseCodes.OperationSuccessful,
            200));
    }

    [HttpGet("upcoming-maturities")]
    [ProducesResponseType(typeof(ApiResponse<UpcomingMaturitySummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UpcomingMaturitySummaryDto>>> GetUpcomingMaturities(
        [FromQuery] long? branchId,
        [FromQuery] int daysAhead = 30,
        CancellationToken cancellationToken = default)
    {
        var summary = await _pdcService.GetUpcomingMaturitiesAsync(branchId, daysAhead, cancellationToken);
        return Ok(ApiResponse<UpcomingMaturitySummaryDto>.CreateSuccess(
            summary,
            ResponseCodes.DataRetrieved,
            200));
    }
}
