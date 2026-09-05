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

public sealed class AssetAssignmentRepository : IAssetAssignmentRepository
{
    private readonly OracleDbContext _context;

    public AssetAssignmentRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<AssetAssignment>> GetAllAssignmentsAsync(
        string? employeeCode = null,
        string? status = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AssetAssignments
            .Include(a => a.Employee)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(a => a.EmployeeCode == employeeCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(a => a.Category == category);
        }

        return await query.OrderByDescending(a => a.IssuedDate).ToListAsync(cancellationToken);
    }

    public async Task<AssetAssignment?> GetAssignmentByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.AssetAssignments
            .Include(a => a.Employee)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<AssetAssignment?> GetAssignmentByTagAsync(string assetTag, CancellationToken cancellationToken = default)
    {
        return await _context.AssetAssignments
            .Include(a => a.Employee)
            .SingleOrDefaultAsync(a => a.AssetTag == assetTag, cancellationToken);
    }

    public async Task AddAssignmentAsync(AssetAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        await _context.AssetAssignments.AddAsync(assignment, cancellationToken);
    }

    public void UpdateAssignment(AssetAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _context.AssetAssignments.Update(assignment);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
