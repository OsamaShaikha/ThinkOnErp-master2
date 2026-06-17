using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class BranchSystemRepository : IBranchSystemRepository
{
    private readonly OracleDbContext _context;
    public BranchSystemRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysSystem>> GetSystemsByBranchIdAsync(long branchId)
    {
        return await _context.Set<SysBranchSystem>()
            .Where(bs => bs.BranchId == branchId && bs.RevokedDate == null)
            .Include(bs => bs.System)
            .Where(bs => bs.System!.IsActive)
            .Select(bs => bs.System!)
            .ToListAsync();
    }

    public async Task AssignSystemsToBranchAsync(long branchId, List<long> systemIds, long? grantedBy)
    {
        var existing = await _context.Set<SysBranchSystem>()
            .Where(bs => bs.BranchId == branchId)
            .ToListAsync();

        _context.Set<SysBranchSystem>().RemoveRange(existing);

        var newAssignments = systemIds.Select(systemId => new SysBranchSystem
        {
            BranchId = branchId,
            SystemId = systemId,
            GrantedBy = grantedBy,
            GrantedDate = DateTime.UtcNow,
            CreationUser = grantedBy?.ToString() ?? "system",
            CreationDate = DateTime.UtcNow
        });
        _context.Set<SysBranchSystem>().AddRange(newAssignments);

        await _context.SaveChangesAsync();
    }

    public async Task RemoveSystemFromBranchAsync(long branchId, long systemId)
    {
        var assignment = await _context.Set<SysBranchSystem>()
            .FirstOrDefaultAsync(bs => bs.BranchId == branchId && bs.SystemId == systemId);

        if (assignment != null)
        {
            _context.Set<SysBranchSystem>().Remove(assignment);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasSystemAsync(long branchId, long systemId)
    {
        return await _context.Set<SysBranchSystem>()
            .AnyAsync(bs => bs.BranchId == branchId && bs.SystemId == systemId && bs.RevokedDate == null);
    }
}
