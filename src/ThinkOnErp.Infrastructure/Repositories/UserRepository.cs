using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly OracleDbContext _context;

    public UserRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysUser>> GetAllAsync() =>
        await _context.SysUsers.Where(u => u.IsActive).ToListAsync();

    public async Task<SysUser?> GetByIdAsync(long rowId)
    {
        var user = await _context.SysUsers.FindAsync(rowId);
        if (user != null)
        {
            var primaryBranch = await _context.SysUserBranches
                .FirstOrDefaultAsync(ub => ub.UserId == rowId && ub.IsPrimary);
            user.BranchId = primaryBranch?.BranchId;
        }
        return user;
    }

    public async Task<long> CreateAsync(SysUser user)
    {
        _context.SysUsers.Add(user);
        await _context.SaveChangesAsync();
        return user.Id;
    }

    public async Task<long> UpdateAsync(SysUser user)
    {
        var existing = await _context.SysUsers.FindAsync(user.Id);
        if (existing == null) return 0;

        existing.FullNameLocal = user.FullNameLocal;
        existing.FullNameEn = user.FullNameEn;
        existing.UserName = user.UserName;
        if (!string.IsNullOrEmpty(user.Password))
        {
            existing.Password = user.Password;
        }
        existing.Phone = user.Phone;
        existing.Phone2 = user.Phone2;
        existing.RoleId = user.RoleId;
        existing.CompanyId = user.CompanyId;
        existing.Email = user.Email;
        existing.IsAdmin = user.IsAdmin;
        existing.DefaultLang = user.DefaultLang;
        existing.UpdateUser = user.UpdateUser;
        existing.UpdateDate = DateTime.Now;

        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long rowId)
    {
        var user = await _context.SysUsers.FindAsync(rowId);
        if (user == null) return 0;
        user.IsActive = false;
        user.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<List<SysUser>> GetByBranchIdAsync(long branchId) =>
        await _context.SysUserBranches
            .Where(ub => ub.BranchId == branchId && ub.User!.IsActive)
            .Select(ub => ub.User!)
            .ToListAsync();

    public async Task<List<SysUser>> GetByCompanyIdAsync(long companyId) =>
        await _context.SysUserBranches
            .Where(ub => ub.User!.IsActive && ub.Branch!.CompanyId == companyId)
            .Select(ub => ub.User!)
            .Distinct()
            .ToListAsync();

    public async Task<int> ForceLogoutAsync(long userId, string adminUser)
    {
        var user = await _context.SysUsers.FindAsync(userId);
        if (user == null) return 0;
        user.ForceLogoutDate = DateTime.Now;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdateUser = adminUser;
        user.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<int> ChangePasswordAsync(long userId, string newPasswordHash, string updateUser)
    {
        var user = await _context.SysUsers.FindAsync(userId);
        if (user == null) return 0;
        user.Password = newPasswordHash;
        user.UpdateUser = updateUser;
        user.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<List<SysUser>> GetAdminUsersAsync() =>
        await _context.SysUsers.Where(u => u.IsAdmin && u.IsActive).ToListAsync();

    public async Task AssignToBranchesAsync(long userId, List<long> branchIds, long primaryBranchId, string assignedBy)
    {
        var existing = _context.SysUserBranches.Where(ub => ub.UserId == userId);
        _context.SysUserBranches.RemoveRange(existing);

        foreach (var branchId in branchIds)
        {
            _context.SysUserBranches.Add(new SysUserBranch
            {
                UserId = userId,
                BranchId = branchId,
                IsPrimary = (branchId == primaryBranchId),
                AssignedBy = assignedBy,
                AssignedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task<List<long>> GetUserBranchIdsAsync(long userId) =>
        await _context.SysUserBranches
            .Where(ub => ub.UserId == userId)
            .Select(ub => ub.BranchId)
            .ToListAsync();

    public async Task<long> GetBranchUserCountAsync(long branchId) =>
        await _context.SysUserBranches
            .CountAsync(ub => ub.BranchId == branchId && ub.User!.IsActive);

    public async Task<List<SysUserBranch>> GetUserBranchesAsync(long userId) =>
        await _context.SysUserBranches
            .Where(ub => ub.UserId == userId)
            .ToListAsync();

    public async Task<List<SysUserBranch>> GetUserBranchesForUsersAsync(List<long> userIds) =>
        await _context.SysUserBranches
            .Where(ub => userIds.Contains(ub.UserId))
            .ToListAsync();
}