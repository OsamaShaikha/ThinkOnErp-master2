using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.AccountStatement;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Provides Account Statement (كشف حساب) financial reporting endpoints for General Ledger accounts.
/// Calculates opening balances, chronological ledger transactions, running balances, and closing totals.
/// </summary>
[ApiController]
[Route("api/accounting/account-statement")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Accounting)]
[TenantScoped]
[Authorize]
public sealed class AccountStatementController : ControllerBase
{
    private readonly IAccountStatementService _statementService;
    private readonly ILogger<AccountStatementController> _logger;

    public AccountStatementController(
        IAccountStatementService statementService,
        ILogger<AccountStatementController> logger)
    {
        _statementService = statementService ?? throw new ArgumentNullException(nameof(statementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a detailed Account Statement (كشف حساب تفصيلي) for a specific account.
    /// </summary>
    /// <param name="request">Filter criteria including AccountCode, FromDate, ToDate, BranchId, FiscalYearId, CostCenterCode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AccountStatementReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccountStatementReportDto>>> GetAccountStatement(
        [FromQuery] AccountStatementRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccountCode))
        {
            return BadRequest(ApiResponse<object>.CreateFailure(
                "رقم الحساب مطلوب لإنشاء كشف الحساب.",
                statusCode: 400));
        }

        var report = await _statementService.GetAccountStatementAsync(request, cancellationToken);

        return Ok(ApiResponse<AccountStatementReportDto>.CreateSuccess(
            report,
            $"تم استخراج كشف حساب الحساب {report.AccountCode} ({report.AccountNameAr}) بنجاح."));
    }

    /// <summary>
    /// Generates an Account Statement Summary (كشف حساب إجمالي / ميزان الحركات) across accounts.
    /// </summary>
    /// <param name="request">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccountStatementSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccountStatementSummaryDto>>>> GetAccountsSummary(
        [FromQuery] AccountStatementRequestDto request,
        CancellationToken cancellationToken)
    {
        var summaryList = await _statementService.GetAccountsSummaryAsync(request, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<AccountStatementSummaryDto>>.CreateSuccess(
            summaryList,
            $"تم استخراج التقرير الإجمالي لعدد {summaryList.Count} حساب بنجاح."));
    }
}
