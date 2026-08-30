using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Banking;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/cash-registers")]
[TenantScoped]
[Authorize]
public class CashRegistersController : ControllerBase
{
    private readonly IBankingService _bankingService;
    private readonly ILogger<CashRegistersController> _logger;

    public CashRegistersController(IBankingService bankingService, ILogger<CashRegistersController> logger)
    {
        _bankingService = bankingService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CashRegisterDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashRegisterDto>>>> GetCashRegisters(
        [FromQuery] long? branchId,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        var list = await _bankingService.GetCashRegistersAsync(branchId, activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CashRegisterDto>>.CreateSuccess(
            list,
            ResponseCodes.CashRegistersRetrieved,
            200));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CashRegisterDto>>> GetCashRegisterById(
        long id,
        CancellationToken cancellationToken)
    {
        var reg = await _bankingService.GetCashRegisterByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<CashRegisterDto>.CreateSuccess(
            reg,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<CashRegisterDto>>> CreateCashRegister(
        [FromBody] CreateCashRegisterDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var reg = await _bankingService.CreateCashRegisterAsync(dto, username, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CashRegisterDto>.CreateSuccess(
            reg,
            ResponseCodes.CashRegisterCreated,
            201));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CashRegisterDto>>> UpdateCashRegister(
        long id,
        [FromBody] UpdateCashRegisterDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var reg = await _bankingService.UpdateCashRegisterAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<CashRegisterDto>.CreateSuccess(
            reg,
            ResponseCodes.CashRegisterUpdated,
            200));
    }
}
