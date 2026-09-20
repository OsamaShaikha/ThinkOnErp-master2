using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public class InvModifierRepository : IInvModifierRepository, IPosModifierRepository
{
    private readonly OracleDbContext _context;

    public InvModifierRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<InvModifierGroup>> GetGroupsByBranchAsync(long branchId, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.InvModifierGroups
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

    public async Task<InvModifierGroup?> GetGroupByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.InvModifierGroups
            .Include(g => g.Options.OrderBy(o => o.SortOrder))
            .Include(g => g.ItemLinks)
                .ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<InvModifierGroup?> GetGroupByCodeAsync(long branchId, string code, CancellationToken ct = default)
    {
        var upperCode = code.Trim().ToUpperInvariant();
        return await _context.InvModifierGroups
            .Include(g => g.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(g => g.BranchId == branchId && g.GroupCode.ToUpper() == upperCode, ct);
    }

    public async Task AddGroupAsync(InvModifierGroup group, CancellationToken ct = default)
    {
        await _context.InvModifierGroups.AddAsync(group, ct);
    }

    public Task UpdateGroupAsync(InvModifierGroup group, CancellationToken ct = default)
    {
        _context.InvModifierGroups.Update(group);
        return Task.CompletedTask;
    }

    public Task DeleteGroupAsync(InvModifierGroup group, CancellationToken ct = default)
    {
        _context.InvModifierGroups.Remove(group);
        return Task.CompletedTask;
    }

    public async Task<InvModifierOption?> GetOptionByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.InvModifierOptions
            .Include(o => o.ModifierGroup)
            .Include(o => o.RelatedItem)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task AddOptionAsync(InvModifierOption option, CancellationToken ct = default)
    {
        await _context.InvModifierOptions.AddAsync(option, ct);
    }

    public Task UpdateOptionAsync(InvModifierOption option, CancellationToken ct = default)
    {
        _context.InvModifierOptions.Update(option);
        return Task.CompletedTask;
    }

    public Task DeleteOptionAsync(InvModifierOption option, CancellationToken ct = default)
    {
        _context.InvModifierOptions.Remove(option);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<InvModifierGroup>> GetGroupsByItemIdAsync(long itemId, CancellationToken ct = default)
    {
        var groupIds = await _context.InvItemModifierGroups
            .Where(m => m.ItemId == itemId)
            .OrderBy(m => m.SortOrder)
            .Select(m => m.ModifierGroupId)
            .ToListAsync(ct);

        return await _context.InvModifierGroups
            .AsNoTracking()
            .Include(g => g.Options.Where(o => o.IsActive).OrderBy(o => o.SortOrder))
            .Where(g => groupIds.Contains(g.Id) && g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<InvItemModifierGroup?> GetItemModifierGroupAsync(long itemId, long groupId, CancellationToken ct = default)
    {
        return await _context.InvItemModifierGroups
            .FirstOrDefaultAsync(m => m.ItemId == itemId && m.ModifierGroupId == groupId, ct);
    }

    public async Task AddItemModifierGroupAsync(InvItemModifierGroup itemGroup, CancellationToken ct = default)
    {
        await _context.InvItemModifierGroups.AddAsync(itemGroup, ct);
    }

    public Task DeleteItemModifierGroupAsync(InvItemModifierGroup itemGroup, CancellationToken ct = default)
    {
        _context.InvItemModifierGroups.Remove(itemGroup);
        return Task.CompletedTask;
    }

    public async Task ClearItemModifierGroupsAsync(long itemId, CancellationToken ct = default)
    {
        var existing = await _context.InvItemModifierGroups
            .Where(m => m.ItemId == itemId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            _context.InvItemModifierGroups.RemoveRange(existing);
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
