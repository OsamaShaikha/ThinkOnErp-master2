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

public sealed class EndOfServiceRepository : IEndOfServiceRepository
{
    private readonly OracleDbContext _context;

    public EndOfServiceRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<EndOfServiceProvisionAccrual>> GetProvisionsByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EndOfServiceProvisionAccruals
            .AsNoTracking()
            .Where(p => p.EmployeeCode == employeeCode)
            .OrderByDescending(p => p.PayPeriod)
            .ToListAsync(cancellationToken);
    }

    public async Task<EndOfServiceProvisionAccrual?> GetProvisionAsync(string employeeCode, string period, CancellationToken cancellationToken = default)
    {
        return await _context.EndOfServiceProvisionAccruals
            .SingleOrDefaultAsync(p => p.EmployeeCode == employeeCode && p.PayPeriod == period, cancellationToken);
    }

    public async Task AddProvisionAsync(EndOfServiceProvisionAccrual provision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provision);
        await _context.EndOfServiceProvisionAccruals.AddAsync(provision, cancellationToken);
    }

    public void UpdateProvision(EndOfServiceProvisionAccrual provision)
    {
        ArgumentNullException.ThrowIfNull(provision);
        _context.EndOfServiceProvisionAccruals.Update(provision);
    }

    public async Task<IReadOnlyList<FinalSettlement>> GetAllSettlementsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.FinalSettlements
            .Include(s => s.Employee)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => s.Status == status);
        }

        return await query
            .OrderByDescending(s => s.TerminationDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<FinalSettlement?> GetSettlementByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.FinalSettlements
            .Include(s => s.Employee)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<FinalSettlement?> GetSettlementByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.FinalSettlements
            .Include(s => s.Employee)
            .OrderByDescending(s => s.TerminationDate)
            .FirstOrDefaultAsync(s => s.EmployeeCode == employeeCode, cancellationToken);
    }

    public async Task AddSettlementAsync(FinalSettlement settlement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        await _context.FinalSettlements.AddAsync(settlement, cancellationToken);
    }

    public void UpdateSettlement(FinalSettlement settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        _context.FinalSettlements.Update(settlement);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
