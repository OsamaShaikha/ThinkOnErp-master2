using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.API.Controllers.Pos;

[ApiController]
[Route("api/pos/reports")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public sealed class PosReportExportsController(IPosReportPdfService export) : ControllerBase
{
    [HttpGet("export/catalog")]
    public IActionResult Catalog([FromQuery] int? language)
    {
        var result = export.GetCatalog(language);
        Response.Headers.CacheControl = "no-store";
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{report}/options")]
    public async Task<IActionResult> Options(string report, [FromQuery] PosReportExportFilterDto filter, CancellationToken ct)
    {
        var result = await export.GetOptionsAsync(report, filter, ct);
        Response.Headers.CacheControl = "no-store";
        if (result.Data != null) Response.Headers.ContentLanguage = result.Data.Language;
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{report}/print-data")]
    public async Task<IActionResult> PrintData(string report, [FromQuery] PosReportExportFilterDto filter, CancellationToken ct)
    {
        var result = await export.GetDataAsync(report, filter, ct);
        Response.Headers.CacheControl = "no-store";
        if (result.Data != null) Response.Headers.ContentLanguage = result.Data.Header.Language;
        return StatusCode(result.StatusCode, result);
    }

    private async Task<IActionResult> Pdf(string report, PosReportExportFilterDto filter, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await export.GetDataAsync(report, filter, ct);
        if (!result.Success || result.Data == null) return StatusCode(result.StatusCode, result);
        var data = result.Data;
        var pdf = await export.RenderAsync(data, ct);
        Response.Headers.ContentLanguage = data.Header.Language;
        var period = data.IsLive ? $"snapshot-{data.Header.IssuedAtUtc:yyyyMMddHHmm}" : $"{data.Header.StartDate:yyyyMMdd}-{data.Header.EndDate:yyyyMMdd}";
        return File(pdf, "application/pdf", $"{data.Code}-{period}-{data.Header.Language}.pdf");
    }

    [HttpGet("sales-by-payment-method/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> PaymentMethodsPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("sales-by-payment-method", filter, ct);

    [HttpGet("sales-by-cashier/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> CashiersPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("sales-by-cashier", filter, ct);

    [HttpGet("sales-by-item/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> ItemsPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("sales-by-item", filter, ct);

    [HttpGet("sales-by-category/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> CategoriesPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("sales-by-category", filter, ct);

    [HttpGet("voids-and-refunds/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> VoidsAndRefundsPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("voids-and-refunds", filter, ct);

    [HttpGet("discounts/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> DiscountsPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("discounts", filter, ct);

    [HttpGet("cash-drawer-variance/pdf")]
    [HttpGet("drawer-variance/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> DrawerVariancePdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("cash-drawer-variance", filter, ct);

    [HttpGet("cash-movements/pdf")]
    [HttpGet("cash-in-out/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> CashMovementsPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("cash-movements", filter, ct);

    [HttpGet("hourly-sales/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> HourlySalesPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("hourly-sales", filter, ct);

    [HttpGet("sales-by-floor-and-table/pdf")]
    [HttpGet("sales-by-table/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> FloorAndTablePdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("sales-by-floor-and-table", filter, ct);

    [HttpGet("sales-by-order-type/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> OrderTypesPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("sales-by-order-type", filter, ct);

    [HttpGet("sales-by-customer/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> CustomersPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("sales-by-customer", filter, ct);

    [HttpGet("tax-summary/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> TaxSummaryPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("tax-summary", filter, ct);

    [HttpGet("open-tables-live/pdf")]
    [HttpGet("open-tables/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> LiveTablesPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("open-tables-live", filter, ct);

    [HttpGet("kitchen-prep-time/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public Task<IActionResult> KitchenPreparationPdf([FromQuery] PosReportExportFilterDto filter, CancellationToken ct) => Pdf("kitchen-prep-time", filter, ct);
}
