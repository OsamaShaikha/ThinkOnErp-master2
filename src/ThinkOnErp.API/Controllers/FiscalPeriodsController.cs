using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.FiscalPeriods;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[TenantScoped]
[Authorize]
public class FiscalPeriodsController : ControllerBase
{
    private readonly IGlFiscalPeriodService _periodService;
    private readonly ILogger<FiscalPeriodsController> _logger;

    public FiscalPeriodsController(
        IGlFiscalPeriodService periodService,
        ILogger<FiscalPeriodsController> logger)
    {
        _periodService = periodService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all fiscal periods for a specific fiscal year.
    /// </summary>
    [HttpGet("api/accounting/fiscal-years/{fiscalYearId}/periods")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlFiscalPeriodDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GlFiscalPeriodDto>>>> GetPeriodsByFiscalYear(
        long fiscalYearId,
        CancellationToken cancellationToken)
    {
        var periods = await _periodService.GetPeriodsByFiscalYearIdAsync(fiscalYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GlFiscalPeriodDto>>.CreateSuccess(
            periods,
            "تم استرجاع الفترات المالية بنجاح",
            200));
    }

    /// <summary>
    /// Automatically generates 12 monthly fiscal periods (and optional 13th adjustment period) for a fiscal year.
    /// </summary>
    [HttpPost("api/accounting/fiscal-years/{fiscalYearId}/periods/generate")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlFiscalPeriodDto>>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GlFiscalPeriodDto>>>> GeneratePeriods(
        long fiscalYearId,
        [FromBody] GenerateFiscalPeriodsDto? dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var includeAdjustment = dto?.IncludeAdjustmentPeriod ?? true;

        var periods = await _periodService.GeneratePeriodsAsync(fiscalYearId, includeAdjustment, username, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<IReadOnlyList<GlFiscalPeriodDto>>.CreateSuccess(
            periods,
            "تم توليد الفترات المالية بنجاح",
            201));
    }

    /// <summary>
    /// Retrieves a single fiscal period by its ID.
    /// </summary>
    [HttpGet("api/accounting/fiscal-periods/{id}")]
    [ProducesResponseType(typeof(ApiResponse<GlFiscalPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GlFiscalPeriodDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlFiscalPeriodDto>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var period = await _periodService.GetByIdAsync(id, cancellationToken);
        if (period == null)
        {
            return NotFound(ApiResponse<GlFiscalPeriodDto>.CreateFailure(
                $"الفترة المالية رقم ({id}) غير موجودة.",
                statusCode: 404));
        }

        return Ok(ApiResponse<GlFiscalPeriodDto>.CreateSuccess(
            period,
            "تم استرجاع الفترة المالية بنجاح",
            200));
    }

    /// <summary>
    /// Soft-closes a fiscal period (restricts posting to supervisors with reason).
    /// </summary>
    [HttpPost("api/accounting/fiscal-periods/{id}/soft-close")]
    [ProducesResponseType(typeof(ApiResponse<GlFiscalPeriodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GlFiscalPeriodDto>>> SoftClose(
        long id,
        [FromBody] CloseGlFiscalPeriodDto? dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _periodService.SoftClosePeriodAsync(id, username, dto?.Reason, cancellationToken);
        return Ok(ApiResponse<GlFiscalPeriodDto>.CreateSuccess(
            result,
            "تم الإقفال المرن للفترة المالية بنجاح",
            200));
    }

    /// <summary>
    /// Hard-closes a fiscal period (completely locks period from any postings).
    /// </summary>
    [HttpPost("api/accounting/fiscal-periods/{id}/hard-close")]
    [ProducesResponseType(typeof(ApiResponse<GlFiscalPeriodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GlFiscalPeriodDto>>> HardClose(
        long id,
        [FromBody] CloseGlFiscalPeriodDto? dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _periodService.HardClosePeriodAsync(id, username, dto?.Reason, cancellationToken);
        return Ok(ApiResponse<GlFiscalPeriodDto>.CreateSuccess(
            result,
            "تم الإقفال النهائي للفترة المالية بنجاح",
            200));
    }

    /// <summary>
    /// Reopens a previously closed fiscal period.
    /// </summary>
    [HttpPost("api/accounting/fiscal-periods/{id}/reopen")]
    [ProducesResponseType(typeof(ApiResponse<GlFiscalPeriodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GlFiscalPeriodDto>>> Reopen(
        long id,
        [FromBody] CloseGlFiscalPeriodDto? dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _periodService.ReopenPeriodAsync(id, username, dto?.Reason, cancellationToken);
        return Ok(ApiResponse<GlFiscalPeriodDto>.CreateSuccess(
            result,
            "تمت إعادة فتح الفترة المالية بنجاح",
            200));
    }
}
