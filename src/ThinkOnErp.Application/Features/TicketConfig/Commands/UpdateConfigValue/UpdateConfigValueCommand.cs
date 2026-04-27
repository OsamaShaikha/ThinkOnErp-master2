using MediatR;

namespace ThinkOnErp.Application.Features.TicketConfig.Commands.UpdateConfigValue;

/// <summary>
/// Command to update a configuration value
/// </summary>
public class UpdateConfigValueCommand : IRequest<bool>
{
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public string UpdateUser { get; set; } = string.Empty;
}
