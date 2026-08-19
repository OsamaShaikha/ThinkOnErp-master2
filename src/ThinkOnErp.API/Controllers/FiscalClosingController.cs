using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Closing;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/closing")]
[TenantScoped]
[Authorize]
public class FiscalClosingController : ControllerBase
{
    private readonly IFiscalClosingService _closingService;
    private readonly ILogger<FiscalClosingController> _logger;

    public FiscalClosingController(IFiscalClosingService closingService, ILogger<FiscalClosingController> logger)
    {
        _closingService = closingService;
        _logger = logger;
    }

    [HttpGet("periods/{periodId:long}/validate")]
    [ProducesResponseType(typeof(ApiResponse<PreClosingPeriodValidationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PreClosingPeriodValidationDto>>> ValidatePeriodClosing(
        long periodId,
        CancellationToken cancellationToken)
    {
        var result = await _closingService.ValidatePeriodClosingAsync(periodId, cancellationToken);
        return Ok(ApiResponse<PreClosingPeriodValidationDto>.CreateSuccess(
            result,
            result.IsReadyToClose ? "الفترة المالية جاهزة للإقفال" : "توجد ملاحظات محاسبية تمنع إقفال الفترة",
            200));
    }

    [HttpPost("periods/{periodId:long}/close")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> ClosePeriod(
        long periodId,
        [FromBody] ExecutePeriodCloseRequest request,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _closingService.ClosePeriodAsync(periodId, request, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            "تم إقفال الفترة المالية وقفل ترحيل القيود بنجاح",
            200));
    }

    [HttpPost("periods/{periodId:long}/reopen")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> ReopenPeriod(
        long periodId,
        [FromBody] ExecutePeriodReopenRequest request,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _closingService.ReopenPeriodAsync(periodId, request, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            "تم إعادة فتح الفترة المالية رسمياً بنجاح",
            200));
    }

    [HttpGet("years/{yearId:long}/validate")]
    [ProducesResponseType(typeof(ApiResponse<PreClosingYearValidationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PreClosingYearValidationDto>>> ValidateYearEndClosing(
        long yearId,
        CancellationToken cancellationToken)
    {
        var result = await _closingService.ValidateYearEndClosingAsync(yearId, cancellationToken);
        return Ok(ApiResponse<PreClosingYearValidationDto>.CreateSuccess(
            result,
            result.IsReadyToClose ? "السنة المالية جاهزة للإقفال السنوي وتوليد القيود الختامية" : "توجد متطلبات محاسبية تمنع الإقفال السنوي",
            200));
    }

    [HttpPost("years/{yearId:long}/execute")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<YearEndClosingResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<YearEndClosingResultDto>>> ExecuteYearEndClosing(
        long yearId,
        [FromBody] ExecuteYearEndCloseRequest request,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _closingService.ExecuteYearEndClosingAsync(yearId, request, username, cancellationToken);
        return Ok(ApiResponse<YearEndClosingResultDto>.CreateSuccess(
            result,
            "تم تنفيذ الإقفال السنوي وتصفير الإيرادات والمصروفات وترحيل الأرباح المبقاة وتدوير الأرصدة بنجاح",
            200));
    }
}
