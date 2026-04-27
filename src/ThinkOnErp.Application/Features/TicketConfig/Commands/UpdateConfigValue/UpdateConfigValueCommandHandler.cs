using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Services;

namespace ThinkOnErp.Application.Features.TicketConfig.Commands.UpdateConfigValue;

/// <summary>
/// Handler for UpdateConfigValueCommand
/// </summary>
public class UpdateConfigValueCommandHandler : IRequestHandler<UpdateConfigValueCommand, bool>
{
    private readonly ITicketConfigurationService _configService;
    private readonly ILogger<UpdateConfigValueCommandHandler> _logger;

    public UpdateConfigValueCommandHandler(
        ITicketConfigurationService configService,
        ILogger<UpdateConfigValueCommandHandler> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(UpdateConfigValueCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating configuration {ConfigKey} to value {ConfigValue} by user {User}",
            request.ConfigKey,
            request.ConfigValue,
            request.UpdateUser);

        var result = await _configService.UpdateConfigValueAsync(
            request.ConfigKey,
            request.ConfigValue,
            request.UpdateUser);

        if (result)
        {
            _logger.LogInformation("Configuration {ConfigKey} updated successfully", request.ConfigKey);
        }
        else
        {
            _logger.LogWarning("Failed to update configuration {ConfigKey}", request.ConfigKey);
        }

        return result;
    }
}
