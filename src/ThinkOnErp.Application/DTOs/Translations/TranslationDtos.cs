using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Translations;

/// <summary>
/// Individual translation entry for creating or updating an entity's localized text.
/// </summary>
public sealed class EntityTranslationDto
{
    /// <summary>
    /// ISO Language code (e.g. "ar", "fr", "es", "tr", "de").
    /// </summary>
    public string Lang { get; set; } = string.Empty;

    /// <summary>
    /// Field name being translated (defaults to "Name" if omitted).
    /// </summary>
    public string Field { get; set; } = "Name";

    /// <summary>
    /// The translated text value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Bulk save request payload for updating translations of an entity.
/// </summary>
public sealed class SaveEntityTranslationsRequestDto
{
    public List<EntityTranslationDto> Translations { get; set; } = new();
}

/// <summary>
/// Response DTO containing all registered translations for an entity.
/// </summary>
public sealed class EntityTranslationsResponseDto
{
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    
    /// <summary>
    /// Dictionary mapping FieldName -> (Dictionary of LangCode -> TranslatedText).
    /// Example: { "Name": { "ar": "...", "fr": "..." }, "Description": { "ar": "..." } }
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Translations { get; set; } = new();
}
