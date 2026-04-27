using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Compliance;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for alert management operations.
/// Provides REST API endpoints for managing alert rules, viewing alert history,
/// acknowledging and resolving alerts, and configuring notification channels.
/// All endpoints require admin authorization.
/// </summary>
[ApiController]
[Route("api/alerts")]
[Authorize(Policy = "AdminOnly")]
public class AlertsController : ControllerBase
{
    private readonly IAlertManager _alertManager;
    private readonly ILogger<AlertsController> _logger;

    /// <summary>
    /// Initializes a new instance of the AlertsController class.
    /// </summary>
    /// <param name="alertManager">Alert manager service for alert operations</param>
    /// <param name="logger">Logger for controller operations</param>
    public AlertsController(
        IAlertManager alertManager,
        ILogger<AlertsController> logger)
    {
        _alertManager = alertManager ?? throw new ArgumentNullException(nameof(alertManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Alert Rules Management

    /// <summary>
    /// Get all configured alert rules with pagination.
    /// Returns alert rules with their conditions, thresholds, and notification settings.
    /// </summary>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    /// <returns>Paged list of alert rules</returns>
    /// <response code="200">Returns the paged list of alert rules</response>
    /// <response code="400">Invalid pagination parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertRuleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertRuleDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertRuleDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertRuleDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAlertRules(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1)
            {
                _logger.LogWarning("Invalid page number {PageNumber}", pageNumber);
                return BadRequest(ApiResponse<PagedResult<AlertRuleDto>>.CreateFailure(
                    "Page number must be greater than 0",
                    statusCode: 400));
            }

            if (pageSize < 1 || pageSize > 100)
            {
                _logger.LogWarning("Invalid page size {PageSize}", pageSize);
                return BadRequest(ApiResponse<PagedResult<AlertRuleDto>>.CreateFailure(
                    "Page size must be between 1 and 100",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Retrieving alert rules: Page={PageNumber}, PageSize={PageSize}",
                pageNumber, pageSize);

            var allRules = await _alertManager.GetAlertRulesAsync();
            var allRuleDtos = allRules.Select(MapToAlertRuleDto).ToList();

            // Apply pagination
            var totalCount = allRuleDtos.Count;
            var skip = (pageNumber - 1) * pageSize;
            var pagedRules = allRuleDtos.Skip(skip).Take(pageSize).ToList();

            var pagedResult = new PagedResult<AlertRuleDto>
            {
                Items = pagedRules,
                TotalCount = totalCount,
                Page = pageNumber,
                PageSize = pageSize
            };

            _logger.LogInformation(
                "Retrieved {Count} alert rules (page {Page} of {TotalPages})",
                pagedRules.Count,
                pagedResult.Page,
                pagedResult.TotalPages);

            return Ok(ApiResponse<PagedResult<AlertRuleDto>>.CreateSuccess(
                pagedResult,
                $"Alert rules retrieved successfully ({totalCount} total rules)"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert rules");
            return StatusCode(500, ApiResponse<PagedResult<AlertRuleDto>>.CreateFailure(
                "An error occurred while retrieving alert rules",
                statusCode: 500));
        }
    }

    /// <summary>
    /// Create a new alert rule.
    /// Defines when and how alerts should be triggered based on event type and severity.
    /// </summary>
    /// <param name="createDto">Alert rule creation data</param>
    /// <returns>The created alert rule</returns>
    /// <response code="201">Alert rule created successfully</response>
    /// <response code="400">Invalid alert rule data</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost("rules")]
    [ProducesResponseType(typeof(ApiResponse<AlertRuleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AlertRuleDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AlertRuleDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<AlertRuleDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAlertRule([FromBody] CreateAlertRuleDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<AlertRuleDto>.CreateFailure(
                    "Invalid alert rule data",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Creating alert rule: Name={Name}, EventType={EventType}, Severity={Severity}",
                createDto.Name, createDto.EventType, createDto.SeverityThreshold);

            // Get current user ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(userIdClaim, out var userId))
            {
                return BadRequest(ApiResponse<AlertRuleDto>.CreateFailure(
                    "Invalid user ID in token",
                    statusCode: 400));
            }

            // Map DTO to domain model
            var rule = new AlertRule
            {
                Name = createDto.Name,
                Description = createDto.Description,
                EventType = createDto.EventType,
                SeverityThreshold = createDto.SeverityThreshold,
                Condition = createDto.Condition,
                NotificationChannels = createDto.NotificationChannels,
                EmailRecipients = createDto.EmailRecipients,
                WebhookUrl = createDto.WebhookUrl,
                SmsRecipients = createDto.SmsRecipients,
                IsActive = true,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            // Create the rule
            var createdRule = await _alertManager.CreateAlertRuleAsync(rule);
            var ruleDto = MapToAlertRuleDto(createdRule);

            return CreatedAtAction(
                nameof(GetAlertRules),
                new { id = createdRule.Id },
                ApiResponse<AlertRuleDto>.CreateSuccess(
                    ruleDto,
                    "Alert rule created successfully"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid alert rule data: {Message}", ex.Message);
            return BadRequest(ApiResponse<AlertRuleDto>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert rule");
            return StatusCode(500, ApiResponse<AlertRuleDto>.CreateFailure(
                "An error occurred while creating the alert rule",
                statusCode: 500));
        }
    }

    /// <summary>
    /// Update an existing alert rule.
    /// Allows modification of alert conditions, thresholds, notification channels, and recipients.
    /// </summary>
    /// <param name="id">Alert rule ID</param>
    /// <param name="updateDto">Alert rule update data</param>
    /// <returns>Success response</returns>
    /// <response code="200">Alert rule updated successfully</response>
    /// <response code="400">Invalid alert rule data</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    /// <response code="404">Alert rule not found</response>
    [HttpPut("rules/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAlertRule(long id, [FromBody] UpdateAlertRuleDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Invalid alert rule data",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating alert rule: Id={Id}", id);

            // Get current user ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(userIdClaim, out var userId))
            {
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Invalid user ID in token",
                    statusCode: 400));
            }

            // Get existing rules to find the one to update
            var existingRules = await _alertManager.GetAlertRulesAsync();
            var existingRule = existingRules.FirstOrDefault(r => r.Id == id);

            if (existingRule == null)
            {
                return NotFound(ApiResponse<object>.CreateFailure(
                    $"Alert rule with ID {id} not found",
                    statusCode: 404));
            }

            // Update only provided fields
            if (!string.IsNullOrWhiteSpace(updateDto.Name))
                existingRule.Name = updateDto.Name;
            
            if (!string.IsNullOrWhiteSpace(updateDto.Description))
                existingRule.Description = updateDto.Description;
            
            if (!string.IsNullOrWhiteSpace(updateDto.SeverityThreshold))
                existingRule.SeverityThreshold = updateDto.SeverityThreshold;
            
            if (updateDto.Condition != null)
                existingRule.Condition = updateDto.Condition;
            
            if (!string.IsNullOrWhiteSpace(updateDto.NotificationChannels))
                existingRule.NotificationChannels = updateDto.NotificationChannels;
            
            if (updateDto.EmailRecipients != null)
                existingRule.EmailRecipients = updateDto.EmailRecipients;
            
            if (updateDto.WebhookUrl != null)
                existingRule.WebhookUrl = updateDto.WebhookUrl;
            
            if (updateDto.SmsRecipients != null)
                existingRule.SmsRecipients = updateDto.SmsRecipients;
            
            if (updateDto.IsActive.HasValue)
                existingRule.IsActive = updateDto.IsActive.Value;

            existingRule.ModifiedBy = userId;
            existingRule.ModifiedAt = DateTime.UtcNow;

            // Update the rule
            await _alertManager.UpdateAlertRuleAsync(existingRule);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { id = existingRule.Id },
                "Alert rule updated successfully"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid alert rule data: {Message}", ex.Message);
            return BadRequest(ApiResponse<object>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alert rule {Id}", id);
            return StatusCode(500, ApiResponse<object>.CreateFailure(
                "An error occurred while updating the alert rule",
                statusCode: 500));
        }
    }

    /// <summary>
    /// Delete an alert rule.
    /// Removes the rule from the system and stops triggering alerts based on this rule.
    /// </summary>
    /// <param name="id">Alert rule ID</param>
    /// <returns>Success response</returns>
    /// <response code="200">Alert rule deleted successfully</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    /// <response code="404">Alert rule not found</response>
    [HttpDelete("rules/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAlertRule(long id)
    {
        try
        {
            _logger.LogInformation("Deleting alert rule: Id={Id}", id);

            // Check if rule exists
            var existingRules = await _alertManager.GetAlertRulesAsync();
            var existingRule = existingRules.FirstOrDefault(r => r.Id == id);

            if (existingRule == null)
            {
                return NotFound(ApiResponse<object>.CreateFailure(
                    $"Alert rule with ID {id} not found",
                    statusCode: 404));
            }

            // Delete the rule
            await _alertManager.DeleteAlertRuleAsync(id);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { id },
                "Alert rule deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alert rule {Id}", id);
            return StatusCode(500, ApiResponse<object>.CreateFailure(
                "An error occurred while deleting the alert rule",
                statusCode: 500));
        }
    }

    #endregion

    #region Alert History

    /// <summary>
    /// Get alert history with pagination.
    /// Returns historical alerts that have been triggered, including acknowledgment and resolution status.
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <returns>Paged list of alert history entries</returns>
    /// <response code="200">Returns the paged alert history</response>
    /// <response code="400">Invalid pagination parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertHistoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertHistoryDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertHistoryDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertHistoryDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAlertHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            if (pageNumber < 1)
            {
                return BadRequest(ApiResponse<PagedResult<AlertHistoryDto>>.CreateFailure(
                    "Page number must be greater than 0",
                    statusCode: 400));
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(ApiResponse<PagedResult<AlertHistoryDto>>.CreateFailure(
                    "Page size must be between 1 and 100",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Retrieving alert history: Page={PageNumber}, PageSize={PageSize}",
                pageNumber, pageSize);

            var pagination = new PaginationOptions
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _alertManager.GetAlertHistoryAsync(pagination);

            // Map to DTOs
            var dtoResult = new PagedResult<AlertHistoryDto>
            {
                Items = result.Items.Select(MapToAlertHistoryDto).ToList(),
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };

            return Ok(ApiResponse<PagedResult<AlertHistoryDto>>.CreateSuccess(
                dtoResult,
                "Alert history retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert history");
            return StatusCode(500, ApiResponse<PagedResult<AlertHistoryDto>>.CreateFailure(
                "An error occurred while retrieving alert history",
                statusCode: 500));
        }
    }

    #endregion

    #region Alert Acknowledgment and Resolution

    /// <summary>
    /// Acknowledge an alert.
    /// Indicates that the alert has been reviewed by an administrator.
    /// Updates the alert status and records who acknowledged it and when.
    /// </summary>
    /// <param name="id">Alert ID</param>
    /// <param name="acknowledgeDto">Acknowledgment data (optional notes)</param>
    /// <returns>Success response</returns>
    /// <response code="200">Alert acknowledged successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    /// <response code="404">Alert not found</response>
    [HttpPost("{id}/acknowledge")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcknowledgeAlert(
        long id,
        [FromBody] AcknowledgeAlertDto? acknowledgeDto = null)
    {
        try
        {
            _logger.LogInformation("Acknowledging alert: Id={Id}", id);

            // Get current user ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(userIdClaim, out var userId))
            {
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Invalid user ID in token",
                    statusCode: 400));
            }

            // Acknowledge the alert
            await _alertManager.AcknowledgeAlertAsync(id, userId);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { id, acknowledgedBy = userId, acknowledgedAt = DateTime.UtcNow },
                "Alert acknowledged successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging alert {Id}", id);
            
            // Check if it's a not found scenario
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(ApiResponse<object>.CreateFailure(
                    $"Alert with ID {id} not found",
                    statusCode: 404));
            }

            return StatusCode(500, ApiResponse<object>.CreateFailure(
                "An error occurred while acknowledging the alert",
                statusCode: 500));
        }
    }

    /// <summary>
    /// Resolve an alert.
    /// Indicates that the alert has been addressed and closed.
    /// Updates the alert status to 'Resolved' and records resolution details.
    /// </summary>
    /// <param name="id">Alert ID</param>
    /// <param name="resolveDto">Resolution data including notes</param>
    /// <returns>Success response</returns>
    /// <response code="200">Alert resolved successfully</response>
    /// <response code="400">Invalid request data or missing resolution notes</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    /// <response code="404">Alert not found</response>
    [HttpPost("{id}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveAlert(
        long id,
        [FromBody] ResolveAlertDto resolveDto)
    {
        try
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(resolveDto?.ResolutionNotes))
            {
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Resolution notes are required",
                    statusCode: 400));
            }

            _logger.LogInformation("Resolving alert: Id={Id}", id);

            // Get current user ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(userIdClaim, out var userId))
            {
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Invalid user ID in token",
                    statusCode: 400));
            }

            // Resolve the alert
            await _alertManager.ResolveAlertAsync(id, userId, resolveDto.ResolutionNotes);

            return Ok(ApiResponse<object>.CreateSuccess(
                new 
                { 
                    id, 
                    resolvedBy = userId, 
                    resolvedAt = DateTime.UtcNow,
                    resolutionNotes = resolveDto.ResolutionNotes
                },
                "Alert resolved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert {Id}", id);
            
            // Check if it's a not found scenario
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(ApiResponse<object>.CreateFailure(
                    $"Alert with ID {id} not found",
                    statusCode: 404));
            }

            return StatusCode(500, ApiResponse<object>.CreateFailure(
                "An error occurred while resolving the alert",
                statusCode: 500));
        }
    }

    #endregion

    #region Notification Channel Testing

    /// <summary>
    /// Test email notification channel.
    /// Sends a test email alert to verify email configuration.
    /// </summary>
    /// <param name="recipients">Array of email addresses to send test alert to</param>
    /// <returns>Success response</returns>
    /// <response code="200">Test email sent successfully</response>
    /// <response code="400">Invalid email addresses</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost("test/email")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestEmailNotification([FromBody] string[] recipients)
    {
        try
        {
            if (recipients == null || recipients.Length == 0)
            {
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "At least one recipient email address is required",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Testing email notification channel: Recipients={Recipients}",
                string.Join(", ", recipients));

            // Create a test alert
            var testAlert = new Alert
            {
                AlertType = "Test",
                Severity = "Low",
                Title = "Test Email Alert",
                Description = "This is a test email alert to verify email notification configuration.",
                TriggeredAt = DateTime.UtcNow
            };

            // Send test email
            await _alertManager.SendEmailAlertAsync(testAlert, recipients);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { recipients, sentAt = DateTime.UtcNow },
                "Test email sent successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test email notification");
            return StatusCode(500, ApiResponse<object>.CreateFailure(
                "An error occurred while sending test email",
                statusCode: 500));
        }
    }

    /// <summary>
    /// Test webhook notification channel.
    /// Sends a test webhook alert to verify webhook configuration.
    /// </summary>
    /// <param name="webhookUrl">Webhook URL to send test alert to</param>
    /// <returns>Success response</returns>
    /// <response code="200">Test webhook sent successfully</response>
    /// <response code="400">Invalid webhook URL</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost("test/webhook")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestWebhookNotification([FromBody] string webhookUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Webhook URL is required",
                    statusCode: 400));
            }

