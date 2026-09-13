using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosSyncService
{
    Task<ApiResponse<PullCatalogSyncDto>> PullCatalogSyncAsync(long branchId, DateTime? lastSyncTime, CancellationToken ct = default);
    Task<ApiResponse<PushOfflineOrdersResultDto>> PushOfflineOrdersAsync(long branchId, List<CreatePosOrderDto> orders, string username, CancellationToken ct = default);
}
