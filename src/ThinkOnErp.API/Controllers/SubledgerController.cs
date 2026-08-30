using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Subledger;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/subledger")]
[TenantScoped]
[Authorize]
public class SubledgerController : ControllerBase
{
    private readonly ISubledgerService _subledgerService;
    private readonly ILogger<SubledgerController> _logger;

    public SubledgerController(ISubledgerService subledgerService, ILogger<SubledgerController> logger)
    {
        _subledgerService = subledgerService;
        _logger = logger;
    }

    [HttpGet("ar/transactions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubledgerTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubledgerTransactionDto>>>> GetArTransactions(
        [FromQuery] string? customerCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool onlyOpen,
        CancellationToken cancellationToken)
    {
        var transactions = await _subledgerService.GetArTransactionsAsync(customerCode, fromDate, toDate, onlyOpen, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SubledgerTransactionDto>>.CreateSuccess(
            transactions,
            ResponseCodes.SubledgerTransactionsRetrieved,
            200));
    }

    [HttpGet("ap/transactions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubledgerTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubledgerTransactionDto>>>> GetApTransactions(
        [FromQuery] string? vendorCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool onlyOpen,
        CancellationToken cancellationToken)
    {
        var transactions = await _subledgerService.GetApTransactionsAsync(vendorCode, fromDate, toDate, onlyOpen, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SubledgerTransactionDto>>.CreateSuccess(
            transactions,
            ResponseCodes.SubledgerTransactionsRetrieved,
            200));
    }

    [HttpGet("ar/open-invoices/{customerCode}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OpenInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OpenInvoiceDto>>>> GetArOpenInvoices(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var invoices = await _subledgerService.GetArOpenInvoicesAsync(customerCode, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OpenInvoiceDto>>.CreateSuccess(
            invoices,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpGet("ap/open-bills/{vendorCode}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OpenInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OpenInvoiceDto>>>> GetApOpenBills(
        string vendorCode,
        CancellationToken cancellationToken)
    {
        var bills = await _subledgerService.GetApOpenBillsAsync(vendorCode, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OpenInvoiceDto>>.CreateSuccess(
            bills,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpGet("ar/open-payments/{customerCode}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OpenInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OpenInvoiceDto>>>> GetArOpenPayments(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var payments = await _subledgerService.GetArOpenPaymentsAsync(customerCode, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OpenInvoiceDto>>.CreateSuccess(
            payments,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpGet("ap/open-payments/{vendorCode}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OpenInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OpenInvoiceDto>>>> GetApOpenPayments(
        string vendorCode,
        CancellationToken cancellationToken)
    {
        var payments = await _subledgerService.GetApOpenPaymentsAsync(vendorCode, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OpenInvoiceDto>>.CreateSuccess(
            payments,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpPost("ar/apply")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<CashApplicationResultDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CashApplicationResultDto>>>> ApplyArCash(
        [FromBody] ApplyCashDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var results = await _subledgerService.ApplyArCashAsync(dto, username, cancellationToken);
        return Ok(ApiResponse<List<CashApplicationResultDto>>.CreateSuccess(
            results,
            ResponseCodes.CashApplied,
            200));
    }

    [HttpPost("ap/apply")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<CashApplicationResultDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CashApplicationResultDto>>>> ApplyApCash(
        [FromBody] ApplyCashDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var results = await _subledgerService.ApplyApCashAsync(dto, username, cancellationToken);
        return Ok(ApiResponse<List<CashApplicationResultDto>>.CreateSuccess(
            results,
            ResponseCodes.CashApplied,
            200));
    }

    [HttpPost("ar/auto-apply")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<CashApplicationResultDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CashApplicationResultDto>>>> AutoApplyArCash(
        [FromBody] AutoApplyCashDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var results = await _subledgerService.AutoApplyArCashAsync(dto, username, cancellationToken);
        return Ok(ApiResponse<List<CashApplicationResultDto>>.CreateSuccess(
            results,
            ResponseCodes.CashApplied,
            200));
    }

    [HttpPost("ap/auto-apply")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<CashApplicationResultDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CashApplicationResultDto>>>> AutoApplyApCash(
        [FromBody] AutoApplyCashDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var results = await _subledgerService.AutoApplyApCashAsync(dto, username, cancellationToken);
        return Ok(ApiResponse<List<CashApplicationResultDto>>.CreateSuccess(
            results,
            ResponseCodes.CashApplied,
            200));
    }

    [HttpDelete("ar/applications/{applicationId:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> UnapplyArCash(
        long applicationId,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _subledgerService.UnapplyArCashAsync(applicationId, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            ResponseCodes.CashUnapplied,
            200));
    }

    [HttpDelete("ap/applications/{applicationId:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> UnapplyApCash(
        long applicationId,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _subledgerService.UnapplyApCashAsync(applicationId, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            ResponseCodes.CashUnapplied,
            200));
    }

    [HttpGet("ar/applications/invoice/{invoiceId:long}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CashApplicationDetailDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashApplicationDetailDto>>>> GetArApplicationsByInvoice(
        long invoiceId,
        CancellationToken cancellationToken)
    {
        var details = await _subledgerService.GetArApplicationsByInvoiceAsync(invoiceId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CashApplicationDetailDto>>.CreateSuccess(
            details,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpGet("ar/applications/payment/{paymentId:long}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CashApplicationDetailDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashApplicationDetailDto>>>> GetArApplicationsByPayment(
        long paymentId,
        CancellationToken cancellationToken)
    {
        var details = await _subledgerService.GetArApplicationsByPaymentAsync(paymentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CashApplicationDetailDto>>.CreateSuccess(
            details,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpGet("ap/applications/bill/{billId:long}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CashApplicationDetailDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashApplicationDetailDto>>>> GetApApplicationsByBill(
        long billId,
        CancellationToken cancellationToken)
    {
        var details = await _subledgerService.GetApApplicationsByBillAsync(billId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CashApplicationDetailDto>>.CreateSuccess(
            details,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpGet("ap/applications/payment/{paymentId:long}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CashApplicationDetailDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashApplicationDetailDto>>>> GetApApplicationsByPayment(
        long paymentId,
        CancellationToken cancellationToken)
    {
        var details = await _subledgerService.GetApApplicationsByPaymentAsync(paymentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CashApplicationDetailDto>>.CreateSuccess(
            details,
            ResponseCodes.DataRetrieved,
            200));
    }

    [HttpGet("ar/statement/{customerCode}")]
    [ProducesResponseType(typeof(ApiResponse<StatementOfAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StatementOfAccountDto>>> GetCustomerStatement(
        string customerCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var from = fromDate ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
        var to = toDate ?? DateTime.UtcNow;
        var statement = await _subledgerService.GetCustomerStatementAsync(customerCode, from, to, cancellationToken);
        return Ok(ApiResponse<StatementOfAccountDto>.CreateSuccess(
            statement,
            ResponseCodes.AccountStatementGenerated,
            200));
    }

    [HttpGet("ap/statement/{vendorCode}")]
    [ProducesResponseType(typeof(ApiResponse<StatementOfAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StatementOfAccountDto>>> GetVendorStatement(
        string vendorCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var from = fromDate ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
        var to = toDate ?? DateTime.UtcNow;
        var statement = await _subledgerService.GetVendorStatementAsync(vendorCode, from, to, cancellationToken);
        return Ok(ApiResponse<StatementOfAccountDto>.CreateSuccess(
            statement,
            ResponseCodes.AccountStatementGenerated,
            200));
    }

    [HttpGet("ar/aging")]
    [ProducesResponseType(typeof(ApiResponse<AgingReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AgingReportDto>>> GetArAging(
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var report = await _subledgerService.GetArAgingReportAsync(date, cancellationToken);
        return Ok(ApiResponse<AgingReportDto>.CreateSuccess(
            report,
            ResponseCodes.AgingReportGenerated,
            200));
    }

    [HttpGet("ap/aging")]
    [ProducesResponseType(typeof(ApiResponse<AgingReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AgingReportDto>>> GetApAging(
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var report = await _subledgerService.GetApAgingReportAsync(date, cancellationToken);
        return Ok(ApiResponse<AgingReportDto>.CreateSuccess(
            report,
            ResponseCodes.AgingReportGenerated,
            200));
    }

    [HttpGet("reconciliation/ar")]
    [ProducesResponseType(typeof(ApiResponse<SubledgerReconciliationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SubledgerReconciliationDto>>> ReconcileAr(
        [FromQuery] long fiscalYearId,
        CancellationToken cancellationToken)
    {
        var result = await _subledgerService.ReconcileArAsync(fiscalYearId, cancellationToken);
        return Ok(ApiResponse<SubledgerReconciliationDto>.CreateSuccess(
            result,
            ResponseCodes.SubledgerReconciled,
            200));
    }

    [HttpGet("reconciliation/ap")]
    [ProducesResponseType(typeof(ApiResponse<SubledgerReconciliationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SubledgerReconciliationDto>>> ReconcileAp(
        [FromQuery] long fiscalYearId,
        CancellationToken cancellationToken)
    {
        var result = await _subledgerService.ReconcileApAsync(fiscalYearId, cancellationToken);
        return Ok(ApiResponse<SubledgerReconciliationDto>.CreateSuccess(
            result,
            ResponseCodes.SubledgerReconciled,
            200));
    }
}
