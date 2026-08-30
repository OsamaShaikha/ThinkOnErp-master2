using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Reports;
using ThinkOnErp.Domain.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvAtpCalculator : IInvAtpCalculator
{
    private readonly IInvItemRepository _itemRepository;
    private readonly IInvStockBalanceRepository _stockBalanceRepository;
    private readonly ILogger<InvAtpCalculator> _logger;

    public InvAtpCalculator(
        IInvItemRepository itemRepository,
        IInvStockBalanceRepository stockBalanceRepository,
        ILogger<InvAtpCalculator> logger)
    {
        _itemRepository = itemRepository;
        _stockBalanceRepository = stockBalanceRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<AtpResultDto>> CalculateAtpAsync(long itemId, long? warehouseId = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating ATP for item {ItemId}, warehouse {WarehouseId}", itemId, warehouseId);

        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item == null)
        {
            return ApiResponse<AtpResultDto>.CreateFailure("Item not found", null, 404);
        }

        var balances = await _stockBalanceRepository.GetByItemAsync(itemId, cancellationToken);
        if (warehouseId.HasValue && warehouseId.Value > 0)
        {
            balances = balances.Where(b => b.WarehouseId == warehouseId.Value).ToList();
        }

        var onHand = balances.Sum(b => b.OnHandQty);
        var reserved = balances.Sum(b => b.ReservedQty);
        var onOrder = balances.Sum(b => b.OnOrderQty);
        var safetyStock = item.SafetyStock;
        var atp = onHand + onOrder - reserved - safetyStock;

        var result = new AtpResultDto
        {
            ItemId = item.Id,
            ItemCode = item.ItemCode,
            OnHand = onHand,
            Reserved = reserved,
            OnOrder = onOrder,
            SafetyStock = safetyStock,
            Atp = atp,
            EarliestAvailableDate = onOrder > 0 ? DateTime.UtcNow.AddDays(item.LeadTimeDays > 0 ? item.LeadTimeDays : 7) : null
        };

        return ApiResponse<AtpResultDto>.CreateSuccess(result);
    }
}
