using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.OpeningBalance;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvOpeningBalanceService
{
    Task<ApiResponse<OpeningBatchDto>> CreateBatchAsync(CreateOpeningBatchDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<OpeningBatchDto>> GetBatchByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<(List<OpeningBatchDto> Batches, int TotalCount)>> GetBatchesPagedAsync(long branchId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<OpeningBatchDto>> UpdateBatchAsync(long id, UpdateOpeningBatchDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteBatchAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<OpeningBatchDto>> PostBatchAsync(long id, string username, CancellationToken ct = default);
}
