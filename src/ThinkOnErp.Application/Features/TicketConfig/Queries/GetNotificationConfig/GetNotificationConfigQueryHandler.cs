using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;
using ThinkOnErp.Application.Services;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetNotificationConfig;

/// <summary>
/// Handler for GetNotificationConfigQuery
/// </summary>
public class GetNotificationConfigQueryHandler : IRequestHandler<GetNotificationConfigQuery, NotificationConfigDto>
{
    private readonly ITicketConfigurationService _configService;

    public GetNotificationConfigQueryHandler(ITicketConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<NotificationConfigDto> Handle(GetNotificationConfigQuery request, CancellationToken cancellationToken)
    {
        var templates = new Dictionary<string, string>
        {
            { "TicketCreated", await _configService.GetNotificationTemplateAsync("TicketCreated") },
            { "TicketAssigned", await _configService.GetNotificationTemplateAsync("TicketAssigned") },
            { "TicketStatusChanged", await _configService.GetNotificationTemplateAsync("TicketStatusChanged") },
            { "TicketResolved", await _configService.GetNotificationTemplateAsync("TicketResolved") },
            { "CommentAdded", await _configService.GetNotificationTemplateAsync("CommentAdded") }
        };

        return new NotificationConfigDto
        {
            Enabled = await _configService.AreNotificationsEnabledAsync(),
            Templates = templates
        };
    }
}
