using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

public class PosBranchSettingRepository : IPosBranchSettingRepository
{
    private readonly OracleDbContext _context;

    public PosBranchSettingRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<PosBranchSetting?> GetByBranchIdAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.PosBranchSettings
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.BranchId == branchId && s.IsActive, ct);
    }

    public async Task<IReadOnlyList<PosBranchSetting>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.PosBranchSettings
            .AsNoTracking()
            .Include(s => s.Branch)
            .Where(s => s.IsActive)
            .OrderBy(s => s.BranchId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(PosBranchSetting setting, CancellationToken ct = default)
    {
        await _context.PosBranchSettings.AddAsync(setting, ct);
    }

    public Task UpdateAsync(PosBranchSetting setting, CancellationToken ct = default)
    {
        _context.PosBranchSettings.Update(setting);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
