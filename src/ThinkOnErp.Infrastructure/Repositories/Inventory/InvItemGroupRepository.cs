using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class InvItemGroupRepository : IInvItemGroupRepository
{
    private readonly OracleDbContext _context;

    public InvItemGroupRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvItemGroup?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.InvItemGroups
            .Include(g => g.SubGroups)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<InvItemGroup?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _context.InvItemGroups
            .FirstOrDefaultAsync(g => g.GroupCode == code, ct);
    }

    public async Task<List<InvItemGroup>> GetMainGroupsAsync(long? branchId = null, CancellationToken ct = default)
    {
        var query = _context.InvItemGroups.AsNoTracking().Where(g => g.GroupLevel == 1 && g.IsActive);
        if (branchId.HasValue) query = query.Where(g => g.BranchId == branchId.Value || g.BranchId == null);
        return await query.OrderBy(g => g.GroupCode).ToListAsync(ct);
    }

    public async Task<List<InvItemGroup>> GetSubGroupsAsync(long mainGroupId, CancellationToken ct = default)
    {
        return await _context.InvItemGroups.AsNoTracking()
            .Where(g => g.ParentGroupId == mainGroupId && g.IsActive)
            .OrderBy(g => g.GroupCode)
            .ToListAsync(ct);
    }

    public async Task<(List<InvItemGroup> Items, int TotalCount)> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var query = _context.InvItemGroups.AsNoTracking();
        if (branchId.HasValue) query = query.Where(g => g.BranchId == branchId.Value || g.BranchId == null);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(g => g.GroupCode).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<InvItemGroup> CreateAsync(InvItemGroup group, CancellationToken ct = default)
    {
        await _context.InvItemGroups.AddAsync(group, ct);
        await _context.SaveChangesAsync(ct);
        return group;
    }

    public async Task UpdateAsync(InvItemGroup group, CancellationToken ct = default)
    {
        _context.InvItemGroups.Update(group);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var group = await _context.InvItemGroups.FindAsync(new object[] { id }, ct);
        if (group != null)
        {
            group.IsActive = false;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(string code, long? excludeId = null, CancellationToken ct = default)
    {
        var query = _context.InvItemGroups.Where(g => g.GroupCode == code);
        if (excludeId.HasValue) query = query.Where(g => g.Id != excludeId.Value);
        return await query.AnyAsync(ct);
    }
}
