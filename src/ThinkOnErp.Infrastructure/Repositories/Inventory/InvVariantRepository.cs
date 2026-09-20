using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class InvVariantRepository : IInvVariantRepository
{
    private readonly OracleDbContext _context;

    public InvVariantRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<InvItemAttribute>> GetAllAttributesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.InvItemAttributes
            .Include(a => a.Values.OrderBy(v => v.SortOrder))
            .AsNoTracking();

        if (activeOnly)
            query = query.Where(a => a.IsActive);

        return await query.OrderBy(a => a.AttributeCode).ToListAsync(cancellationToken);
    }

    public async Task<InvItemAttribute?> GetAttributeByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.InvItemAttributes
            .Include(a => a.Values.OrderBy(v => v.SortOrder))
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<InvItemAttribute?> GetAttributeByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.InvItemAttributes
            .Include(a => a.Values.OrderBy(v => v.SortOrder))
            .FirstOrDefaultAsync(a => a.AttributeCode == code, cancellationToken);
    }

    public Task AddAttributeAsync(InvItemAttribute attribute, CancellationToken cancellationToken = default)
    {
        _context.InvItemAttributes.Add(attribute);
        return Task.CompletedTask;
    }

    public Task UpdateAttributeAsync(InvItemAttribute attribute, CancellationToken cancellationToken = default)
    {
        _context.InvItemAttributes.Update(attribute);
        return Task.CompletedTask;
    }

    public async Task<InvItemAttributeValue?> GetAttributeValueByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.InvItemAttributeValues
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public Task AddAttributeValueAsync(InvItemAttributeValue value, CancellationToken cancellationToken = default)
    {
        _context.InvItemAttributeValues.Add(value);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<InvItemVariant> Items, int TotalCount)> GetVariantsPagedAsync(
        int pageNumber,
        int pageSize,
        long? itemId = null,
        string? search = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvItemVariants
            .Include(v => v.Item)
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.Attribute)
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.AttributeValue)
            .AsNoTracking()
            .AsQueryable();

        if (itemId.HasValue)
            query = query.Where(v => v.ItemId == itemId.Value);

        if (activeOnly.HasValue)
            query = query.Where(v => v.IsActive == activeOnly.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(v => v.Sku.ToLower().Contains(searchLower) ||
                                     v.VariantNameLocal.ToLower().Contains(searchLower) ||
                                     (v.VariantNameEn != null && v.VariantNameEn.ToLower().Contains(searchLower)) ||
                                     (v.Barcode != null && v.Barcode.ToLower().Contains(searchLower)) ||
                                     (v.Item != null && v.Item.ItemCode.ToLower().Contains(searchLower)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(v => v.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<InvItemVariant>> GetVariantsByItemIdAsync(long itemId, CancellationToken cancellationToken = default)
    {
        return await _context.InvItemVariants
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.Attribute)
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.AttributeValue)
            .Where(v => v.ItemId == itemId)
            .OrderBy(v => v.Sku)
            .ToListAsync(cancellationToken);
    }

    public async Task<InvItemVariant?> GetVariantByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.InvItemVariants
            .Include(v => v.Item)
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.Attribute)
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.AttributeValue)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<InvItemVariant?> GetVariantBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await _context.InvItemVariants
            .Include(v => v.Item)
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.Attribute)
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.AttributeValue)
            .FirstOrDefaultAsync(v => v.Sku == sku, cancellationToken);
    }

    public async Task<InvItemVariant?> GetVariantByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        return await _context.InvItemVariants
            .Include(v => v.Item)
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.Attribute)
            .Include(v => v.AttributeValues)
                .ThenInclude(av => av.AttributeValue)
            .FirstOrDefaultAsync(v => v.Barcode == barcode, cancellationToken);
    }

    public async Task<bool> ExistsSkuAsync(string sku, long? excludeVariantId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.InvItemVariants.Where(v => v.Sku == sku);
        if (excludeVariantId.HasValue)
            query = query.Where(v => v.Id != excludeVariantId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public Task AddVariantAsync(InvItemVariant variant, CancellationToken cancellationToken = default)
    {
        _context.InvItemVariants.Add(variant);
        return Task.CompletedTask;
    }

    public Task UpdateVariantAsync(InvItemVariant variant, CancellationToken cancellationToken = default)
    {
        _context.InvItemVariants.Update(variant);
        return Task.CompletedTask;
    }

    public Task DeleteVariantAsync(InvItemVariant variant, CancellationToken cancellationToken = default)
    {
        _context.InvItemVariants.Remove(variant);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
