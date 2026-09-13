using System;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Sync.Queries.PullCatalogSync;

public record PullCatalogSyncQuery(long BranchId, DateTime? LastSyncTime) : IRequest<ApiResponse<PullCatalogSyncDto>>;

public class PullCatalogSyncQueryHandler : IRequestHandler<PullCatalogSyncQuery, ApiResponse<PullCatalogSyncDto>>
{
    private readonly IPosSyncService _syncService;

    public PullCatalogSyncQueryHandler(IPosSyncService syncService)
    {
        _syncService = syncService;
    }

    public async Task<ApiResponse<PullCatalogSyncDto>> Handle(PullCatalogSyncQuery request, CancellationToken cancellationToken)
    {
        return await _syncService.PullCatalogSyncAsync(request.BranchId, request.LastSyncTime, cancellationToken);
    }
}
