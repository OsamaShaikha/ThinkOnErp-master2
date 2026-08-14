using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class GlCostCenterRepository : IGlCostCenterRepository
{
    private readonly OracleDbContext _context;

    public GlCostCenterRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<GlCostCenter>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.GlCostCenters
            .AsNoTracking()
            .OrderBy(c => c.CostCenterCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<GlCostCenter?> GetByCodeAsync(string costCenterCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(costCenterCode);

        return await _context.GlCostCenters
            .Include(c => c.InverseParentCostCenter)
            .SingleOrDefaultAsync(c => c.CostCenterCode == costCenterCode, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string costCenterCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(costCenterCode);

        return await _context.GlCostCenters
            .AsNoTracking()
            .AnyAsync(c => c.CostCenterCode == costCenterCode, cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(string costCenterCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(costCenterCode);

        return await _context.GlCostCenters
            .AsNoTracking()
            .AnyAsync(c => c.ParentCostCenterCode == costCenterCode, cancellationToken);
    }

    public async Task AddAsync(GlCostCenter costCenter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(costCenter);

        await _context.GlCostCenters.AddAsync(costCenter, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
