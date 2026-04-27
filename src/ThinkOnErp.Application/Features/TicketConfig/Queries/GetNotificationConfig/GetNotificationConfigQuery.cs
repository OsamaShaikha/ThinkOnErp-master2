using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetNotificationConfig;

/// <summary>
/// Query to retrieve notification configuration settings
/// </summary>
public class GetNotificationConfigQuery : IRequest<NotificationConfigDto>
{
}
