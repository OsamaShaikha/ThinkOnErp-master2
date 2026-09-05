using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class TrxTypeRepository : ITrxTypeRepository
{
    private readonly OracleDbContext _context;

    public TrxTypeRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<TrxDocType?> GetDocTypeAsync(int code, CancellationToken ct = default)
    {
        return await _context.TrxDocTypes
            .Include(t => t.TransactionTypes)
            .FirstOrDefaultAsync(t => t.TypeCode == code, ct);
    }

    public async Task<List<TrxDocType>> GetAllDocTypesAsync(CancellationToken ct = default)
    {
        return await _context.TrxDocTypes
            .AsNoTracking()
            .Include(t => t.TransactionTypes)
            .OrderBy(t => t.TypeCode)
            .ToListAsync(ct);
    }

    public async Task<TrxDocType> CreateDocTypeAsync(TrxDocType docType, CancellationToken ct = default)
    {
        await _context.TrxDocTypes.AddAsync(docType, ct);
        await _context.SaveChangesAsync(ct);
        return docType;
    }

    public async Task UpdateDocTypeAsync(TrxDocType docType, CancellationToken ct = default)
    {
        _context.TrxDocTypes.Update(docType);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteDocTypeAsync(TrxDocType docType, CancellationToken ct = default)
    {
        _context.TrxDocTypes.Remove(docType);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<TrxTransactionType?> GetTrxTypeAsync(int code, CancellationToken ct = default)
    {
        return await _context.TrxTransactionTypes
            .Include(t => t.DocType)
            .FirstOrDefaultAsync(t => t.TrxCode == code, ct);
    }

    public async Task<List<TrxTransactionType>> GetAllTrxTypesAsync(CancellationToken ct = default)
    {
        return await _context.TrxTransactionTypes
            .AsNoTracking()
            .Include(t => t.DocType)
            .OrderBy(t => t.DocTypeCode)
            .ThenBy(t => t.TrxCode)
            .ToListAsync(ct);
    }

    public async Task<List<TrxTransactionType>> GetTrxTypesByDocTypeAsync(int docTypeCode, CancellationToken ct = default)
    {
        return await _context.TrxTransactionTypes
            .AsNoTracking()
            .Where(t => t.DocTypeCode == docTypeCode)
            .OrderBy(t => t.TrxCode)
            .ToListAsync(ct);
    }

    public async Task<TrxTransactionType> CreateTrxTypeAsync(TrxTransactionType trxType, CancellationToken ct = default)
    {
        await _context.TrxTransactionTypes.AddAsync(trxType, ct);
        await _context.SaveChangesAsync(ct);
        return trxType;
    }

    public async Task UpdateTrxTypeAsync(TrxTransactionType trxType, CancellationToken ct = default)
    {
        _context.TrxTransactionTypes.Update(trxType);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteTrxTypeAsync(TrxTransactionType trxType, CancellationToken ct = default)
    {
        _context.TrxTransactionTypes.Remove(trxType);
        await _context.SaveChangesAsync(ct);
    }
}
