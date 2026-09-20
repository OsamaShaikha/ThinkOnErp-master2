using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class InvItemCategoryRepository : IInvItemCategoryRepository
{
    private readonly OracleDbContext _context;

    public InvItemCategoryRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvItemCategory?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.InvItemCategories
            .Include(g => g.ParentCategory)
            .Include(g => g.SubCategories)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<InvItemCategory?> GetByCodeAsync(long code, CancellationToken ct = default)
    {
        return await _context.InvItemCategories
            .FirstOrDefaultAsync(g => g.CategoryCode == code, ct);
    }

    public async Task<List<InvItemCategory>> GetMainCategoriesAsync(long? branchId = null, CancellationToken ct = default)
    {
        IQueryable<InvItemCategory> query = _context.InvItemCategories
            .AsNoTracking()
            .Include(g => g.SubCategories)
            .Where(g => g.CategoryLevel == 1 && g.IsActive);

        if (branchId.HasValue) query = query.Where(g => g.BranchId == branchId.Value || g.BranchId == null);
        return await query.OrderBy(g => g.CategoryCode).ToListAsync(ct);
    }

    public async Task<List<InvItemCategory>> GetSubCategoriesAsync(long mainCategoryId, CancellationToken ct = default)
    {
        IQueryable<InvItemCategory> query = _context.InvItemCategories
            .AsNoTracking()
            .Include(g => g.ParentCategory)
            .Where(g => g.ParentCategoryId == mainCategoryId && g.IsActive);

        return await query.OrderBy(g => g.CategoryCode).ToListAsync(ct);
    }

    public async Task<List<InvItemCategory>> GetAllSubCategoriesAsync(long? branchId = null, CancellationToken ct = default)
    {
        IQueryable<InvItemCategory> query = _context.InvItemCategories
            .AsNoTracking()
            .Include(g => g.ParentCategory)
            .Where(g => g.CategoryLevel == 2 && g.IsActive);

        if (branchId.HasValue) query = query.Where(g => g.BranchId == branchId.Value || g.BranchId == null);
        return await query.OrderBy(g => g.ParentCategoryId).ThenBy(g => g.CategoryCode).ToListAsync(ct);
    }

    public async Task<(List<InvItemCategory> Items, int TotalCount)> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        IQueryable<InvItemCategory> query = _context.InvItemCategories.AsNoTracking().Include(g => g.ParentCategory);
        if (branchId.HasValue) query = query.Where(g => g.BranchId == branchId.Value || g.BranchId == null);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(g => g.CategoryLevel).ThenBy(g => g.CategoryCode).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<InvItemCategory> CreateAsync(InvItemCategory category, CancellationToken ct = default)
    {
        await _context.InvItemCategories.AddAsync(category, ct);
        await _context.SaveChangesAsync(ct);
        return category;
    }

    public async Task UpdateAsync(InvItemCategory category, CancellationToken ct = default)
    {
        _context.InvItemCategories.Update(category);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var category = await _context.InvItemCategories.FindAsync(new object[] { id }, ct);
        if (category != null)
        {
            category.IsActive = false;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task HardDeleteAsync(long id, CancellationToken ct = default)
    {
        var category = await _context.InvItemCategories.FindAsync(new object[] { id }, ct);
        if (category != null)
        {
            _context.InvItemCategories.Remove(category);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(long code, long? excludeId = null, CancellationToken ct = default)
    {
        var query = _context.InvItemCategories.Where(g => g.CategoryCode == code);
        if (excludeId.HasValue) query = query.Where(g => g.Id != excludeId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task<bool> HasSubCategoriesAsync(long mainCategoryId, CancellationToken ct = default)
    {
        return await _context.InvItemCategories.AnyAsync(g => g.ParentCategoryId == mainCategoryId && g.IsActive, ct);
    }

    public async Task<bool> HasItemsAsync(long categoryId, bool isMainCategory, CancellationToken ct = default)
    {
        return await _context.InvItems.AnyAsync(i => i.CategoryId == categoryId && i.IsActive, ct);
    }

    public async Task<int> GetItemsCountAsync(long categoryId, bool isMainCategory, CancellationToken ct = default)
    {
        return await _context.InvItems.CountAsync(i => i.CategoryId == categoryId && i.IsActive, ct);
    }

    public async Task<int> GetCategoryItemsCountAsync(long categoryId, CancellationToken ct = default)
    {
        return await _context.InvItems.CountAsync(i => i.CategoryId == categoryId && i.IsActive, ct);
    }

    public async Task<List<InvItemCategory>> GetAllActiveCategoriesAsync(long? branchId = null, bool? posOnly = null, CancellationToken ct = default)
    {
        IQueryable<InvItemCategory> query = _context.InvItemCategories
            .AsNoTracking()
            .Where(g => g.IsActive);

        if (branchId.HasValue)
            query = query.Where(g => g.BranchId == branchId.Value || g.BranchId == null);

        if (posOnly.HasValue && posOnly.Value)
            query = query.Where(g => g.ShowInPos);

        return await query.OrderBy(g => g.CategoryLevel).ThenBy(g => g.CategoryCode).ToListAsync(ct);
    }

    public async Task<List<InvItemCategory>> GetChildrenAsync(long parentCategoryId, CancellationToken ct = default)
    {
        return await _context.InvItemCategories
            .AsNoTracking()
            .Where(g => g.ParentCategoryId == parentCategoryId && g.IsActive)
            .OrderBy(g => g.CategoryCode)
            .ToListAsync(ct);
    }

    public async Task<bool> IsDescendantOfAsync(long potentialChildId, long ancestorId, CancellationToken ct = default)
    {
        if (potentialChildId == ancestorId) return true;

        long? currentParentId = potentialChildId;
        var visited = new HashSet<long>();

        while (currentParentId.HasValue)
        {
            if (!visited.Add(currentParentId.Value)) break;
            if (currentParentId.Value == ancestorId) return true;

            var parent = await _context.InvItemCategories
                .AsNoTracking()
                .Where(g => g.Id == currentParentId.Value)
                .Select(g => g.ParentCategoryId)
                .FirstOrDefaultAsync(ct);

            currentParentId = parent;
        }

        return false;
    }

}
