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

namespace ThinkOnErp.API.Controllers.Purchasing;

/// <summary>
/// Purchasing Document Types API: Returns active procurement document types (Bills 200, Returns 250, Orders 450).
/// </summary>
[ApiController]
[Route("api/purchasing/document-types")]
[ApiExplorerSettings(GroupName = ApiCategories.Purchasing)]
[TenantScoped]
[Authorize]
public sealed class PurchasingDocumentTypesController : ControllerBase
{
    private readonly ITrxTypeService _typeService;

    public PurchasingDocumentTypesController(ITrxTypeService typeService)
    {
        _typeService = typeService;
    }

    /// <summary>
    /// Retrieves active purchasing document types and their active transaction types.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TrxDocTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<TrxDocTypeDto>>>> GetPurchasingDocumentTypes(CancellationToken ct)
    {
        var result = await _typeService.GetDocTypesByModuleAsync("PURCHASING", activeOnly: true, excludePos: false, ct);
        return StatusCode(result.StatusCode, result);
    }
}
