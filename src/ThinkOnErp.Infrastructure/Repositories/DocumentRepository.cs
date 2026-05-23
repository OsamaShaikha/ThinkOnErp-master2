using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly OracleDbContext _context;

    public DocumentRepository(OracleDbContext context) => _context = context;

    public async Task<long> CreateAsync(SysDocument document)
    {
        _context.SysDocuments.Add(document);
        await _context.SaveChangesAsync();
        return document.Id;
    }

    public async Task UpdateAsync(SysDocument document)
    {
        document.UpdateDate = DateTime.UtcNow;
        _context.SysDocuments.Update(document);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> SoftDeleteAsync(long id, string updateUser)
    {
        var document = await _context.SysDocuments.FindAsync(id);
        if (document == null) return false;

        document.IsActive = "N";
        document.UpdateUser = updateUser;
        document.UpdateDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> BulkSoftDeleteAsync(long[] ids, string updateUser)
    {
        var documents = await _context.SysDocuments
            .Where(d => ids.Contains(d.Id))
            .ToListAsync();

        foreach (var document in documents)
        {
            document.IsActive = "N";
            document.UpdateUser = updateUser;
            document.UpdateDate = DateTime.UtcNow;
        }

        return await _context.SaveChangesAsync();
    }

    public async Task<SysDocument?> GetByIdAsync(long id) =>
        await _context.SysDocuments.FindAsync(id);

    public async Task<(List<SysDocument> Items, int TotalCount)> GetByOwnerAsync(
        string ownerType, long ownerId, int page, int pageSize,
        string? category = null, string? search = null)
    {
        var query = _context.SysDocuments
            .Where(d => d.OwnerType == ownerType && d.OwnerId == ownerId && d.IsActive == "Y");

        if (!string.IsNullOrEmpty(category))
            query = query.Where(d => d.Category == category);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(d =>
                d.FileName.Contains(search) ||
                (d.Description != null && d.Description.Contains(search)) ||
                (d.Tags != null && d.Tags.Contains(search)));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(d => d.CreationDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<int> CountByOwnerAsync(string ownerType, long ownerId) =>
        await _context.SysDocuments
            .CountAsync(d => d.OwnerType == ownerType && d.OwnerId == ownerId && d.IsActive == "Y");

    public async Task<bool> ExistsAsync(long id) =>
        await _context.SysDocuments.AnyAsync(d => d.Id == id && d.IsActive == "Y");
}
