using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Balances;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/balances")]
[TenantScoped]
[Authorize]
public class AccountBalancesController : ControllerBase
{
    private readonly IGlAccountBalanceService _balanceService;
    private readonly ILogger<AccountBalancesController> _logger;

    public AccountBalancesController(
        IGlAccountBalanceService balanceService,
        ILogger<AccountBalancesController> logger)
    {
        _balanceService = balanceService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves pre-aggregated account balances with optional filtering by account, branch, fiscal year, and period.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlAccountBalanceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GlAccountBalanceDto>>>> GetBalances(
        [FromQuery] AccountBalanceFilterDto filter,
        CancellationToken cancellationToken)
    {
        var balances = await _balanceService.GetBalancesAsync(filter, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GlAccountBalanceDto>>.CreateSuccess(
            balances,
            ResponseCodes.AccountBalancesRetrieved,
            200));
    }

    /// <summary>
    /// Generates high-performance Trial Balance (ميزان المراجعة) for a specific date range or period range.
    /// </summary>
    [HttpGet("trial-balance")]
    [ProducesResponseType(typeof(ApiResponse<TrialBalanceReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TrialBalanceReportDto>>> GetTrialBalance(
        [FromQuery] long fiscalYearId,
        [FromQuery] long fromPeriodId,
        [FromQuery] long toPeriodId,
        [FromQuery] long? branchId,
        CancellationToken cancellationToken)
    {
        var report = await _balanceService.GetTrialBalanceAsync(fiscalYearId, fromPeriodId, toPeriodId, branchId, cancellationToken);
        return Ok(ApiResponse<TrialBalanceReportDto>.CreateSuccess(
            report,
            ResponseCodes.TrialBalanceGenerated,
            200));
    }

    /// <summary>
    /// Recalculates and rebuilds all account balances for a fiscal year from posted journal vouchers.
    /// </summary>
    [HttpPost("recalculate")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> RecalculateBalances(
        [FromQuery] long fiscalYearId,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        await _balanceService.RecalculateBalancesAsync(fiscalYearId, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            true,
            ResponseCodes.OperationSuccessful,
            200));
    }
}
