using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for entity translation persistence and lookups.
/// </summary>
public interface ITranslationRepository
{
    /// <summary>
    /// Gets all translations for a specific entity.
    /// </summary>
    Task<IReadOnlyList<SysEntityTranslation>> GetTranslationsAsync(
        string entityType, 
        long entityId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single translation for a specific entity field and language.
    /// </summary>
    Task<string?> GetTranslationAsync(
        string entityType, 
        long entityId, 
        string fieldName, 
        string langCode, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets translations in bulk for a list of entity IDs for a specific language and field.
    /// Returns a dictionary mapping EntityId -> Translated Text.
    /// </summary>
    Task<Dictionary<long, string>> GetTranslationsForEntitiesAsync(
        string entityType, 
        IEnumerable<long> entityIds, 
        string fieldName, 
        string langCode, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a batch of translations for a specific entity.
    /// </summary>
    Task SaveTranslationsAsync(
        string entityType, 
        long entityId, 
        IEnumerable<(string LangCode, string FieldName, string TranslationText)> translations, 
        string username, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all translations for a specific entity.
    /// </summary>
    Task DeleteTranslationsAsync(
        string entityType, 
        long entityId, 
        CancellationToken cancellationToken = default);
}
