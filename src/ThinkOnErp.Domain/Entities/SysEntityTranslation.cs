using System;

namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Centralized entity for dynamic, multi-language translations across the system (Items, Accounts, Warehouses, Categories, etc.).
/// </summary>
public sealed class SysEntityTranslation
{
    public long Id { get; set; }

    /// <summary>
    /// The entity type code (e.g. "ITEM", "ACCOUNT", "WAREHOUSE", "CATEGORY", "SYS_CODE").
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The primary ID / numeric code of the entity.
    /// </summary>
    public long EntityId { get; set; }

    /// <summary>
    /// The field being translated (e.g. "Name", "Description", "Notes").
    /// </summary>
    public string FieldName { get; set; } = "Name";

    /// <summary>
    /// ISO Language code (e.g. "ar", "fr", "es", "tr", "de", "zh").
    /// </summary>
    public string LangCode { get; set; } = string.Empty;

    /// <summary>
    /// The translated text value.
    /// </summary>
    public string TranslationText { get; set; } = string.Empty;

    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
