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

namespace ThinkOnErp.API.Controllers.Sales;

/// <summary>
/// Sales Document Types API: Returns active commercial sales document types (Invoices 100, Returns 150, Quotations 400).
/// Automatically excludes POS sales (1004).
/// </summary>
[ApiController]
[Route("api/sales/document-types")]
[ApiExplorerSettings(GroupName = ApiCategories.Sales)]
[TenantScoped]
[Authorize]
public sealed class SalesDocumentTypesController : ControllerBase
{
    private readonly ITrxTypeService _typeService;

    public SalesDocumentTypesController(ITrxTypeService typeService)
    {
        _typeService = typeService;
    }

    /// <summary>
    /// Retrieves active sales document types and their active transaction types (excluding POS sales 1004).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TrxDocTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<TrxDocTypeDto>>>> GetSalesDocumentTypes(CancellationToken ct)
    {
        var result = await _typeService.GetDocTypesByModuleAsync("SALES", activeOnly: true, excludePos: true, ct);
        return StatusCode(result.StatusCode, result);
    }
}
