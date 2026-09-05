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

public sealed class PositionRepository : IPositionRepository
{
    private readonly OracleDbContext _context;

    public PositionRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<Position>> GetAllAsync(string? departmentCode = null, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Positions
            .Include(p => p.Department)
            .Include(p => p.JobGrade)
            .Include(p => p.ReportsToPosition)
            .Include(p => p.Employees)
            .AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(departmentCode))
        {
            query = query.Where(p => p.DepartmentCode == departmentCode);
        }

        return await query
            .OrderBy(p => p.DepartmentCode)
            .ThenBy(p => p.PositionCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<Position?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Positions
            .Include(p => p.Department)
            .Include(p => p.JobGrade)
            .Include(p => p.ReportsToPosition)
            .Include(p => p.Employees)
            .Include(p => p.DirectReportPositions)
            .SingleOrDefaultAsync(p => p.PositionCode == code, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Positions.AsNoTracking().AnyAsync(p => p.PositionCode == code, cancellationToken);
    }

    public async Task<bool> HasReportsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Positions.AsNoTracking().AnyAsync(p => p.ReportsToPositionCode == code, cancellationToken);
    }

    public async Task<bool> HasEmployeesAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Employees.AsNoTracking().AnyAsync(e => e.PositionCode == code, cancellationToken);
    }

    public async Task AddAsync(Position position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        await _context.Positions.AddAsync(position, cancellationToken);
    }

    public void Update(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);
        _context.Positions.Update(position);
    }

    public void Remove(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);
        _context.Positions.Remove(position);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
