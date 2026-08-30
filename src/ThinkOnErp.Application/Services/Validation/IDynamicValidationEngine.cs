using FluentValidation.Results;

namespace ThinkOnErp.Application.Services.Validation;

/// <summary>
/// Core dynamic validation engine interface for executing metadata-driven validation rules on any object.
/// Resolves rules from THINKON_ERP.SYS_FIELD_VALIDATION_RULE and localized messages from SYS_CODE.
/// </summary>
public interface IDynamicValidationEngine
{
    /// <summary>
    /// Synchronously validates an object against active database rules.
    /// </summary>
    ValidationResult Validate<T>(T instance, string? countryCode = null, long? companyId = null);

    /// <summary>
    /// Asynchronously validates an object against active database rules.
    /// </summary>
    Task<ValidationResult> ValidateAsync<T>(T instance, string? countryCode = null, long? companyId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an object and throws FluentValidation.ValidationException if validation fails.
    /// </summary>
    void ValidateAndThrow<T>(T instance, string? countryCode = null, long? companyId = null);

    /// <summary>
    /// Asynchronously validates an object and throws FluentValidation.ValidationException if validation fails.
    /// </summary>
    Task ValidateAndThrowAsync<T>(T instance, string? countryCode = null, long? companyId = null, CancellationToken cancellationToken = default);
}
