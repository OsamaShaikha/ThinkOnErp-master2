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

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly OracleDbContext _context;

    public DepartmentRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<Department>> GetAllAsync(long? branchId = null, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Departments
            .Include(d => d.ParentDepartment)
            .Include(d => d.Branch)
            .Include(d => d.CostCenter)
            .Include(d => d.Employees)
            .Include(d => d.Positions)
            .AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(d => d.IsActive);
        }

        if (branchId.HasValue)
        {
            query = query.Where(d => d.BranchId == branchId.Value);
        }

        return await query
            .OrderBy(d => d.DepartmentCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<Department?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Departments
            .Include(d => d.ParentDepartment)
            .Include(d => d.Branch)
            .Include(d => d.CostCenter)
            .Include(d => d.Employees)
            .Include(d => d.Positions)
            .SingleOrDefaultAsync(d => d.DepartmentCode == code, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Departments.AsNoTracking().AnyAsync(d => d.DepartmentCode == code, cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Departments.AsNoTracking().AnyAsync(d => d.ParentDepartmentCode == code, cancellationToken);
    }

    public async Task<bool> HasEmployeesAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Employees.AsNoTracking().AnyAsync(e => e.DepartmentCode == code, cancellationToken);
    }

    public async Task AddAsync(Department department, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(department);
        await _context.Departments.AddAsync(department, cancellationToken);
    }

    public void Update(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        _context.Departments.Update(department);
    }

    public void Remove(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        _context.Departments.Remove(department);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
