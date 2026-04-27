using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.TicketConfig;
using ThinkOnErp.Application.Features.TicketConfig.Commands.UpdateConfigValue;
using ThinkOnErp.Application.Features.TicketConfig.Commands.UpdateSlaConfig;
using ThinkOnErp.Application.Features.TicketConfig.Queries.GetAllConfigs;
using ThinkOnErp.Application.Features.TicketConfig.Queries.GetSlaConfig;
using ThinkOnErp.Application.Features.TicketConfig.Queries.GetFileAttachmentConfig;
using ThinkOnErp.Application.Features.TicketConfig.Queries.GetNotificationConfig;
using ThinkOnErp.Application.Features.TicketConfig.Queries.GetWorkflowConfig;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for managing ticket system configuration settings.
/// Provides endpoints for retrieving and updating various configuration categories.
/// All endpoints require AdminOnly authorization.
/// </summary>
[ApiController]
[Route("api/configuration")]
[Authorize(Policy = "AdminOnly")]
public class ConfigurationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ConfigurationController> _logger;

    /// <summary>
    /// Initializes a new instance of the ConfigurationController class.
    /// </summary>
    /// <param name="mediator">MediatR instance for sending commands and queries</param>
    /// <param name="logger">Logger for controller operations</param>
    public ConfigurationController(IMediator mediator, ILogger<ConfigurationController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all ticket system configuration settings.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <returns>ApiResponse containing list of all configuration settings</returns>
    /// <response code="200">Returns all configuration settings</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(ApiResponse<List<TicketConfigDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketConfigDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketConfigDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<TicketConfigDto>>>> GetAllConfigurations()
    {
        try
        {
            _logger.LogInformation("Retrieving all ticket configuration settings");

            var query = new GetAllConfigsQuery();
            var configs = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} configuration settings", configs.Count);

            return Ok(ApiResponse<List<TicketConfigDto>>.CreateSuccess(
                configs,
                "Configuration settings retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all configuration settings");
            throw;
        }
    }

    /// <summary>
    /// Retrieves SLA (Service Level Agreement) configuration settings.
    /// Includes priority-based target hours and escalation thresholds.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <returns>ApiResponse containing SLA configuration settings</returns>
    /// <response code="200">Returns SLA configuration settings</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("sla-settings")]
    [ProducesResponseType(typeof(ApiResponse<SlaConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SlaConfigDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SlaConfigDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<SlaConfigDto>>> GetSlaConfiguration()
    {
        try
        {
            _logger.LogInformation("Retrieving SLA configuration settings");

            var query = new GetSlaConfigQuery();
            var config = await _mediator.Send(query);

            _logger.LogInformation("Retrieved SLA configuration successfully");

            return Ok(ApiResponse<SlaConfigDto>.CreateSuccess(
                config,
                "SLA configuration retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SLA configuration");
            throw;
        }
    }

    /// <summary>
    /// Updates SLA (Service Level Agreement) configuration settings in bulk.
    /// Updates all priority-based target hours and escalation threshold in a single operation.
    /// Configuration changes are validated and logged for audit trail.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="dto">DTO containing updated SLA configuration values</param>
    /// <returns>ApiResponse indicating success or failure</returns>
    /// <response code="200">SLA configuration updated successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPut("sla-settings")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateSlaConfiguration(
        [FromBody] SlaConfigDto dto)
    {
        try
        {
            _logger.LogInformation(
                "Updating SLA configuration by user: {User}",
                User.Identity?.Name ?? "unknown");

            var command = new UpdateSlaConfigCommand
            {
                LowPriorityHours = dto.LowPriorityHours,
                MediumPriorityHours = dto.MediumPriorityHours,
                HighPriorityHours = dto.HighPriorityHours,
                CriticalPriorityHours = dto.CriticalPriorityHours,
                EscalationThresholdPercentage = dto.EscalationThresholdPercentage,
                UpdateUser = User.Identity?.Name ?? "system"
            };

            var result = await _mediator.Send(command);

            if (!result)
            {
                _logger.LogWarning("Failed to update SLA configuration");
                return BadRequest(ApiResponse<bool>.CreateFailure(
                    "Failed to update SLA configuration",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "SLA configuration updated successfully by user: {User}",
                User.Identity?.Name ?? "unknown");

            return Ok(ApiResponse<bool>.CreateSuccess(
                result,
                "SLA configuration updated successfully",
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error updating SLA configuration: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<bool>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SLA configuration");
            throw;
        }
    }

    /// <summary>
    /// Retrieves file attachment configuration settings.
    /// Includes maximum file size, attachment count limits, and allowed file types.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <returns>ApiResponse containing file attachment configuration settings</returns>
    /// <response code="200">Returns file attachment configuration settings</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("file-attachments")]
    [ProducesResponseType(typeof(ApiResponse<FileAttachmentConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FileAttachmentConfigDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<FileAttachmentConfigDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<FileAttachmentConfigDto>>> GetFileAttachmentConfiguration()
    {
        try
        {
            _logger.LogInformation("Retrieving file attachment configuration settings");

            var query = new GetFileAttachmentConfigQuery();
            var config = await _mediator.Send(query);

            _logger.LogInformation("Retrieved file attachment configuration successfully");

            return Ok(ApiResponse<FileAttachmentConfigDto>.CreateSuccess(
                config,
                "File attachment configuration retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file attachment configuration");
            throw;
        }
    }

    /// <summary>
    /// Retrieves notification configuration settings.
    /// Includes notification enabled status and email templates.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <returns>ApiResponse containing notification configuration settings</returns>
    /// <response code="200">Returns notification configuration settings</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("notifications")]
    [ProducesResponseType(typeof(ApiResponse<NotificationConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<NotificationConfigDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<NotificationConfigDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<NotificationConfigDto>>> GetNotificationConfiguration()
    {
        try
        {
            _logger.LogInformation("Retrieving notification configuration settings");

            var query = new GetNotificationConfigQuery();
            var config = await _mediator.Send(query);

            _logger.LogInformation("Retrieved notification configuration successfully");

            return Ok(ApiResponse<NotificationConfigDto>.CreateSuccess(
                config,
                "Notification configuration retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification configuration");
            throw;
        }
    }

    /// <summary>
    /// Retrieves workflow configuration settings.
    /// Includes allowed status transitions and auto-close policies.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <returns>ApiResponse containing workflow configuration settings</returns>
    /// <response code="200">Returns workflow configuration settings</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("workflow")]
    [ProducesResponseType(typeof(ApiResponse<WorkflowConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowConfigDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowConfigDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<WorkflowConfigDto>>> GetWorkflowConfiguration()
    {
        try
        {
            _logger.LogInformation("Retrieving workflow configuration settings");

            var query = new GetWorkflowConfigQuery();
            var config = await _mediator.Send(query);

            _logger.LogInformation("Retrieved workflow configuration successfully");

            return Ok(ApiResponse<WorkflowConfigDto>.CreateSuccess(
                config,
                "Workflow configuration retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow configuration");
            throw;
        }
    }

    /// <summary>
    /// Updates a specific configuration value by key.
    /// Validates the configuration key and value, then updates the setting.
    /// Configuration changes are logged for audit trail.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="key">Configuration key to update</param>
    /// <param name="dto">DTO containing the new configuration value</param>
    /// <returns>ApiResponse indicating success or failure</returns>
    /// <response code="200">Configuration updated successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="404">Configuration key not found</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPut("{key}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateConfiguration(
        string key,
        [FromBody] UpdateTicketConfigDto dto)
    {
        try
        {
            _logger.LogInformation(
                "Updating configuration key: {ConfigKey} by user: {User}",
                key,
                User.Identity?.Name ?? "unknown");

            var command = new UpdateConfigValueCommand
            {
                ConfigKey = key,
                ConfigValue = dto.ConfigValue,
                UpdateUser = User.Identity?.Name ?? "system"
            };

            var result = await _mediator.Send(command);

            if (!result)
            {
                _logger.LogWarning("Configuration key not found: {ConfigKey}", key);
                return NotFound(ApiResponse<bool>.CreateFailure(
                    $"Configuration key '{key}' not found",
                    statusCode: 404));
            }

            _logger.LogInformation(
                "Configuration updated successfully: {ConfigKey}",
                key);

            return Ok(ApiResponse<bool>.CreateSuccess(
                result,
                "Configuration updated successfully",
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error updating configuration: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<bool>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration key: {ConfigKey}", key);
            throw;
        }
    }
}
