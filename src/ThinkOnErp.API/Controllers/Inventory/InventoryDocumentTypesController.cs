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

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Inventory Document Types API: Returns active stock movement document types (Vouchers 300, Transfers 350).
/// </summary>
[ApiController]
[Route("api/inventory/document-types")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class InventoryDocumentTypesController : ControllerBase
{
    private readonly ITrxTypeService _typeService;

    public InventoryDocumentTypesController(ITrxTypeService typeService)
    {
        _typeService = typeService;
    }

    /// <summary>
    /// Retrieves active inventory movement document types and their active transaction types.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TrxDocTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<TrxDocTypeDto>>>> GetInventoryDocumentTypes(CancellationToken ct)
    {
        var result = await _typeService.GetDocTypesByModuleAsync("INVENTORY", activeOnly: true, excludePos: false, ct);
        return StatusCode(result.StatusCode, result);
    }
}
