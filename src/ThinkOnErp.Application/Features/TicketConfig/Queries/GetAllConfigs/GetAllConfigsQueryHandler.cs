using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetAllConfigs;

/// <summary>
/// Handler for GetAllConfigsQuery
/// </summary>
public class GetAllConfigsQueryHandler : IRequestHandler<GetAllConfigsQuery, List<TicketConfigDto>>
{
    private readonly ITicketConfigRepository _configRepository;

    public GetAllConfigsQueryHandler(ITicketConfigRepository configRepository)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task<List<TicketConfigDto>> Handle(GetAllConfigsQuery request, CancellationToken cancellationToken)
    {
        var configs = await _configRepository.GetAllAsync();

        return configs.Select(c => new TicketConfigDto
        {
            RowId = c.RowId,
            ConfigKey = c.ConfigKey,
            ConfigValue = c.ConfigValue,
            ConfigType = c.ConfigType,
            DescriptionAr = c.DescriptionAr,
            DescriptionEn = c.DescriptionEn,
            IsActive = c.IsActive,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();
    }
}
