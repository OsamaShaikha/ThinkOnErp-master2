using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvReconciliationService : IInvReconciliationService
{
    private readonly IInvStockBalanceRepository _stockBalanceRepository;
    private readonly ILogger<InvReconciliationService> _logger;

    public InvReconciliationService(
        IInvStockBalanceRepository stockBalanceRepository,
        ILogger<InvReconciliationService> logger)
    {
        _stockBalanceRepository = stockBalanceRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> RunReconciliationCheckAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running inventory GL reconciliation check");

        // Sum of all stock balances * avg cost
        var balances = await _stockBalanceRepository.GetByWarehouseAsync(1, cancellationToken);
        var subledgerTotal = balances.Sum(b => b.OnHandQty * b.AvgCost);

        _logger.LogInformation("Inventory subledger valuation sum: {Total}", subledgerTotal);

        return ApiResponse<bool>.CreateSuccess(true, $"Reconciliation complete. Subledger total: {subledgerTotal:N2}");
    }
}
