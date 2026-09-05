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

public sealed class EmploymentEventRepository : IEmploymentEventRepository
{
    private readonly OracleDbContext _context;

    public EmploymentEventRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<EmploymentEvent>> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EmploymentEvents
            .AsNoTracking()
            .Where(e => e.EmployeeCode == employeeCode)
            .OrderByDescending(e => e.EffectiveDate)
            .ThenByDescending(e => e.CreationDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(EmploymentEvent employmentEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employmentEvent);
        await _context.EmploymentEvents.AddAsync(employmentEvent, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
