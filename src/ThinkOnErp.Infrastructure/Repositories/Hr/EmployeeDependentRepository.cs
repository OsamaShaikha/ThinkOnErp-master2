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

public sealed class EmployeeDependentRepository : IEmployeeDependentRepository
{
    private readonly OracleDbContext _context;

    public EmployeeDependentRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<EmployeeDependent>> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDependents
            .AsNoTracking()
            .Where(d => d.EmployeeCode == employeeCode && d.IsActive)
            .OrderBy(d => d.DateOfBirth)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeDependent?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDependents.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(EmployeeDependent dependent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependent);
        await _context.EmployeeDependents.AddAsync(dependent, cancellationToken);
    }

    public void Update(EmployeeDependent dependent)
    {
        ArgumentNullException.ThrowIfNull(dependent);
        _context.EmployeeDependents.Update(dependent);
    }

    public void Remove(EmployeeDependent dependent)
    {
        ArgumentNullException.ThrowIfNull(dependent);
        _context.EmployeeDependents.Remove(dependent);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
