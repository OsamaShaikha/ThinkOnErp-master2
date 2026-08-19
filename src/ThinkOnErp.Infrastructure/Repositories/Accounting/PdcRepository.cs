using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class PdcRepository : IPdcRepository
{
    private readonly OracleDbContext _context;

    public PdcRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<GlPdcRegister?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.PdcRegisters
            .Include(p => p.Branch)
            .Include(p => p.FiscalYear)
            .Include(p => p.Currency)
            .Include(p => p.OriginatingVoucher)
            .Include(p => p.ClearingVoucher)
            .Include(p => p.BounceVoucher)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<GlPdcRegister?> GetByChequeNumberAsync(string chequeNumber, string chequeType, CancellationToken cancellationToken = default)
    {
        return await _context.PdcRegisters
            .Include(p => p.Branch)
            .Include(p => p.FiscalYear)
            .Include(p => p.Currency)
            .FirstOrDefaultAsync(p => p.ChequeNumber == chequeNumber && p.ChequeType == chequeType, cancellationToken);
    }

    public async Task<IReadOnlyList<GlPdcRegister>> GetChequesAsync(
        long? branchId,
        string? chequeType,
        string? status,
        string? partyCode,
        DateTime? fromDueDate,
        DateTime? toDueDate,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PdcRegisters
            .Include(p => p.Branch)
            .Include(p => p.Currency)
            .Include(p => p.OriginatingVoucher)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(p => p.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(chequeType))
        {
            query = query.Where(p => p.ChequeType == chequeType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(partyCode))
        {
            query = query.Where(p => p.PartyCode == partyCode);
        }

        if (fromDueDate.HasValue)
        {
            query = query.Where(p => p.DueDate >= fromDueDate.Value.Date);
        }

        if (toDueDate.HasValue)
        {
            query = query.Where(p => p.DueDate <= toDueDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        return await query.OrderBy(p => p.DueDate).ThenBy(p => p.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GlPdcRegister>> GetUpcomingMaturitiesAsync(long? branchId, int daysAhead, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var horizon = today.AddDays(daysAhead);

        var query = _context.PdcRegisters
            .Include(p => p.Branch)
            .Include(p => p.Currency)
            .Where(p => (p.Status == "RECEIVED" || p.Status == "DEPOSITED" || p.Status == "ISSUED")
                     && p.DueDate >= today
                     && p.DueDate <= horizon);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(p => p.BranchId == branchId.Value);
        }

        return await query.OrderBy(p => p.DueDate).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(GlPdcRegister pdc, CancellationToken cancellationToken = default)
    {
        await _context.PdcRegisters.AddAsync(pdc, cancellationToken);
    }

    public void Remove(GlPdcRegister pdc)
    {
        _context.PdcRegisters.Remove(pdc);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
