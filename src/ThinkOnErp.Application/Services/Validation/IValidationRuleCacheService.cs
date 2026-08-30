using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Services.Validation;

/// <summary>
/// Service interface for retrieving cached dynamic validation rules stored in THINKON_ERP.SYS_FIELD_VALIDATION_RULE.
/// </summary>
public interface IValidationRuleCacheService
{
    /// <summary>
    /// Retrieves active validation rules for a specific entity, with optional country-specific and tenant overrides.
    /// </summary>
    IReadOnlyList<SysFieldValidationRule> GetRulesForEntity(string entityName, string? countryCode = null, long? companyId = null);

    /// <summary>
    /// Asynchronously retrieves active validation rules for a specific entity.
    /// </summary>
    Task<IReadOnlyList<SysFieldValidationRule>> GetRulesForEntityAsync(string entityName, string? countryCode = null, long? companyId = null);

    /// <summary>
    /// Invalidate and reload the in-memory validation rules cache from the database.
    /// </summary>
    void RefreshCache();

    /// <summary>
    /// Asynchronously invalidate and reload the in-memory validation rules cache.
    /// </summary>
    Task RefreshCacheAsync();
}
