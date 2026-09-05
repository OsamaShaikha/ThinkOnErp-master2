using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class PayrollPeriodRepository : IPayrollPeriodRepository
{
    private readonly OracleDbContext _context;

    public PayrollPeriodRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<PayrollPeriod>> GetAllAsync(long companyId, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollPeriods
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId);

        if (fiscalYear.HasValue)
        {
            query = query.Where(p => p.FiscalYear == fiscalYear.Value);
        }

        return await query.OrderByDescending(p => p.FiscalYear).ThenByDescending(p => p.Month).ToListAsync(cancellationToken);
    }

    public async Task<PayrollPeriod?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PayrollPeriod?> GetByPeriodCodeAsync(long companyId, string periodCode, string payrollType = "MONTHLY", CancellationToken cancellationToken = default)
    {
        return await _context.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.PeriodCode == periodCode && p.PayrollType == payrollType, cancellationToken);
    }

    public async Task<bool> ExistsAsync(long companyId, int fiscalYear, int month, string payrollType = "MONTHLY", CancellationToken cancellationToken = default)
    {
        return await _context.PayrollPeriods
            .AnyAsync(p => p.CompanyId == companyId && p.FiscalYear == fiscalYear && p.Month == month && p.PayrollType == payrollType, cancellationToken);
    }

    public async Task AddAsync(PayrollPeriod period, CancellationToken cancellationToken = default)
    {
        await _context.PayrollPeriods.AddAsync(period, cancellationToken);
    }

    public void Update(PayrollPeriod period)
    {
        _context.PayrollPeriods.Update(period);
    }

    public async Task AddSnapshotAsync(PayrollCalculationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _context.PayrollCalculationSnapshots.AddAsync(snapshot, cancellationToken);
    }

    public async Task<PayrollCalculationSnapshot?> GetSnapshotByLineIdAsync(long payrollRunLineId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollCalculationSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PayrollRunLineId == payrollRunLineId, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
