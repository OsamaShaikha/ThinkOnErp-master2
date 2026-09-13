using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

public class PosModifierRepository : IPosModifierRepository
{
    private readonly OracleDbContext _context;

    public PosModifierRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PosModifierGroup>> GetGroupsByBranchAsync(long branchId, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.PosModifierGroups
            .AsNoTracking()
            .Include(g => g.Options.OrderBy(o => o.SortOrder))
            .Include(g => g.ItemLinks)
            .Where(g => g.BranchId == branchId);

        if (isActive.HasValue)
        {
            query = query.Where(g => g.IsActive == isActive.Value);
        }

        return await query.OrderBy(g => g.SortOrder).ThenBy(g => g.GroupCode).ToListAsync(ct);
    }

    public async Task<PosModifierGroup?> GetGroupByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosModifierGroups
            .Include(g => g.Options.OrderBy(o => o.SortOrder))
            .Include(g => g.ItemLinks)
                .ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<PosModifierGroup?> GetGroupByCodeAsync(long branchId, string code, CancellationToken ct = default)
    {
        var upperCode = code.Trim().ToUpperInvariant();
        return await _context.PosModifierGroups
            .Include(g => g.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(g => g.BranchId == branchId && g.GroupCode.ToUpper() == upperCode, ct);
    }

    public async Task AddGroupAsync(PosModifierGroup group, CancellationToken ct = default)
    {
        await _context.PosModifierGroups.AddAsync(group, ct);
    }

    public Task UpdateGroupAsync(PosModifierGroup group, CancellationToken ct = default)
    {
        _context.PosModifierGroups.Update(group);
        return Task.CompletedTask;
    }

    public Task DeleteGroupAsync(PosModifierGroup group, CancellationToken ct = default)
    {
        _context.PosModifierGroups.Remove(group);
        return Task.CompletedTask;
    }

    public async Task<PosModifierOption?> GetOptionByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosModifierOptions
            .Include(o => o.ModifierGroup)
            .Include(o => o.RelatedItem)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task AddOptionAsync(PosModifierOption option, CancellationToken ct = default)
    {
        await _context.PosModifierOptions.AddAsync(option, ct);
    }

    public Task UpdateOptionAsync(PosModifierOption option, CancellationToken ct = default)
    {
        _context.PosModifierOptions.Update(option);
        return Task.CompletedTask;
    }

    public Task DeleteOptionAsync(PosModifierOption option, CancellationToken ct = default)
    {
        _context.PosModifierOptions.Remove(option);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PosModifierGroup>> GetGroupsByItemIdAsync(long itemId, CancellationToken ct = default)
    {
        var groupIds = await _context.PosItemModifierGroups
            .Where(m => m.ItemId == itemId)
            .OrderBy(m => m.SortOrder)
            .Select(m => m.ModifierGroupId)
            .ToListAsync(ct);

        return await _context.PosModifierGroups
            .AsNoTracking()
            .Include(g => g.Options.Where(o => o.IsActive).OrderBy(o => o.SortOrder))
            .Where(g => groupIds.Contains(g.Id) && g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<PosItemModifierGroup?> GetItemModifierGroupAsync(long itemId, long groupId, CancellationToken ct = default)
    {
        return await _context.PosItemModifierGroups
            .FirstOrDefaultAsync(m => m.ItemId == itemId && m.ModifierGroupId == groupId, ct);
    }

    public async Task AddItemModifierGroupAsync(PosItemModifierGroup itemGroup, CancellationToken ct = default)
    {
        await _context.PosItemModifierGroups.AddAsync(itemGroup, ct);
    }

    public Task DeleteItemModifierGroupAsync(PosItemModifierGroup itemGroup, CancellationToken ct = default)
    {
        _context.PosItemModifierGroups.Remove(itemGroup);
        return Task.CompletedTask;
    }

    public async Task ClearItemModifierGroupsAsync(long itemId, CancellationToken ct = default)
    {
        var existing = await _context.PosItemModifierGroups
            .Where(m => m.ItemId == itemId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            _context.PosItemModifierGroups.RemoveRange(existing);
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
