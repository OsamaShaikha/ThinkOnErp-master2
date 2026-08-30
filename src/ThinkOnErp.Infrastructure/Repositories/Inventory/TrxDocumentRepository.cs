using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class TrxDocumentRepository : ITrxDocumentRepository
{
    private readonly OracleDbContext _context;

    public TrxDocumentRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<TrxDocumentHeader?> GetByKeyAsync(long branchId, int docYear, int docType, long id, CancellationToken ct = default)
    {
        return await _context.TrxDocumentHeaders
            .Include(h => h.Lines)
                .ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DocYear == docYear && h.DocType == docType && h.Id == id, ct);
    }

    public async Task<TrxDocumentHeader?> GetByDocNoAsync(long branchId, int docYear, int docType, string docNo, CancellationToken ct = default)
    {
        return await _context.TrxDocumentHeaders
            .Include(h => h.Lines)
            .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DocYear == docYear && h.DocType == docType && h.DocNo == docNo, ct);
    }

    public async Task<(List<TrxDocumentHeader> Items, int TotalCount)> GetPagedAsync(
        long branchId, int? docYear, int? docType, int? trxType, int? partyTypeCode, long? partyId, int? statusCode,
        int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _context.TrxDocumentHeaders.AsNoTracking().Where(h => h.BranchId == branchId);

        if (docYear.HasValue) query = query.Where(h => h.DocYear == docYear.Value);
        if (docType.HasValue) query = query.Where(h => h.DocType == docType.Value);
        if (trxType.HasValue) query = query.Where(h => h.TrxType == trxType.Value);
        if (partyTypeCode.HasValue) query = query.Where(h => h.PartyTypeCode == partyTypeCode.Value);
        if (partyId.HasValue) query = query.Where(h => h.PartyId == partyId.Value);
        if (statusCode.HasValue) query = query.Where(h => h.DocStatusCode == statusCode.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(h => h.DocDate)
            .ThenByDescending(h => h.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<long> GetNextIdAsync(long branchId, int docYear, int docType, CancellationToken ct = default)
    {
        var maxId = await _context.TrxDocumentHeaders
            .Where(h => h.BranchId == branchId && h.DocYear == docYear && h.DocType == docType)
            .Select(h => (long?)h.Id)
            .MaxAsync(ct);

        return (maxId ?? 0) + 1;
    }

    public async Task<TrxDocumentHeader> CreateAsync(TrxDocumentHeader doc, CancellationToken ct = default)
    {
        await _context.TrxDocumentHeaders.AddAsync(doc, ct);
        await _context.SaveChangesAsync(ct);
        return doc;
    }

    public async Task UpdateAsync(TrxDocumentHeader doc, CancellationToken ct = default)
    {
        _context.TrxDocumentHeaders.Update(doc);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(TrxDocumentHeader doc, CancellationToken ct = default)
    {
        _context.TrxDocumentHeaders.Remove(doc);
        await _context.SaveChangesAsync(ct);
    }
}
