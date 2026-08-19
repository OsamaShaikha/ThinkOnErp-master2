using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Reports;
using ThinkOnErp.Application.DTOs.Accounting.Subledger;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/reports")]
[TenantScoped]
[Authorize]
public class FinancialReportsController : ControllerBase
{
    private readonly IFinancialReportsService _reportsService;
    private readonly ILogger<FinancialReportsController> _logger;

    public FinancialReportsController(IFinancialReportsService reportsService, ILogger<FinancialReportsController> logger)
    {
        _reportsService = reportsService;
        _logger = logger;
    }

    [HttpGet("gl-statement")]
    [ProducesResponseType(typeof(ApiResponse<GlStatementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GlStatementDto>>> GetGeneralLedgerStatement(
        [FromQuery] GlStatementFilterDto filter,
        CancellationToken cancellationToken)
    {
        if (filter.FromDate == default) filter.FromDate = new DateTime(DateTime.UtcNow.Year, 1, 1);
        if (filter.ToDate == default) filter.ToDate = DateTime.UtcNow;

        var statement = await _reportsService.GetGeneralLedgerStatementAsync(filter, cancellationToken);
        return Ok(ApiResponse<GlStatementDto>.CreateSuccess(
            statement,
            $"تم استخراج كشف الحساب العام لحساب ({filter.AccountCode}) بنجاح",
            200));
    }

    [HttpGet("income-statement")]
    [ProducesResponseType(typeof(ApiResponse<IncomeStatementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IncomeStatementDto>>> GetIncomeStatement(
        [FromQuery] FinancialStatementFilterDto filter,
        CancellationToken cancellationToken)
    {
        var incomeStatement = await _reportsService.GetIncomeStatementAsync(filter, cancellationToken);
        return Ok(ApiResponse<IncomeStatementDto>.CreateSuccess(
            incomeStatement,
            "تم استخراج قائمة الدخل (الأرباح والخسائر) بنجاح",
            200));
    }

    [HttpGet("balance-sheet")]
    [ProducesResponseType(typeof(ApiResponse<BalanceSheetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BalanceSheetDto>>> GetBalanceSheet(
        [FromQuery] FinancialStatementFilterDto filter,
        CancellationToken cancellationToken)
    {
        var balanceSheet = await _reportsService.GetBalanceSheetAsync(filter, cancellationToken);
        return Ok(ApiResponse<BalanceSheetDto>.CreateSuccess(
            balanceSheet,
            "تم استخراج الميزانية العمومية (قائمة المركز المالي) بنجاح",
            200));
    }

    [HttpGet("customer-statement/{customerCode}")]
    [ProducesResponseType(typeof(ApiResponse<StatementOfAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StatementOfAccountDto>>> GetCustomerStatement(
        string customerCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var from = fromDate ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
        var to = toDate ?? DateTime.UtcNow;
        var statement = await _reportsService.GetCustomerStatementAsync(customerCode, from, to, cancellationToken);
        return Ok(ApiResponse<StatementOfAccountDto>.CreateSuccess(
            statement,
            "تم استخراج كشف حساب العميل بنجاح",
            200));
    }

    [HttpGet("vendor-statement/{vendorCode}")]
    [ProducesResponseType(typeof(ApiResponse<StatementOfAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StatementOfAccountDto>>> GetVendorStatement(
        string vendorCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var from = fromDate ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
        var to = toDate ?? DateTime.UtcNow;
        var statement = await _reportsService.GetVendorStatementAsync(vendorCode, from, to, cancellationToken);
        return Ok(ApiResponse<StatementOfAccountDto>.CreateSuccess(
            statement,
            "تم استخراج كشف حساب المورد بنجاح",
            200));
    }

    [HttpGet("ar-aging")]
    [ProducesResponseType(typeof(ApiResponse<AgingReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AgingReportDto>>> GetArAging(
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var report = await _reportsService.GetArAgingReportAsync(date, cancellationToken);
        return Ok(ApiResponse<AgingReportDto>.CreateSuccess(
            report,
            "تم استخراج تقرير أعمار ديون العملاء بنجاح",
            200));
    }

    [HttpGet("ap-aging")]
    [ProducesResponseType(typeof(ApiResponse<AgingReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AgingReportDto>>> GetApAging(
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var report = await _reportsService.GetApAgingReportAsync(date, cancellationToken);
        return Ok(ApiResponse<AgingReportDto>.CreateSuccess(
            report,
            "تم استخراج تقرير أعمار ديون الموردين بنجاح",
            200));
    }
}
