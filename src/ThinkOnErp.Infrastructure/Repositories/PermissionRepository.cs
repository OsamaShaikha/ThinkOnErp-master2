using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class PermissionRepository : IPermissionRepository
{
    private readonly OracleDbContext _context;
    public PermissionRepository(OracleDbContext context) => _context = context;

    public async Task<bool> CheckUserPermissionAsync(long userId, string screenCode, string action)
    {
        var user = await _context.SysUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive) return false;
        if (user.IsAdmin) return true;

        var screen = await _context.SysScreens.FirstOrDefaultAsync(s => s.ScreenCode == screenCode && s.IsActive);
        if (screen == null) return false;

        // Check user-level permission overrides first
        var userPermission = await _context.SysUserScreenPermissions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ScreenId == screen.RowId);

        if (userPermission != null)
        {
            return action.ToUpper() switch
            {
                "VIEW" => userPermission.CanView,
                "INSERT" => userPermission.CanInsert,
                "UPDATE" => userPermission.CanUpdate,
                "DELETE" => userPermission.CanDelete,
                _ => false
            };
        }

        // Check role-level permissions
        var userRoles = await _context.SysUserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        foreach (var roleId in userRoles)
        {
            var rolePermission = await _context.SysRoleScreenPermissions
                .FirstOrDefaultAsync(p => p.RoleId == roleId && p.ScreenId == screen.RowId);

            if (rolePermission != null)
            {
                var allowed = action.ToUpper() switch
                {
                    "VIEW" => rolePermission.CanView,
                    "INSERT" => rolePermission.CanInsert,
                    "UPDATE" => rolePermission.CanUpdate,
                    "DELETE" => rolePermission.CanDelete,
                    _ => false
                };
                if (allowed) return true;
            }
        }

        return false;
    }

    public async Task<List<SysUserRole>> GetUserRolesAsync(long userId) =>
        await _context.SysUserRoles.Where(ur => ur.UserId == userId).ToListAsync();

    public async Task AssignRoleToUserAsync(long userId, long roleId, long? assignedBy, string creationUser)
    {
        var userRole = new SysUserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedBy = assignedBy,
            AssignedDate = DateTime.Now,
            CreationUser = creationUser,
            CreationDate = DateTime.Now
        };
        _context.SysUserRoles.Add(userRole);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveRoleFromUserAsync(long userId, long roleId)
    {
        var userRole = await _context.SysUserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (userRole != null)
        {
            _context.SysUserRoles.Remove(userRole);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<SysRoleScreenPermission>> GetRoleScreenPermissionsAsync(long roleId) =>
        await _context.SysRoleScreenPermissions.Where(p => p.RoleId == roleId).ToListAsync();

    public async Task SetRoleScreenPermissionAsync(long roleId, long screenId, bool canView, bool canInsert, bool canUpdate, bool canDelete, string creationUser)
    {
        var existing = await _context.SysRoleScreenPermissions
            .FirstOrDefaultAsync(p => p.RoleId == roleId && p.ScreenId == screenId);
        
        if (existing != null)
        {
            existing.CanView = canView;
            existing.CanInsert = canInsert;
            existing.CanUpdate = canUpdate;
            existing.CanDelete = canDelete;
            existing.UpdateUser = creationUser;
            existing.UpdateDate = DateTime.Now;
        }
        else
        {
            _context.SysRoleScreenPermissions.Add(new SysRoleScreenPermission
            {
                RoleId = roleId,
                ScreenId = screenId,
                CanView = canView,
                CanInsert = canInsert,
                CanUpdate = canUpdate,
                CanDelete = canDelete,
                CreationUser = creationUser,
                CreationDate = DateTime.Now
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task DeleteRoleScreenPermissionAsync(long roleId, long screenId)
    {
        var perm = await _context.SysRoleScreenPermissions
            .FirstOrDefaultAsync(p => p.RoleId == roleId && p.ScreenId == screenId);
        if (perm != null)
        {
            _context.SysRoleScreenPermissions.Remove(perm);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<SysUserScreenPermission>> GetUserScreenPermissionsAsync(long userId) =>
        await _context.SysUserScreenPermissions.Where(p => p.UserId == userId).ToListAsync();

    public async Task SetUserScreenPermissionAsync(long userId, long screenId, bool canView, bool canInsert, bool canUpdate, bool canDelete, long? assignedBy, string? notes, string creationUser)
    {
        var existing = await _context.SysUserScreenPermissions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ScreenId == screenId);
        
        if (existing != null)
        {
            existing.CanView = canView;
            existing.CanInsert = canInsert;
            existing.CanUpdate = canUpdate;
            existing.CanDelete = canDelete;
            existing.Notes = notes;
            existing.UpdateUser = creationUser;
            existing.UpdateDate = DateTime.Now;
        }
        else
        {
            _context.SysUserScreenPermissions.Add(new SysUserScreenPermission
            {
                UserId = userId,
                ScreenId = screenId,
                CanView = canView,
                CanInsert = canInsert,
                CanUpdate = canUpdate,
                CanDelete = canDelete,
                AssignedBy = assignedBy,
                Notes = notes,
                CreationUser = creationUser,
                CreationDate = DateTime.Now
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task DeleteUserScreenPermissionAsync(long userId, long screenId)
    {
        var perm = await _context.SysUserScreenPermissions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ScreenId == screenId);
        if (perm != null)
        {
            _context.SysUserScreenPermissions.Remove(perm);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<SysCompanySystem>> GetCompanySystemsAsync(long companyId) =>
        await _context.SysCompanySystems.Where(cs => cs.CompanyId == companyId).ToListAsync();

    public async Task SetCompanySystemAsync(long companyId, long systemId, bool isAllowed, long? grantedBy, string? notes, string creationUser)
    {
        var existing = await _context.SysCompanySystems
            .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.SystemId == systemId);
        
        if (existing != null)
        {
            existing.IsAllowed = isAllowed;
            existing.GrantedBy = grantedBy;
            existing.Notes = notes;
            existing.GrantedDate = DateTime.Now;
            existing.UpdateUser = creationUser;
            existing.UpdateDate = DateTime.Now;
        }
        else
        {
            _context.SysCompanySystems.Add(new SysCompanySystem
            {
                CompanyId = companyId,
                SystemId = systemId,
                IsAllowed = isAllowed,
                GrantedBy = grantedBy,
                GrantedDate = DateTime.Now,
                Notes = notes,
                CreationUser = creationUser,
                CreationDate = DateTime.Now
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task<List<SysBranchSystem>> GetBranchSystemsAsync(long branchId) =>
        await _context.SysBranchSystems.Where(bs => bs.BranchId == branchId).ToListAsync();

    public async Task SetBranchSystemAsync(long branchId, long systemId, bool isAllowed, long? grantedBy, string? notes, string creationUser)
    {
        var existing = await _context.SysBranchSystems
            .FirstOrDefaultAsync(bs => bs.BranchId == branchId && bs.SystemId == systemId);

        if (existing != null)
        {
            existing.IsAllowed = isAllowed;
            existing.GrantedBy = grantedBy;
            existing.Notes = notes;
            existing.GrantedDate = DateTime.Now;
            existing.UpdateUser = creationUser;
            existing.UpdateDate = DateTime.Now;
        }
        else
        {
            _context.SysBranchSystems.Add(new SysBranchSystem
            {
                BranchId = branchId,
                SystemId = systemId,
                IsAllowed = isAllowed,
                GrantedBy = grantedBy,
                GrantedDate = DateTime.Now,
                Notes = notes,
                CreationUser = creationUser,
                CreationDate = DateTime.Now
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task<List<SysBranchScreenPermission>> GetBranchScreenPermissionsAsync(long branchId) =>
        await _context.SysBranchScreenPermissions.Where(bp => bp.BranchId == branchId).ToListAsync();

    public async Task GrantSystemScreensToBranchAsync(long branchId, long systemId, long? grantedBy, string creationUser)
    {
        var screens = await _context.SysScreens
            .Where(s => s.SystemId == systemId && s.IsActive)
            .ToListAsync();

        foreach (var screen in screens)
        {
            var existing = await _context.SysBranchScreenPermissions
                .FirstOrDefaultAsync(bp => bp.BranchId == branchId && bp.ScreenId == screen.RowId);

            if (existing != null)
            {
                existing.CanView = true;
                existing.CanInsert = true;
                existing.CanUpdate = true;
                existing.CanDelete = true;
                existing.GrantedBy = grantedBy;
                existing.GrantedDate = DateTime.Now;
                existing.UpdateUser = creationUser;
                existing.UpdateDate = DateTime.Now;
            }
            else
            {
                _context.SysBranchScreenPermissions.Add(new SysBranchScreenPermission
                {
                    BranchId = branchId,
                    ScreenId = screen.RowId,
                    CanView = true,
                    CanInsert = true,
                    CanUpdate = true,
                    CanDelete = true,
                    GrantedBy = grantedBy,
                    GrantedDate = DateTime.Now,
                    CreationUser = creationUser,
                    CreationDate = DateTime.Now
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    public async Task<List<SysCompanyScreenPermission>> GetCompanyScreenPermissionsAsync(long companyId) =>
        await _context.SysCompanyScreenPermissions.Where(cp => cp.CompanyId == companyId).ToListAsync();

    public async Task GrantSystemScreensToCompanyAsync(long companyId, long systemId, long? grantedBy, string creationUser)
    {
        var screens = await _context.SysScreens
            .Where(s => s.SystemId == systemId && s.IsActive)
            .ToListAsync();

        foreach (var screen in screens)
        {
            var existing = await _context.SysCompanyScreenPermissions
                .FirstOrDefaultAsync(cp => cp.CompanyId == companyId && cp.ScreenId == screen.RowId);

            if (existing != null)
            {
                existing.CanView = true;
                existing.CanInsert = true;
                existing.CanUpdate = true;
                existing.CanDelete = true;
                existing.GrantedBy = grantedBy;
                existing.GrantedDate = DateTime.Now;
                existing.UpdateUser = creationUser;
                existing.UpdateDate = DateTime.Now;
            }
            else
            {
                _context.SysCompanyScreenPermissions.Add(new SysCompanyScreenPermission
                {
                    CompanyId = companyId,
                    ScreenId = screen.RowId,
                    CanView = true,
                    CanInsert = true,
                    CanUpdate = true,
                    CanDelete = true,
                    GrantedBy = grantedBy,
                    GrantedDate = DateTime.Now,
                    CreationUser = creationUser,
                    CreationDate = DateTime.Now
                });
            }
        }
        await _context.SaveChangesAsync();
    }
}