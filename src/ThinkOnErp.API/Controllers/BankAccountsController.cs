using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Banking;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/bank-accounts")]
[TenantScoped]
[Authorize]
public class BankAccountsController : ControllerBase
{
    private readonly IBankingService _bankingService;
    private readonly ILogger<BankAccountsController> _logger;

    public BankAccountsController(IBankingService bankingService, ILogger<BankAccountsController> logger)
    {
        _bankingService = bankingService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BankAccountDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BankAccountDto>>>> GetBankAccounts(
        [FromQuery] long? branchId,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        var list = await _bankingService.GetBankAccountsAsync(branchId, activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BankAccountDto>>.CreateSuccess(
            list,
            "تم استرجاع قائمة الحسابات البنكية بنجاح",
            200));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> GetBankAccountById(
        long id,
        CancellationToken cancellationToken)
    {
        var account = await _bankingService.GetBankAccountByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<BankAccountDto>.CreateSuccess(
            account,
            "تم استرجاع بيانات الحساب البنكي بنجاح",
            200));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> CreateBankAccount(
        [FromBody] CreateBankAccountDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var account = await _bankingService.CreateBankAccountAsync(dto, username, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<BankAccountDto>.CreateSuccess(
            account,
            "تم إنشاء الحساب البنكي بنجاح",
            201));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> UpdateBankAccount(
        long id,
        [FromBody] UpdateBankAccountDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var account = await _bankingService.UpdateBankAccountAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<BankAccountDto>.CreateSuccess(
            account,
            "تم تحديث بيانات الحساب البنكي بنجاح",
            200));
    }
}
