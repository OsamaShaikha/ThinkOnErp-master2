using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Translations;

namespace ThinkOnErp.Application.Services;

/// <summary>
/// Service interface for universal multi-language translation resolution and management.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Saves or updates translations for a specific entity.
    /// </summary>
    Task SaveTranslationsAsync(
        string entityType, 
        long entityId, 
        IEnumerable<EntityTranslationDto> translations, 
        string username, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all translations for an entity formatted for the UI modal.
    /// </summary>
    Task<EntityTranslationsResponseDto> GetTranslationsAsync(
        string entityType, 
        long entityId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the display text for an entity in the requested language (or current user language), falling back to defaultText if not found.
    /// </summary>
    Task<string> ResolveDisplayNameAsync(
        string entityType, 
        long entityId, 
        string fieldName, 
        string defaultText, 
        string? requestedLang = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-populates DisplayName for a list of DTOs efficiently using a single DB query.
    /// </summary>
    Task PopulateDisplayNamesAsync<T>(
        string entityType, 
        IEnumerable<T> items, 
        Func<T, long> idSelector, 
        Func<T, string> defaultSelector, 
        Action<T, string> setter, 
        string fieldName = "Name", 
        string? requestedLang = null, 
        CancellationToken cancellationToken = default);
}
