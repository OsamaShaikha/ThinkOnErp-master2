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

public class PosTableRepository : IPosTableRepository
{
    private readonly OracleDbContext _context;

    public PosTableRepository(OracleDbContext context)
    {
        _context = context;
    }

    // Floors
    public async Task<IReadOnlyList<PosFloor>> GetFloorsWithTablesAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.PosFloors
            .AsNoTracking()
            .Include(f => f.Tables.Where(t => t.IsActive))
            .Where(f => f.BranchId == branchId && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PosFloor>> GetFloorsByBranchAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.PosFloors
            .AsNoTracking()
            .Include(f => f.Tables)
            .Where(f => f.BranchId == branchId)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<PosFloor?> GetFloorByIdAsync(long floorId, CancellationToken ct = default)
    {
        return await _context.PosFloors
            .Include(f => f.Tables)
            .FirstOrDefaultAsync(f => f.Id == floorId, ct);
    }

    public async Task AddFloorAsync(PosFloor floor, CancellationToken ct = default)
    {
        await _context.PosFloors.AddAsync(floor, ct);
    }

    public Task UpdateFloorAsync(PosFloor floor, CancellationToken ct = default)
    {
        _context.PosFloors.Update(floor);
        return Task.CompletedTask;
    }

    public Task DeleteFloorAsync(PosFloor floor, CancellationToken ct = default)
    {
        _context.PosFloors.Remove(floor);
        return Task.CompletedTask;
    }

    // Tables
    public async Task<IReadOnlyList<PosTable>> GetTablesByFloorAsync(long floorId, CancellationToken ct = default)
    {
        return await _context.PosTables
            .AsNoTracking()
            .Include(t => t.Floor)
            .Where(t => t.FloorId == floorId)
            .OrderBy(t => t.TableNumber)
            .ToListAsync(ct);
    }

    public async Task<PosTable?> GetTableByIdAsync(long tableId, CancellationToken ct = default)
    {
        return await _context.PosTables
            .Include(t => t.Floor)
            .Include(t => t.ActiveOrder)
            .FirstOrDefaultAsync(t => t.Id == tableId, ct);
    }

    public async Task AddTableAsync(PosTable table, CancellationToken ct = default)
    {
        await _context.PosTables.AddAsync(table, ct);
    }

    public Task UpdateTableAsync(PosTable table, CancellationToken ct = default)
    {
        _context.PosTables.Update(table);
        return Task.CompletedTask;
    }

    public Task DeleteTableAsync(PosTable table, CancellationToken ct = default)
    {
        _context.PosTables.Remove(table);
        return Task.CompletedTask;
    }

    public async Task UpdateTableStatusAsync(long tableId, PosTableStatus status, long? activeOrderId, CancellationToken ct = default)
    {
        var table = await _context.PosTables.FirstOrDefaultAsync(t => t.Id == tableId, ct);
        if (table != null)
        {
            table.Status = status;
            table.ActiveOrderId = activeOrderId;
            table.StatusChangedAt = System.DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
