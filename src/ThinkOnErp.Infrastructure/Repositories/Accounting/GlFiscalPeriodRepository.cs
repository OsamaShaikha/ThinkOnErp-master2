using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class GlFiscalPeriodRepository : IGlFiscalPeriodRepository
{
    private readonly OracleDbContext _context;

    public GlFiscalPeriodRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GlFiscalPeriod>> GetByFiscalYearIdAsync(long fiscalYearId, CancellationToken cancellationToken = default)
    {
        return await _context.GlFiscalPeriods
            .Where(p => p.FiscalYearId == fiscalYearId)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<GlFiscalPeriod?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.GlFiscalPeriods
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<GlFiscalPeriod?> GetPeriodByDateAsync(long fiscalYearId, DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _context.GlFiscalPeriods
            .Where(p => p.FiscalYearId == fiscalYearId && p.StartDate.Date <= targetDate && p.EndDate.Date >= targetDate)
            .OrderBy(p => p.IsAdjustment ? 1 : 0) // Prefer regular monthly period over adjustment period
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GlFiscalPeriod?> GetPeriodByDateOnlyAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _context.GlFiscalPeriods
            .Include(p => p.FiscalYear)
            .Where(p => p.StartDate.Date <= targetDate && p.EndDate.Date >= targetDate)
            .OrderBy(p => p.IsAdjustment ? 1 : 0)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(GlFiscalPeriod period, CancellationToken cancellationToken = default)
    {
        await _context.GlFiscalPeriods.AddAsync(period, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<GlFiscalPeriod> periods, CancellationToken cancellationToken = default)
    {
        await _context.GlFiscalPeriods.AddRangeAsync(periods, cancellationToken);
    }

    public Task DeleteRangeAsync(IEnumerable<GlFiscalPeriod> periods, CancellationToken cancellationToken = default)
    {
        _context.GlFiscalPeriods.RemoveRange(periods);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
