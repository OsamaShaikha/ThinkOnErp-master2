using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Tax;
using ThinkOnErp.Application.Services.Accounting.Tax;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Enterprise Tax Engine (محرك الضرائب)
/// Manages Tax Categories, Rates, Groups, Instant Tax Calculation, VAT Returns, and ZATCA E-Invoicing QR codes.
/// </summary>
[ApiController]
[Route("api/accounting/tax")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Accounting)]
[TenantScoped]
[Authorize]
public sealed class TaxController : ControllerBase
{
    private readonly ITaxEngineService _taxEngineService;
    private readonly ILogger<TaxController> _logger;

    public TaxController(
        ITaxEngineService taxEngineService,
        ILogger<TaxController> logger)
    {
        _taxEngineService = taxEngineService ?? throw new ArgumentNullException(nameof(taxEngineService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Tax Calculation Engine
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates itemized and total taxes (inclusive/exclusive), discounts, and suggests GL entries.
    /// </summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(ApiResponse<TaxCalculationResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaxCalculationResultDto>>> CalculateTax(
        [FromBody] TaxCalculationRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _taxEngineService.CalculateTaxAsync(request, cancellationToken);
        return Ok(ApiResponse<TaxCalculationResultDto>.CreateSuccess(result, ResponseCodes.OperationSuccessful));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Multi-Country VAT / Tax Return Declarations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all supported official country Tax Declaration templates (GCC/ZATCA 16, Jordan Sales Tax, Egypt Form 10, UK/EU 9, Global Generic).
    /// </summary>
    [HttpGet("declaration/templates")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TaxDeclarationTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TaxDeclarationTemplateDto>>>> GetDeclarationTemplates(
        CancellationToken cancellationToken)
    {
        var templates = await _taxEngineService.GetAvailableDeclarationTemplatesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TaxDeclarationTemplateDto>>.CreateSuccess(templates, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Gets metadata and box structures for a specific Tax Declaration template by its code.
    /// </summary>
    [HttpGet("declaration/templates/{templateCode}")]
    [ProducesResponseType(typeof(ApiResponse<TaxDeclarationTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TaxDeclarationTemplateDto>>> GetDeclarationTemplateByCode(
        [FromRoute] string templateCode,
        CancellationToken cancellationToken)
    {
        var template = await _taxEngineService.GetDeclarationTemplateAsync(templateCode, cancellationToken);
        if (template == null)
        {
            return NotFound(ApiResponse<TaxDeclarationTemplateDto>.CreateFailure(ErrorCodes.TaxCategoryNotFound, statusCode: StatusCodes.Status404NotFound));
        }
        return Ok(ApiResponse<TaxDeclarationTemplateDto>.CreateSuccess(template, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Generates the country-specific Tax Return / VAT declaration report based on chosen template or default jurisdiction.
    /// Supports GCC_ZATCA_16, JO_SALES_TAX, EG_VAT_FORM_10, UK_EU_VAT_9, and GLOBAL_GENERIC.
    /// </summary>
    [HttpGet("vat-declaration")]
    [HttpGet("declaration")]
    [ProducesResponseType(typeof(ApiResponse<VatDeclarationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VatDeclarationDto>>> GetVatDeclaration(
        [FromQuery] VatDeclarationFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _taxEngineService.GetVatDeclarationAsync(filter, cancellationToken);
        return Ok(ApiResponse<VatDeclarationDto>.CreateSuccess(result, ResponseCodes.TaxReturnCalculated));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. ZATCA QR Code Generator
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates standard TLV Base64 QR code string for ZATCA E-Invoicing (Phase 1 & Phase 2).
    /// </summary>
    [HttpPost("zatca-qr")]
    [ProducesResponseType(typeof(ApiResponse<ZatcaQrResultDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<ZatcaQrResultDto>> GenerateZatcaQr(
        [FromBody] ZatcaQrRequestDto request)
    {
        var result = _taxEngineService.GenerateZatcaQrCode(request);
        return Ok(ApiResponse<ZatcaQrResultDto>.CreateSuccess(result, ResponseCodes.OperationSuccessful));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Tax Categories CRUD
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all tax categories (VAT, WHT, EXCISE...).
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TaxCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TaxCategoryDto>>>> GetCategories(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var list = await _taxEngineService.GetCategoriesAsync(includeInactive, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TaxCategoryDto>>.CreateSuccess(list, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Gets a tax category by ID.
    /// </summary>
    [HttpGet("categories/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TaxCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TaxCategoryDto>>> GetCategoryById(
        long id,
        CancellationToken cancellationToken)
    {
        var category = await _taxEngineService.GetCategoryByIdAsync(id, cancellationToken);
        if (category == null)
            return NotFound(ApiResponse<object>.CreateFailure(ErrorCodes.TaxCategoryNotFound, statusCode: 404));

        return Ok(ApiResponse<TaxCategoryDto>.CreateSuccess(category, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Creates a new tax category.
    /// </summary>
    [HttpPost("categories")]
    [ProducesResponseType(typeof(ApiResponse<TaxCategoryDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<TaxCategoryDto>>> CreateCategory(
        [FromBody] CreateTaxCategoryDto dto,
        CancellationToken cancellationToken)
    {
        var username = GetCurrentUsername();
        var result = await _taxEngineService.CreateCategoryAsync(dto, username, cancellationToken);
        return CreatedAtAction(nameof(GetCategoryById), new { id = result.Id },
            ApiResponse<TaxCategoryDto>.CreateSuccess(result, ResponseCodes.TaxCategoryCreated));
    }

    /// <summary>
    /// Updates an existing tax category.
    /// </summary>
    [HttpPut("categories/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TaxCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaxCategoryDto>>> UpdateCategory(
        long id,
        [FromBody] UpdateTaxCategoryDto dto,
        CancellationToken cancellationToken)
    {
        var username = GetCurrentUsername();
        var result = await _taxEngineService.UpdateCategoryAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<TaxCategoryDto>.CreateSuccess(result, ResponseCodes.RecordUpdated));
    }

    /// <summary>
    /// Deletes a tax category.
    /// </summary>
    [HttpDelete("categories/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCategory(
        long id,
        CancellationToken cancellationToken)
    {
        await _taxEngineService.DeleteCategoryAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(null, ResponseCodes.RecordDeleted));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Tax Rates CRUD
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all tax rates (e.g. VAT_15, VAT_5, VAT_0, VAT_EXEMPT, VAT_OUT_OF_SCOPE).
    /// </summary>
    [HttpGet("rates")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TaxRateDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TaxRateDto>>>> GetRates(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var list = await _taxEngineService.GetRatesAsync(includeInactive, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TaxRateDto>>.CreateSuccess(list, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Gets a tax rate by ID.
    /// </summary>
    [HttpGet("rates/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TaxRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TaxRateDto>>> GetRateById(
        long id,
        CancellationToken cancellationToken)
    {
        var rate = await _taxEngineService.GetRateByIdAsync(id, cancellationToken);
        if (rate == null)
            return NotFound(ApiResponse<object>.CreateFailure(ErrorCodes.TaxRateNotFound, statusCode: 404));

        return Ok(ApiResponse<TaxRateDto>.CreateSuccess(rate, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Gets a tax rate by code (e.g. VAT_15).
    /// </summary>
    [HttpGet("rates/code/{code}")]
    [ProducesResponseType(typeof(ApiResponse<TaxRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TaxRateDto>>> GetRateByCode(
        string code,
        CancellationToken cancellationToken)
    {
        var rate = await _taxEngineService.GetRateByCodeAsync(code, cancellationToken);
        if (rate == null)
            return NotFound(ApiResponse<object>.CreateFailure(ErrorCodes.TaxRateNotFound, statusCode: 404));

        return Ok(ApiResponse<TaxRateDto>.CreateSuccess(rate, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Creates a new tax rate.
    /// </summary>
    [HttpPost("rates")]
    [ProducesResponseType(typeof(ApiResponse<TaxRateDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<TaxRateDto>>> CreateRate(
        [FromBody] CreateTaxRateDto dto,
        CancellationToken cancellationToken)
    {
        var username = GetCurrentUsername();
        var result = await _taxEngineService.CreateRateAsync(dto, username, cancellationToken);
        return CreatedAtAction(nameof(GetRateById), new { id = result.Id },
            ApiResponse<TaxRateDto>.CreateSuccess(result, ResponseCodes.TaxRateCreated));
    }

    /// <summary>
    /// Updates an existing tax rate.
    /// </summary>
    [HttpPut("rates/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TaxRateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaxRateDto>>> UpdateRate(
        long id,
        [FromBody] UpdateTaxRateDto dto,
        CancellationToken cancellationToken)
    {
        var username = GetCurrentUsername();
        var result = await _taxEngineService.UpdateRateAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<TaxRateDto>.CreateSuccess(result, ResponseCodes.TaxRateUpdated));
    }

    /// <summary>
    /// Deletes a tax rate.
    /// </summary>
    [HttpDelete("rates/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRate(
        long id,
        CancellationToken cancellationToken)
    {
        await _taxEngineService.DeleteRateAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(null, ResponseCodes.RecordDeleted));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Tax Groups CRUD
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all tax groups.
    /// </summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TaxGroupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TaxGroupDto>>>> GetGroups(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var list = await _taxEngineService.GetGroupsAsync(includeInactive, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TaxGroupDto>>.CreateSuccess(list, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Gets a tax group by ID.
    /// </summary>
    [HttpGet("groups/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TaxGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TaxGroupDto>>> GetGroupById(
        long id,
        CancellationToken cancellationToken)
    {
        var group = await _taxEngineService.GetGroupByIdAsync(id, cancellationToken);
        if (group == null)
            return NotFound(ApiResponse<object>.CreateFailure(ErrorCodes.TaxGroupNotFound, statusCode: 404));

        return Ok(ApiResponse<TaxGroupDto>.CreateSuccess(group, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Creates a new tax group.
    /// </summary>
    [HttpPost("groups")]
    [ProducesResponseType(typeof(ApiResponse<TaxGroupDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<TaxGroupDto>>> CreateGroup(
        [FromBody] CreateTaxGroupDto dto,
        CancellationToken cancellationToken)
    {
        var username = GetCurrentUsername();
        var result = await _taxEngineService.CreateGroupAsync(dto, username, cancellationToken);
        return CreatedAtAction(nameof(GetGroupById), new { id = result.Id },
            ApiResponse<TaxGroupDto>.CreateSuccess(result, ResponseCodes.TaxGroupCreated));
    }

    /// <summary>
    /// Deletes a tax group.
    /// </summary>
    [HttpDelete("groups/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteGroup(
        long id,
        CancellationToken cancellationToken)
    {
        await _taxEngineService.DeleteGroupAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(null, ResponseCodes.RecordDeleted));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper
    // ─────────────────────────────────────────────────────────────────────────

    private string GetCurrentUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
               ?? User.FindFirst("userName")?.Value
               ?? User.FindFirst("username")?.Value
               ?? "SYSTEM";
    }
}
