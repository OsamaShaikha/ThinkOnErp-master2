using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ThinkOnErpDbContext _context;

    public UserRepository(ThinkOnErpDbContext context) => _context = context;

    public async Task<List<SysUser>> GetAllAsync() =>
        await _context.SysUsers.Where(u => u.IsActive).ToListAsync();

    public async Task<SysUser?> GetByIdAsync(long rowId) =>
        await _context.SysUsers.FindAsync(rowId);

    public async Task<long> CreateAsync(SysUser user)
    {
        _context.SysUsers.Add(user);
        await _context.SaveChangesAsync();
        return user.Id;
    }

    public async Task<long> UpdateAsync(SysUser user)
    {
        user.UpdateDate = DateTime.Now;
        _context.SysUsers.Update(user);
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
        await _context.SysUsers.Where(u => u.BranchId == branchId && u.IsActive).ToListAsync();

    public async Task<List<SysUser>> GetByCompanyIdAsync(long companyId) =>
        await _context.SysUsers
            .Where(u => u.IsActive && u.BranchId.HasValue && 
                _context.SysBranches.Any(b => b.Id == u.BranchId.Value && b.CompanyId == companyId))
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
}