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

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly OracleDbContext _context;

    public EmployeeRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<Employee>> GetAllAsync(
        string? departmentCode = null,
        long? branchId = null,
        string? status = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Employees
            .Include(e => e.Position)
            .Include(e => e.Department)
            .Include(e => e.Branch)
            .Include(e => e.Manager)
            .AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(departmentCode))
        {
            query = query.Where(e => e.DepartmentCode == departmentCode);
        }

        if (branchId.HasValue)
        {
            query = query.Where(e => e.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.EmploymentStatus == status);
        }

        return await query
            .OrderBy(e => e.EmployeeCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<Employee?> GetByCodeAsync(string employeeCode, bool includeDetails = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Employees
            .Include(e => e.Position)
            .Include(e => e.Department)
            .Include(e => e.Branch)
            .Include(e => e.Manager)
            .AsQueryable();

        if (includeDetails)
        {
            query = query
                .Include(e => e.Dependents)
                .Include(e => e.Documents)
                .Include(e => e.EmploymentEvents);
        }

        return await query.SingleOrDefaultAsync(e => e.EmployeeCode == employeeCode, cancellationToken);
    }

    public async Task<Employee?> GetByNationalIdAsync(string nationalId, CancellationToken cancellationToken = default)
    {
        return await _context.Employees
            .Include(e => e.Position)
            .Include(e => e.Department)
            .SingleOrDefaultAsync(e => e.NationalId == nationalId, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.Employees.AsNoTracking().AnyAsync(e => e.EmployeeCode == employeeCode, cancellationToken);
    }

    public async Task<bool> NationalIdExistsAsync(string nationalId, string? excludeEmployeeCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Employees.AsNoTracking().Where(e => e.NationalId == nationalId);
        if (!string.IsNullOrWhiteSpace(excludeEmployeeCode))
        {
            query = query.Where(e => e.EmployeeCode != excludeEmployeeCode);
        }
        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employee);
        await _context.Employees.AddAsync(employee, cancellationToken);
    }

    public void Update(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);
        _context.Employees.Update(employee);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
