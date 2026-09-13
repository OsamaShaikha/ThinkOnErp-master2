using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

public class PosOrderRepository : IPosOrderRepository
{
    private readonly OracleDbContext _context;

    public PosOrderRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<PosOrderHeader?> GetOrderByIdAsync(long orderId, CancellationToken ct = default)
    {
        return await _context.PosOrderHeaders
            .Include(o => o.Lines)
                .ThenInclude(l => l.Modifiers)
            .Include(o => o.Payments)
            .Include(o => o.Taxes)
            .Include(o => o.Customer)
            .Include(o => o.Table)
            .Include(o => o.Shift)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
    }

    public async Task<PosOrderHeader?> GetOrderByClientUuidAsync(long branchId, string clientUuid, CancellationToken ct = default)
    {
        return await _context.PosOrderHeaders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.BranchId == branchId && o.ClientUuid == clientUuid, ct);
    }

    public async Task<IReadOnlyList<PosOrderHeader>> GetActiveOrdersAsync(long branchId, PosOrderStatus? status = null, CancellationToken ct = default)
    {
        var query = _context.PosOrderHeaders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Table)
            .Where(o => o.BranchId == branchId && o.IsActive);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }
        else
        {
            query = query.Where(o => o.Status != PosOrderStatus.Completed && o.Status != PosOrderStatus.Voided && o.Status != PosOrderStatus.Refunded);
        }

        return await query.OrderByDescending(o => o.CreationDate).ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<PosOrderHeader> Items, long TotalCount)> GetOrdersPagedAsync(
        long branchId,
        long? shiftId = null,
        PosOrderStatus? status = null,
        PosOrderType? orderType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.PosOrderHeaders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Include(o => o.Customer)
            .Include(o => o.Table)
            .Where(o => o.BranchId == branchId);

        if (shiftId.HasValue)
            query = query.Where(o => o.ShiftId == shiftId.Value);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (orderType.HasValue)
            query = query.Where(o => o.OrderType == orderType.Value);

        if (fromDate.HasValue)
            query = query.Where(o => o.CreationDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(o => o.CreationDate <= toDate.Value);

        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreationDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<int> GetOrderCountTodayAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.PosOrderHeaders
            .CountAsync(o => o.BranchId == branchId && o.CreationDate.Date == DateTime.UtcNow.Date, ct);
    }

    public async Task AddOrderAsync(PosOrderHeader order, CancellationToken ct = default)
    {
        await _context.PosOrderHeaders.AddAsync(order, ct);
    }

    public Task UpdateOrderAsync(PosOrderHeader order, CancellationToken ct = default)
    {
        _context.PosOrderHeaders.Update(order);
        return Task.CompletedTask;
    }

    public Task DeleteOrderAsync(PosOrderHeader order, CancellationToken ct = default)
    {
        _context.PosOrderHeaders.Remove(order);
        return Task.CompletedTask;
    }

    public async Task AddPaymentAsync(PosOrderPayment payment, CancellationToken ct = default)
    {
        await _context.PosOrderPayments.AddAsync(payment, ct);
    }

    public async Task AddTaxAsync(PosOrderTax tax, CancellationToken ct = default)
    {
        await _context.PosOrderTaxes.AddAsync(tax, ct);
    }

    public async Task AddInventoryConflictAsync(PosInventoryConflict conflict, CancellationToken ct = default)
    {
        await _context.PosInventoryConflicts.AddAsync(conflict, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
