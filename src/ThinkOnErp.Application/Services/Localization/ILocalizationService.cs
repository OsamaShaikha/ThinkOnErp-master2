namespace ThinkOnErp.Application.Services.Localization;

/// <summary>
/// Service interface for dynamic, database-driven error message and code localization via SYS_CODE.
/// Eliminates hardcoded localized strings in backend services.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Returns the human-readable localized text for a given error code (e.g. 'ERR_INVALID_TAX_NUMBER')
    /// based on the specified language code ('ar', 'en', 'fr', 'tr') or the current HTTP request culture.
    /// </summary>
    string GetMessage(string errorCode, string? languageCode = null);

    /// <summary>
    /// Returns the human-readable localized text for a given error code and explicit language ID (1=AR, 2=EN).
    /// </summary>
    string GetMessage(string errorCode, int languageId);

    /// <summary>
    /// Returns formatted localized text with placeholder substitution.
    /// </summary>
    string GetMessageWithFormat(string errorCode, string? languageCode = null, params object[] args);

    /// <summary>
    /// Resolves an ISO language code ('ar-SA', 'en-US') to the numeric SYS_CODE language ID (1=AR, 2=EN).
    /// </summary>
    int ResolveLanguageId(string? languageCode);

    /// <summary>
    /// Invalidate and refresh in-memory cached translations from SYS_CODE.
    /// </summary>
    void RefreshCache();
}
