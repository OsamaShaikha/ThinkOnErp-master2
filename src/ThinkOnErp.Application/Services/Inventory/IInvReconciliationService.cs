using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvReconciliationService
{
    Task<ApiResponse<bool>> RunReconciliationCheckAsync(CancellationToken cancellationToken = default);
}
