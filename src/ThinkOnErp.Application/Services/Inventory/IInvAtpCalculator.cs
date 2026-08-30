using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Reports;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvAtpCalculator
{
    Task<ApiResponse<AtpResultDto>> CalculateAtpAsync(long itemId, long? warehouseId = null, CancellationToken cancellationToken = default);
}
