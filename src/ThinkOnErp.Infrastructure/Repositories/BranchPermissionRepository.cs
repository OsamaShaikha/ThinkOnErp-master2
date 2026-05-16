using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for branch-level permissions using Entity Framework Core.
/// Manages SYS_BRANCH_SYSTEMS and SYS_BRANCH_SCREEN_PERMISSIONS tables.
/// </summary>
public class BranchPermissionRepository : IBranchPermissionRepository
{
    private readonly OracleDbContext _dbContext;

    public BranchPermissionRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    #region Branch System Permissions

    public async Task<List<SysBranchSystem>> GetBranchSystemsAsync(long branchId)
    {
        return await _dbContext.SysBranchSystems
            .Where(s => s.BranchId == branchId)
            .ToListAsync();
    }

    public async Task<SysBranchSystem?> GetBranchSystemAsync(long branchId, long systemId)
    {
        return await _dbContext.SysBranchSystems
            .FirstOrDefaultAsync(s => s.BranchId == branchId && s.SystemId == systemId);
    }

    public async Task<long> GrantSystemAccessAsync(long branchId, long systemId, long grantedBy, string? notes, string creationUser)
    {
        var entity = new SysBranchSystem
        {
            BranchId = branchId,
            SystemId = systemId,
            IsAllowed = true,
            GrantedBy = grantedBy,
            GrantedDate = DateTime.UtcNow,
            Notes = notes,
            CreationUser = creationUser,
            CreationDate = DateTime.UtcNow
        };
        _dbContext.SysBranchSystems.Add(entity);
        await _dbContext.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<long> RevokeSystemAccessAsync(long branchId, long systemId, string updateUser)
    {
        var entity = await _dbContext.SysBranchSystems
            .FirstOrDefaultAsync(s => s.BranchId == branchId && s.SystemId == systemId);
        if (entity == null) return 0;
        entity.IsAllowed = false;
        entity.RevokedDate = DateTime.UtcNow;
        entity.UpdateUser = updateUser;
        entity.UpdateDate = DateTime.UtcNow;
        return await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> IsBranchSystemAllowedAsync(long branchId, long systemId)
    {
        return await _dbContext.SysBranchSystems
            .AnyAsync(s => s.BranchId == branchId && s.SystemId == systemId && s.IsAllowed);
    }

    #endregion

    #region Branch Screen Permissions

    public async Task<List<SysBranchScreen>> GetBranchScreensAsync(long branchId)
    {
        return await _dbContext.SysBranchScreenPermissions
            .Where(p => p.BranchId == branchId)
            .Select(p => new SysBranchScreen
            {
                RowId = p.Id,
                BranchId = p.BranchId,
                ScreenId = p.ScreenId,
                IsAllowed = true,
                GrantedBy = p.GrantedBy,
                GrantedDate = p.GrantedDate,
                Notes = p.Notes,
                CreationUser = p.CreationUser,
                CreationDate = p.CreationDate,
                UpdateUser = p.UpdateUser,
                UpdateDate = p.UpdateDate
            })
            .ToListAsync();
    }

    public async Task<SysBranchScreen?> GetBranchScreenAsync(long branchId, long screenId)
    {
        return await _dbContext.SysBranchScreenPermissions
            .Where(p => p.BranchId == branchId && p.ScreenId == screenId)
            .Select(p => new SysBranchScreen
            {
                RowId = p.Id,
                BranchId = p.BranchId,
                ScreenId = p.ScreenId,
                IsAllowed = true,
                GrantedBy = p.GrantedBy,
                GrantedDate = p.GrantedDate,
                Notes = p.Notes,
                CreationUser = p.CreationUser,
                CreationDate = p.CreationDate,
                UpdateUser = p.UpdateUser,
                UpdateDate = p.UpdateDate
            })
            .FirstOrDefaultAsync();
    }

    public async Task<long> GrantScreenAccessAsync(long branchId, long screenId, long grantedBy, string? notes, string creationUser)
    {
        var entity = new SysBranchScreenPermission
        {
            BranchId = branchId,
            ScreenId = screenId,
            CanView = true,
            CanInsert = true,
            CanUpdate = true,
            CanDelete = true,
            GrantedBy = grantedBy,
            GrantedDate = DateTime.UtcNow,
            Notes = notes,
            CreationUser = creationUser,
            CreationDate = DateTime.UtcNow
        };
        _dbContext.SysBranchScreenPermissions.Add(entity);
        await _dbContext.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<long> RevokeScreenAccessAsync(long branchId, long screenId, string updateUser)
    {
        var entity = await _dbContext.SysBranchScreenPermissions
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.ScreenId == screenId);
        if (entity == null) return 0;
        _dbContext.SysBranchScreenPermissions.Remove(entity);
        return await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> IsBranchScreenAllowedAsync(long branchId, long screenId)
    {
        return await _dbContext.SysBranchScreenPermissions
            .AnyAsync(p => p.BranchId == branchId && p.ScreenId == screenId);
    }

    #endregion

    #region Bulk Operations

    public async Task<int> GrantMultipleSystemsAsync(long branchId, List<long> systemIds, long grantedBy, string creationUser)
    {
        foreach (var systemId in systemIds)
        {
            _dbContext.SysBranchSystems.Add(new SysBranchSystem
            {
                BranchId = branchId,
                SystemId = systemId,
                IsAllowed = true,
                GrantedBy = grantedBy,
                GrantedDate = DateTime.UtcNow,
                CreationUser = creationUser,
                CreationDate = DateTime.UtcNow
            });
        }
        return await _dbContext.SaveChangesAsync();
    }

    public async Task<int> GrantMultipleScreensAsync(long branchId, List<long> screenIds, long grantedBy, string creationUser)
    {
        foreach (var screenId in screenIds)
        {
            _dbContext.SysBranchScreenPermissions.Add(new SysBranchScreenPermission
            {
                BranchId = branchId,
                ScreenId = screenId,
                CanView = true,
                CanInsert = true,
                CanUpdate = true,
                CanDelete = true,
                GrantedBy = grantedBy,
                GrantedDate = DateTime.UtcNow,
                CreationUser = creationUser,
                CreationDate = DateTime.UtcNow
            });
        }
        return await _dbContext.SaveChangesAsync();
    }

    #endregion

    #region Get All Systems/Screens

    public async Task<List<SysSystem>> GetAllSystemsAsync()
    {
        return await _dbContext.SysSystems.ToListAsync();
    }

    public async Task<List<SysScreen>> GetAllScreensAsync()
    {
        return await _dbContext.SysScreens.ToListAsync();
    }

    public async Task<List<SysScreen>> GetScreensBySystemAsync(long systemId)
    {
        return await _dbContext.SysScreens
            .Where(s => s.SystemId == systemId)
            .ToListAsync();
    }

    #endregion
}
