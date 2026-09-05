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

public sealed class CompensationRepository : ICompensationRepository
{
    private readonly OracleDbContext _context;

    public CompensationRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<SalaryComponent>> GetAllComponentsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.SalaryComponents.AsNoTracking();
        if (activeOnly) query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.ComponentType).ThenBy(c => c.ComponentCode).ToListAsync(cancellationToken);
    }

    public async Task<SalaryComponent?> GetComponentByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.SalaryComponents.SingleOrDefaultAsync(c => c.ComponentCode == code, cancellationToken);
    }

    public async Task<bool> ComponentCodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.SalaryComponents.AsNoTracking().AnyAsync(c => c.ComponentCode == code, cancellationToken);
    }

    public async Task AddComponentAsync(SalaryComponent component, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(component);
        await _context.SalaryComponents.AddAsync(component, cancellationToken);
    }

    public void UpdateComponent(SalaryComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _context.SalaryComponents.Update(component);
    }

    public async Task<EmployeeSalaryStructure?> GetActiveStructureAsync(string employeeCode, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeSalaryStructures
            .Include(s => s.Lines)
                .ThenInclude(l => l.Component)
            .AsNoTracking()
            .Where(s => s.EmployeeCode == employeeCode && s.IsActive)
            .Where(s => s.EffectiveFrom <= effectiveDate && (s.EffectiveTo == null || s.EffectiveTo >= effectiveDate))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeSalaryStructure>> GetStructureHistoryAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeSalaryStructures
            .Include(s => s.Lines)
                .ThenInclude(l => l.Component)
            .AsNoTracking()
            .Where(s => s.EmployeeCode == employeeCode)
            .OrderByDescending(s => s.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddStructureAsync(EmployeeSalaryStructure structure, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(structure);
        await _context.EmployeeSalaryStructures.AddAsync(structure, cancellationToken);
    }

    public void UpdateStructure(EmployeeSalaryStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        _context.EmployeeSalaryStructures.Update(structure);
    }

    public async Task<IReadOnlyList<SalaryRevision>> GetRevisionsByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.SalaryRevisions
            .AsNoTracking()
            .Where(r => r.EmployeeCode == employeeCode)
            .OrderByDescending(r => r.EffectiveDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRevisionAsync(SalaryRevision revision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);
        await _context.SalaryRevisions.AddAsync(revision, cancellationToken);
    }

    public async Task<IReadOnlyList<EmploymentContract>> GetContractsByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EmploymentContracts
            .AsNoTracking()
            .Where(c => c.EmployeeCode == employeeCode)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmploymentContract?> GetActiveContractAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EmploymentContracts
            .AsNoTracking()
            .Where(c => c.EmployeeCode == employeeCode && c.Status == "ACTIVE" && c.IsActive)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmploymentContract?> GetContractByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.EmploymentContracts.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddContractAsync(EmploymentContract contract, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);
        await _context.EmploymentContracts.AddAsync(contract, cancellationToken);
    }

    public void UpdateContract(EmploymentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        _context.EmploymentContracts.Update(contract);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
