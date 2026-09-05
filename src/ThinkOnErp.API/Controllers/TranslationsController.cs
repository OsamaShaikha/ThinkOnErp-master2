using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Translations;
using ThinkOnErp.Application.Services;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Universal Dynamic Translation Controller.
/// Manages multi-language translations for any entity (Items, Accounts, Warehouses, Categories, etc.).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = Swagger.ApiCategories.System)]
[Authorize]
public class TranslationsController : ControllerBase
{
    private readonly ITranslationService _translationService;

    public TranslationsController(ITranslationService translationService)
    {
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
    }

    /// <summary>
    /// Gets all registered translations for a specific entity (e.g. ITEM, ACCOUNT, WAREHOUSE).
    /// Used by the frontend translation modal.
    /// </summary>
    /// <param name="entityType">Entity type name (e.g., 'ITEM', 'ACCOUNT', 'WAREHOUSE')</param>
    /// <param name="entityId">ID of the entity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{entityType}/{entityId}")]
    [ProducesResponseType(typeof(ApiResponse<EntityTranslationsResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EntityTranslationsResponseDto>>> GetTranslations(
        string entityType, 
        long entityId, 
        CancellationToken cancellationToken = default)
    {
        var result = await _translationService.GetTranslationsAsync(entityType, entityId, cancellationToken);
        return Ok(ApiResponse<EntityTranslationsResponseDto>.CreateSuccess(result));
    }

    /// <summary>
    /// Saves or updates translations for a specific entity.
    /// </summary>
    /// <param name="entityType">Entity type name (e.g., 'ITEM', 'ACCOUNT', 'WAREHOUSE')</param>
    /// <param name="entityId">ID of the entity</param>
    /// <param name="request">List of translations to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPut("{entityType}/{entityId}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> SaveTranslations(
        string entityType, 
        long entityId, 
        [FromBody] SaveEntityTranslationsRequestDto request, 
        CancellationToken cancellationToken = default)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("userName") ?? "SYSTEM";

        await _translationService.SaveTranslationsAsync(entityType, entityId, request.Translations, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(true, "Translations saved successfully"));
    }

    /// <summary>
    /// Resolves the translated text of a specific field for an entity in the requested language (or caller's default language).
    /// </summary>
    [HttpGet("{entityType}/{entityId}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> Resolve(
        string entityType, 
        long entityId, 
        [FromQuery] string fieldName = "Name", 
        [FromQuery] string defaultText = "", 
        [FromQuery] string? lang = null, 
        CancellationToken cancellationToken = default)
    {
        var resolved = await _translationService.ResolveDisplayNameAsync(entityType, entityId, fieldName, defaultText, lang, cancellationToken);
        return Ok(ApiResponse<string>.CreateSuccess(resolved));
    }
}
