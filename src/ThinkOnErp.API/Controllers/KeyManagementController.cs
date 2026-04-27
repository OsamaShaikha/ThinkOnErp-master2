using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for managing encryption and signing keys.
/// Provides endpoints for key rotation, validation, and status monitoring.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class KeyManagementController : ControllerBase
{
    private readonly IKeyManagementService _keyManagementService;
    private readonly ILogger<KeyManagementController> _logger;

    public KeyManagementController(
        IKeyManagementService keyManagementService,
        ILogger<KeyManagementController> logger)
    {
        _keyManagementService = keyManagementService ?? throw new ArgumentNullException(nameof(keyManagementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current key rotation status and metadata.
    /// </summary>
    /// <returns>Key rotation metadata including versions, last rotation dates, and next rotation dates</returns>
    [HttpGet("rotation-status")]
    [ProducesResponseType(typeof(KeyRotationStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRotationStatus()
    {
        try
        {
            var metadata = await _keyManagementService.GetKeyRotationMetadataAsync();

            var response = new KeyRotationStatusResponse
            {
                EncryptionKeyVersion = metadata.EncryptionKeyVersion,
                SigningKeyVersion = metadata.SigningKeyVersion,
                EncryptionKeyLastRotated = metadata.EncryptionKeyLastRotated,
                SigningKeyLastRotated = metadata.SigningKeyLastRotated,
                EncryptionKeyNextRotation = metadata.EncryptionKeyNextRotation,
                SigningKeyNextRotation = metadata.SigningKeyNextRotation,
                EncryptionKeyRotationOverdue = metadata.EncryptionKeyRotationOverdue,
                SigningKeyRotationOverdue = metadata.SigningKeyRotationOverdue,
                StorageProvider = metadata.StorageProvider
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve key rotation status");
            return StatusCode(500, new { error = "Failed to retrieve key rotation status", details = ex.Message });
        }
    }

    /// <summary>
    /// Rotates the encryption key and returns the new version identifier.
    /// </summary>
    /// <returns>New encryption key version</returns>
    [HttpPost("rotate-encryption-key")]
    [ProducesResponseType(typeof(KeyRotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RotateEncryptionKey()
    {
        try
        {
            _logger.LogInformation("Encryption key rotation requested by user");

            var newVersion = await _keyManagementService.RotateEncryptionKeyAsync();

            var response = new KeyRotationResponse
            {
                NewVersion = newVersion,
                RotatedAt = DateTime.UtcNow,
                KeyType = "Encryption"
            };

            _logger.LogInformation("Encryption key rotated successfully. New version: {Version}", newVersion);

            return Ok(response);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Key rotation not supported by current provider");
            return BadRequest(new { error = "Key rotation not supported", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate encryption key");
            return StatusCode(500, new { error = "Failed to rotate encryption key", details = ex.Message });
        }
    }

    /// <summary>
    /// Rotates the signing key and returns the new version identifier.
    /// </summary>
    /// <returns>New signing key version</returns>
    [HttpPost("rotate-signing-key")]
    [ProducesResponseType(typeof(KeyRotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RotateSigningKey()
    {
        try
        {
            _logger.LogInformation("Signing key rotation requested by user");

            var newVersion = await _keyManagementService.RotateSigningKeyAsync();

            var response = new KeyRotationResponse
            {
                NewVersion = newVersion,
                RotatedAt = DateTime.UtcNow,
                KeyType = "Signing"
            };

            _logger.LogInformation("Signing key rotated successfully. New version: {Version}", newVersion);

            return Ok(response);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Key rotation not supported by current provider");
            return BadRequest(new { error = "Key rotation not supported", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate signing key");
            return StatusCode(500, new { error = "Failed to rotate signing key", details = ex.Message });
        }
    }

    /// <summary>
    /// Validates that all required keys are configured and accessible.
    /// </summary>
    /// <returns>Validation result indicating whether keys are valid</returns>
    [HttpGet("validate")]
    [ProducesResponseType(typeof(KeyValidationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateKeys()
    {
        try
        {
            var isValid = await _keyManagementService.ValidateKeysAsync();

            var metadata = await _keyManagementService.GetKeyRotationMetadataAsync();

            var response = new KeyValidationResponse
            {
                Valid = isValid,
                EncryptionKeyValid = isValid, // Simplified - could be more granular
                SigningKeyValid = isValid,
                Provider = metadata.StorageProvider,
                ValidationTimestamp = DateTime.UtcNow
            };

            if (!isValid)
            {
                _logger.LogWarning("Key validation failed");
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate keys");
            return StatusCode(500, new { error = "Failed to validate keys", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current encryption key version.
    /// </summary>
    /// <returns>Current encryption key version identifier</returns>
    [HttpGet("encryption-key-version")]
    [ProducesResponseType(typeof(KeyVersionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEncryptionKeyVersion()
    {
        try
        {
            var version = await _keyManagementService.GetCurrentEncryptionKeyVersionAsync();

            var response = new KeyVersionResponse
            {
                Version = version,
                KeyType = "Encryption"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve encryption key version");
            return StatusCode(500, new { error = "Failed to retrieve encryption key version", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current signing key version.
    /// </summary>
    /// <returns>Current signing key version identifier</returns>
    [HttpGet("signing-key-version")]
    [ProducesResponseType(typeof(KeyVersionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSigningKeyVersion()
    {
        try
        {
            var version = await _keyManagementService.GetCurrentSigningKeyVersionAsync();

            var response = new KeyVersionResponse
            {
                Version = version,
                KeyType = "Signing"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve signing key version");
            return StatusCode(500, new { error = "Failed to retrieve signing key version", details = ex.Message });
        }
    }
}

#region Response DTOs

public class KeyRotationStatusResponse
{
    public string EncryptionKeyVersion { get; set; } = string.Empty;
    public string SigningKeyVersion { get; set; } = string.Empty;
    public DateTime? EncryptionKeyLastRotated { get; set; }
    public DateTime? SigningKeyLastRotated { get; set; }
    public DateTime? EncryptionKeyNextRotation { get; set; }
    public DateTime? SigningKeyNextRotation { get; set; }
    public bool EncryptionKeyRotationOverdue { get; set; }
    public bool SigningKeyRotationOverdue { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
}

public class KeyRotationResponse
{
    public string NewVersion { get; set; } = string.Empty;
    public DateTime RotatedAt { get; set; }
    public string KeyType { get; set; } = string.Empty;
}

public class KeyValidationResponse
{
    public bool Valid { get; set; }
    public bool EncryptionKeyValid { get; set; }
    public bool SigningKeyValid { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTime ValidationTimestamp { get; set; }
}

public class KeyVersionResponse
{
    public string Version { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
}

#endregion
