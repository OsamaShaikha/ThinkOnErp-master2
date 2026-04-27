using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetFileAttachmentConfig;

/// <summary>
/// Query to retrieve file attachment configuration settings
/// </summary>
public class GetFileAttachmentConfigQuery : IRequest<FileAttachmentConfigDto>
{
}
