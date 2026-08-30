using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvStockLedgerService
{
    Task<ApiResponse<StockMovementDto>> PostMovementAsync(StockMovementRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<StockMovementDto>>> GetMovementsByItemAsync(long itemId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<StockMovementDto>>> GetMovementsByWarehouseAsync(long warehouseId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<StockMovementDto>>> GetMovementsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}
