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
        _context = context;
    }

    public async Task<(IReadOnlyList<Employee> Items, int TotalCount)> GetEmployeesAsync(
        string? searchKeyword = null,
        string? departmentCode = null,
        string? status = null,
        long? branchId = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Employees
            .Include(e => e.Dependents)
            .Include(e => e.SalaryStructures.Where(s => s.IsActive))
                .ThenInclude(s => s.Lines.Where(l => l.IsActive))
                    .ThenInclude(l => l.Component)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var kw = searchKeyword.Trim().ToLower();
            query = query.Where(e =>
                e.EmployeeCode.ToLower().Contains(kw) ||
                e.NameLocal.ToLower().Contains(kw) ||
                e.NameEn.ToLower().Contains(kw) ||
                e.NationalId.ToLower().Contains(kw) ||
                (e.Phone != null && e.Phone.Contains(kw)) ||
                (e.Email != null && e.Email.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(departmentCode))
        {
            query = query.Where(e => e.DepartmentCode == departmentCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.EmploymentStatus == status);
        }

        if (branchId.HasValue)
        {
            query = query.Where(e => e.BranchId == branchId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.EmployeeCode)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Employee?> GetEmployeeByCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.Employees
            .Include(e => e.Dependents)
            .Include(e => e.SalaryStructures)
                .ThenInclude(s => s.Lines)
                    .ThenInclude(l => l.Component)
            .FirstOrDefaultAsync(e => e.EmployeeCode == employeeCode, cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.Employees.AnyAsync(e => e.EmployeeCode == employeeCode, cancellationToken);
    }

    public async Task<bool> ExistsByNationalIdAsync(string nationalId, string? excludeEmployeeCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Employees.Where(e => e.NationalId == nationalId);
        if (!string.IsNullOrWhiteSpace(excludeEmployeeCode))
        {
            query = query.Where(e => e.EmployeeCode != excludeEmployeeCode);
        }
        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        await _context.Employees.AddAsync(employee, cancellationToken);
    }

    public Task UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        _context.Employees.Update(employee);
        return Task.CompletedTask;
    }

    public Task DeleteEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        _context.Employees.Remove(employee);
        return Task.CompletedTask;
    }

    public async Task<EmployeeDependent?> GetDependentByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDependents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task AddDependentAsync(EmployeeDependent dependent, CancellationToken cancellationToken = default)
    {
        await _context.EmployeeDependents.AddAsync(dependent, cancellationToken);
    }

    public Task DeleteDependentAsync(EmployeeDependent dependent, CancellationToken cancellationToken = default)
    {
        _context.EmployeeDependents.Remove(dependent);
        return Task.CompletedTask;
    }

    public async Task<SalaryStructure?> GetActiveSalaryStructureAsync(string employeeCode, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        return await _context.SalaryStructures
            .Include(s => s.Lines.Where(l => l.IsActive))
                .ThenInclude(l => l.Component)
            .Where(s => s.EmployeeCode == employeeCode && s.IsActive && s.EffectiveFrom <= asOfDate && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalaryStructure>> GetSalaryStructuresAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.SalaryStructures
            .Include(s => s.Lines)
                .ThenInclude(l => l.Component)
            .Where(s => s.EmployeeCode == employeeCode)
            .OrderByDescending(s => s.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddSalaryStructureAsync(SalaryStructure structure, CancellationToken cancellationToken = default)
    {
        await _context.SalaryStructures.AddAsync(structure, cancellationToken);
    }

    public async Task<IReadOnlyList<SalaryComponent>> GetSalaryComponentsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SalaryComponents
            .OrderBy(c => c.ComponentCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<SalaryComponent?> GetSalaryComponentByCodeAsync(string componentCode, CancellationToken cancellationToken = default)
    {
        return await _context.SalaryComponents.FirstOrDefaultAsync(c => c.ComponentCode == componentCode, cancellationToken);
    }

    public async Task AddSalaryComponentAsync(SalaryComponent component, CancellationToken cancellationToken = default)
    {
        await _context.SalaryComponents.AddAsync(component, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
