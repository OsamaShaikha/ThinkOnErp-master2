using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;
using ThinkOnErp.Application.Services;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetFileAttachmentConfig;

/// <summary>
/// Handler for GetFileAttachmentConfigQuery
/// </summary>
public class GetFileAttachmentConfigQueryHandler : IRequestHandler<GetFileAttachmentConfigQuery, FileAttachmentConfigDto>
{
    private readonly ITicketConfigurationService _configService;

    public GetFileAttachmentConfigQueryHandler(ITicketConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<FileAttachmentConfigDto> Handle(GetFileAttachmentConfigQuery request, CancellationToken cancellationToken)
    {
        return new FileAttachmentConfigDto
        {
            MaxSizeBytes = await _configService.GetMaxFileAttachmentSizeAsync(),
            MaxCount = await _configService.GetMaxAttachmentCountAsync(),
            AllowedTypes = await _configService.GetAllowedFileTypesAsync()
        };
    }
}