            _logger.LogInformation("Testing webhook notification channel: Url={Url}", webhookUrl);

            // Create a test alert
            var testAlert = new Alert
            {
                AlertType = "Test",
                Severity = "Low",
                Title = "Test Webhook Alert",
                Description = "This is a test webhook alert to verify webhook notification configuration.",
                TriggeredAt = DateTime.UtcNow
            };

            // Send test webhook
            await _alertManager.SendWebhookAlertAsync(testAlert, webhookUrl);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { webhookUrl, sentAt = DateTime.UtcNow },
                "Test webhook sent successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test webhook notification");
            return StatusCode(500, ApiResponse<object>.CreateFailure(
                "An error occurred while sending test webhook",
                statusCode: 500));
        }
    }

    /// <summary>
    /// Test SMS notification channel.
    /// Sends a test SMS alert to verify SMS configuration.
    /// </summary>
    /// <param name="phoneNumbers">Array of phone numbers to send test alert to (E.164 format)</param>
    /// <returns>Success response</returns>
    /// <response code="200">Test SMS sent successfully</response>
    /// <response code="400">Invalid phone numbers</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost("test/sms")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestSmsNotification([FromBody] string[] phoneNumbers)
    {
        try
        {
            if (phoneNumbers == null || phoneNumbers.Length == 0)
            {
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "At least one phone number is required",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Testing SMS notification channel: PhoneNumbers={PhoneNumbers}",
                string.Join(", ", phoneNumbers));

            // Create a test alert
            var testAlert = new Alert
            {
                AlertType = "Test",
                Severity = "Low",
                Title = "Test SMS Alert",
                Description = "This is a test SMS alert to verify SMS notification configuration.",
                TriggeredAt = DateTime.UtcNow
            };

            // Send test SMS
            await _alertManager.SendSmsAlertAsync(testAlert, phoneNumbers);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { phoneNumbers, sentAt = DateTime.UtcNow },
                "Test SMS sent successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test SMS notification");
            return StatusCode(500, ApiResponse<object>.CreateFailure(
                "An error occurred while sending test SMS",
                statusCode: 500));
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Map AlertRule domain model to AlertRuleDto.
    /// </summary>
    private AlertRuleDto MapToAlertRuleDto(AlertRule rule)
    {
        return new AlertRuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            EventType = rule.EventType,
            SeverityThreshold = rule.SeverityThreshold,
            Condition = rule.Condition,
            NotificationChannels = rule.NotificationChannels,
            EmailRecipients = rule.EmailRecipients,
            WebhookUrl = rule.WebhookUrl,
            SmsRecipients = rule.SmsRecipients,
            IsActive = rule.IsActive,
            CreatedAt = rule.CreatedAt,
            ModifiedAt = rule.ModifiedAt,
            CreatedBy = rule.CreatedBy,
            ModifiedBy = rule.ModifiedBy
        };
    }

    /// <summary>
    /// Map AlertHistory domain model to AlertHistoryDto.
    /// </summary>
    private AlertHistoryDto MapToAlertHistoryDto(AlertHistory history)
    {
        return new AlertHistoryDto
        {
            Id = history.Id,
            RuleId = history.RuleId,
            RuleName = history.RuleName,
            AlertType = history.AlertType,
            Severity = history.Severity,
            Title = history.Title,
            Description = history.Description,
            CorrelationId = history.CorrelationId,
            TriggeredAt = history.TriggeredAt,
            AcknowledgedAt = history.AcknowledgedAt,
            AcknowledgedByUsername = history.AcknowledgedByUsername,
            ResolvedAt = history.ResolvedAt,
            ResolvedByUsername = history.ResolvedByUsername,
            ResolutionNotes = history.ResolutionNotes,
            NotificationChannels = history.NotificationChannels,
            NotificationSuccess = history.NotificationSuccess,
            Metadata = history.Metadata
        };
    }

    #endregion
}
