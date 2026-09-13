using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosOrderRepository
{
    Task<PosOrderHeader?> GetOrderByIdAsync(long orderId, CancellationToken ct = default);
    Task<PosOrderHeader?> GetOrderByClientUuidAsync(long branchId, string clientUuid, CancellationToken ct = default);
    Task<IReadOnlyList<PosOrderHeader>> GetActiveOrdersAsync(long branchId, PosOrderStatus? status = null, CancellationToken ct = default);
    Task<(IReadOnlyList<PosOrderHeader> Items, long TotalCount)> GetOrdersPagedAsync(
        long branchId,
        long? shiftId = null,
        PosOrderStatus? status = null,
        PosOrderType? orderType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<int> GetOrderCountTodayAsync(long branchId, CancellationToken ct = default);
    Task AddOrderAsync(PosOrderHeader order, CancellationToken ct = default);
    Task UpdateOrderAsync(PosOrderHeader order, CancellationToken ct = default);
    Task DeleteOrderAsync(PosOrderHeader order, CancellationToken ct = default);
    Task AddPaymentAsync(PosOrderPayment payment, CancellationToken ct = default);
    Task AddTaxAsync(PosOrderTax tax, CancellationToken ct = default);
    Task AddInventoryConflictAsync(PosInventoryConflict conflict, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
