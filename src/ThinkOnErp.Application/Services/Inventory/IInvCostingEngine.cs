using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvCostingEngine
{
    Task<ApiResponse<decimal>> RecalculateWeightedAverageAsync(long itemId, decimal incomingQty, decimal incomingCost, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ConsumeFifoLayersAsync(long itemId, decimal quantityToConsume, CancellationToken cancellationToken = default);
    Task<ApiResponse<decimal>> CalculateStandardCostVarianceAsync(long itemId, decimal actualCost, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> GetFefoLotAsync(long itemId, CancellationToken cancellationToken = default);
    Task<ApiResponse<decimal>> GetCostForIssueAsync(long itemId, decimal quantity, string costingMethod, CancellationToken cancellationToken = default);
}
