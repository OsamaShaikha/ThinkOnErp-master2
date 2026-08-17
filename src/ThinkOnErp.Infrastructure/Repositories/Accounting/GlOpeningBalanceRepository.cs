using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class GlOpeningBalanceRepository : IGlOpeningBalanceRepository
{
    private readonly OracleDbContext _context;

    public GlOpeningBalanceRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<GlOpeningBalanceHeader?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _context.GlOpeningBalanceHeaders
            .Include(h => h.Details.OrderBy(d => d.LineSer))
                .ThenInclude(d => d.Account)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<GlOpeningBalanceHeader?> GetByBranchAndFiscalYearAsync(
        long branchId,
        long fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        return await _context.GlOpeningBalanceHeaders
            .Include(h => h.Details.OrderBy(d => d.LineSer))
                .ThenInclude(d => d.Account)
            .FirstOrDefaultAsync(
                h => h.BranchId == branchId && h.FiscalYearId == fiscalYearId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<GlOpeningBalanceHeader>> GetAllByBranchAsync(
        long branchId,
        CancellationToken cancellationToken = default)
    {
        return await _context.GlOpeningBalanceHeaders
            .AsNoTracking()
            .Where(h => h.BranchId == branchId)
            .OrderByDescending(h => h.AsOfDate)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsForFiscalYearAsync(
        long branchId,
        long fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        return _context.GlOpeningBalanceHeaders
            .AsNoTracking()
            .AnyAsync(
                h => h.BranchId == branchId && h.FiscalYearId == fiscalYearId,
                cancellationToken);
    }

    public async Task AddAsync(
        GlOpeningBalanceHeader header,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(header);
        await _context.GlOpeningBalanceHeaders.AddAsync(header, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
