using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Types;
using ThinkOnErp.Application.Services.Inventory;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Global Document Definitions and Administration API.
/// Used for settings (prefixes, toggling active states) and reporting (e.g., item movement ledger).
/// </summary>
[ApiController]
[Route("api/documents")]
[ApiExplorerSettings(GroupName = ApiCategories.System)]
[TenantScoped]
[Authorize]
public sealed class GlobalDocumentsController : ControllerBase
{
    private readonly ITrxTypeService _typeService;

    public GlobalDocumentsController(ITrxTypeService typeService)
    {
        _typeService = typeService;
    }

    /// <summary>
    /// Retrieves all document types and their transaction types, optionally filtered by moduleCode (SALES, PURCHASING, INVENTORY).
    /// Returns both active and inactive definitions for administration and reporting.
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<List<TrxDocTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<TrxDocTypeDto>>>> GetDocumentTypes(
        [FromQuery] string? moduleCode,
        CancellationToken ct)
    {
        var result = await _typeService.GetDocTypesAdminFilteredAsync(moduleCode, ct);
        return StatusCode(result.StatusCode, result);
    }
}
