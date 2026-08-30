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

    public async Task<TrxTransactionType?> GetTrxTypeAsync(int code, CancellationToken ct = default)
    {
        return await _context.TrxTransactionTypes.FirstOrDefaultAsync(t => t.TrxCode == code, ct);
    }

    public async Task<List<TrxTransactionType>> GetTrxTypesByDocTypeAsync(int docTypeCode, CancellationToken ct = default)
    {
        return await _context.TrxTransactionTypes
            .AsNoTracking()
            .Where(t => t.DocTypeCode == docTypeCode && t.IsActive)
            .OrderBy(t => t.TrxCode)
            .ToListAsync(ct);
    }
}
