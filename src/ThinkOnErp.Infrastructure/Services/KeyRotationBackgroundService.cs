using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Background service that automatically rotates encryption and signing keys
/// when they are about to expire. Runs periodic checks and triggers rotation
/// when needed based on configuration.
/// </summary>
public class KeyRotationBackgroundService : BackgroundService
{
    private readonly ILogger<KeyRotationBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly KeyManagementOptions _options;
    private readonly TimeSpan _checkInterval;

    /// <summary>
    /// Initializes a new instance of the KeyRotationBackgroundService.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="serviceProvider">Service provider for creating scoped services</param>
    /// <param name="options">Key management configuration options</param>
    public KeyRotationBackgroundService(
        ILogger<KeyRotationBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<KeyManagementOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Check for key rotation once per day
        _checkInterval = TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Executes the background service, checking for key rotation needs periodically.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableKeyRotation)
        {
            _logger.LogInformation(
                "Automatic key rotation is disabled. Keys must be rotated manually.");
            return;
        }

        _logger.LogInformation(
            "KeyRotationBackgroundService started. Check interval: {Interval}, Rotation period: {RotationDays} days",
            _checkInterval, _options.KeyRotationDays);

        // Wait a bit before first check to allow application to fully start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRotateKeysAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during key rotation check");
            }

            // Wait for next check interval
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Service is stopping
                break;
            }
        }

        _logger.LogInformation("KeyRotationBackgroundService stopped");
    }

    /// <summary>
    /// Checks if keys need rotation and performs rotation if needed.
    /// </summary>
    private async Task CheckAndRotateKeysAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var keyManagementService = scope.ServiceProvider.GetRequiredService<IKeyManagementService>();
        var alertManager = scope.ServiceProvider.GetService<IAlertManager>();

        try
        {
            _logger.LogDebug("Checking if key rotation is needed");

            // Get key rotation metadata to check if rotation is needed
            var metadata = await keyManagementService.GetKeyRotationMetadataAsync(cancellationToken);

            var shouldRotate = metadata.EncryptionKeyRotationOverdue || metadata.SigningKeyRotationOverdue;

            if (!shouldRotate)
            {
                _logger.LogDebug("Key rotation not needed at this time");
                return;
            }

            _logger.LogInformation("Key rotation is needed. Starting automatic key rotation...");

            // Perform key rotation
            var newEncryptionKeyId = await keyManagementService.RotateEncryptionKeyAsync(cancellationToken);
            var newSigningKeyId = await keyManagementService.RotateSigningKeyAsync(cancellationToken);

            _logger.LogInformation(
                "Automatic key rotation completed successfully. " +
                "New encryption key: {EncKeyId}, New signing key: {SigKeyId}",
                newEncryptionKeyId,
                newSigningKeyId);

            // Send success notification
            if (alertManager != null)
            {
                await SendRotationSuccessAlertAsync(
                    alertManager,
                    newEncryptionKeyId,
                    newSigningKeyId);
            }

            // Validate new keys
            var isValid = await keyManagementService.ValidateKeysAsync(cancellationToken);
            if (!isValid)
            {
                _logger.LogWarning("Key validation after rotation failed");

                if (alertManager != null)
                {
                    await SendValidationWarningAlertAsync(alertManager, new List<string> { "Key validation failed after rotation" });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform automatic key rotation");

            // Send failure notification
            if (alertManager != null)
            {
                await SendRotationFailureAlertAsync(alertManager, ex.Message);
            }

            throw;
        }
    }

    /// <summary>
    /// Sends an alert notification when key rotation succeeds.
    /// </summary>
    private async Task SendRotationSuccessAlertAsync(
        IAlertManager alertManager,
        string encryptionKeyId,
        string signingKeyId)
    {
        try
        {
            var alert = new Domain.Models.Alert
            {
                AlertType = "KeyRotationSuccess",
                Severity = "Info",
                Title = "Automatic Key Rotation Completed",
                Description = $"Encryption and signing keys have been rotated successfully. " +
                         $"New encryption key: {encryptionKeyId}, New signing key: {signingKeyId}",
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    EncryptionKeyId = encryptionKeyId,
                    SigningKeyId = signingKeyId,
                    RotationTime = DateTime.UtcNow,
                    Source = "KeyRotationBackgroundService"
                })
            };

            await alertManager.TriggerAlertAsync(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send key rotation success alert");
        }
    }

    /// <summary>
    /// Sends an alert notification when key rotation fails.
    /// </summary>
    private async Task SendRotationFailureAlertAsync(
        IAlertManager alertManager,
        string errorMessage)
    {
        try
        {
            var alert = new Domain.Models.Alert
            {
                AlertType = "KeyRotationFailure",
                Severity = "Critical",
                Title = "Automatic Key Rotation Failed",
                Description = $"Failed to rotate encryption and signing keys. " +
                         $"Manual intervention may be required. Error: {errorMessage}",
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ErrorMessage = errorMessage,
                    FailureTime = DateTime.UtcNow,
                    Source = "KeyRotationBackgroundService"
                })
            };

            await alertManager.TriggerAlertAsync(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send key rotation failure alert");
        }
    }

    /// <summary>
    /// Sends an alert notification when key validation finds issues after rotation.
    /// </summary>
    private async Task SendValidationWarningAlertAsync(
        IAlertManager alertManager,
        List<string> issues)
    {
        try
        {
            var alert = new Domain.Models.Alert
            {
                AlertType = "KeyValidationWarning",
                Severity = "Warning",
                Title = "Key Validation Issues After Rotation",
                Description = $"Key validation found {issues.Count} issue(s) after rotation: " +
                         string.Join("; ", issues),
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Issues = issues,
                    ValidationTime = DateTime.UtcNow,
                    Source = "KeyRotationBackgroundService"
                })
            };

            await alertManager.TriggerAlertAsync(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send key validation warning alert");
        }
    }
}
