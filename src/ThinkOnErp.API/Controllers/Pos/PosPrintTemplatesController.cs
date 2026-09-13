using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Print Templates &amp; Routing API: Receipt templates, tax invoices, and kitchen station print routing.
/// </summary>
[ApiController]
[Route("api/pos/print-templates")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosPrintTemplatesController : ControllerBase
{
    private readonly IPosPrintTemplateService _service;

    public PosPrintTemplatesController(IPosPrintTemplateService service)
    {
        _service = service;
    }

    // =================== TEMPLATES ===================

    /// <summary>
    /// Retrieves all print templates for a branch.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PrintTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PrintTemplateDto>>>> GetTemplates(
        [FromQuery] long branchId,
        [FromQuery] string? templateType,
        CancellationToken ct)
    {
        var result = await _service.GetTemplatesByBranchAsync(branchId, templateType, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a print template by its ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PrintTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PrintTemplateDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PrintTemplateDto>>> GetTemplateById(
        long id,
        CancellationToken ct)
    {
        var result = await _service.GetTemplateByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Creates a new ESC/POS print template.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PrintTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PrintTemplateDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PrintTemplateDto>>> CreateTemplate(
        [FromBody] CreatePrintTemplateDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.CreateTemplateAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates an ESC/POS print template layout.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PrintTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PrintTemplateDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PrintTemplateDto>>> UpdateTemplate(
        long id,
        [FromBody] UpdatePrintTemplateDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.UpdateTemplateAsync(id, dto, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Deletes a print template.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteTemplate(
        long id,
        CancellationToken ct)
    {
        var result = await _service.DeleteTemplateAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    // =================== ROUTING ===================

    /// <summary>
    /// Retrieves all printer routing configurations for a branch.
    /// </summary>
    [HttpGet("routing")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PrinterRoutingDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PrinterRoutingDto>>>> GetRoutings(
        [FromQuery] long branchId,
        CancellationToken ct)
    {
        var result = await _service.GetRoutingsByBranchAsync(branchId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a printer routing configuration by ID.
    /// </summary>
    [HttpGet("routing/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PrinterRoutingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PrinterRoutingDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PrinterRoutingDto>>> GetRoutingById(
        long id,
        CancellationToken ct)
    {
        var result = await _service.GetRoutingByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Creates a printer routing rule directing item categories to kitchen or bar printers.
    /// </summary>
    [HttpPost("routing")]
    [ProducesResponseType(typeof(ApiResponse<PrinterRoutingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PrinterRoutingDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PrinterRoutingDto>>> CreateRouting(
        [FromBody] CreatePrinterRoutingDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.CreateRoutingAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates a printer routing rule.
    /// </summary>
    [HttpPut("routing/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PrinterRoutingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PrinterRoutingDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PrinterRoutingDto>>> UpdateRouting(
        long id,
        [FromBody] UpdatePrinterRoutingDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.UpdateRoutingAsync(id, dto, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Deletes a printer routing rule.
    /// </summary>
    [HttpDelete("routing/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteRouting(
        long id,
        CancellationToken ct)
    {
        var result = await _service.DeleteRoutingAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
