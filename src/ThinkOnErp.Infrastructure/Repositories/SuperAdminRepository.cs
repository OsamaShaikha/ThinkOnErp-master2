using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class SuperAdminRepository : ISuperAdminRepository
{
    private readonly OracleDbContext _context;
    public SuperAdminRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysSuperAdmin>> GetAllAsync() =>
        await _context.SysSuperAdmins.Where(s => s.IsActive).ToListAsync();

    public async Task<SysSuperAdmin?> GetByIdAsync(long id) =>
        await _context.SysSuperAdmins.FindAsync(id);

    public async Task<SysSuperAdmin?> GetByUsernameAsync(string username) =>
        await _context.SysSuperAdmins.FirstOrDefaultAsync(s => s.UserName == username);

    public async Task<SysSuperAdmin?> GetByEmailAsync(string email) =>
        await _context.SysSuperAdmins.FirstOrDefaultAsync(s => s.Email == email);

    public async Task<long> CreateAsync(SysSuperAdmin superAdmin)
    {
        _context.SysSuperAdmins.Add(superAdmin);
        await _context.SaveChangesAsync();
        return superAdmin.Id;
    }

    public async Task<long> UpdateAsync(SysSuperAdmin superAdmin)
    {
        superAdmin.UpdateDate = DateTime.Now;
        _context.SysSuperAdmins.Update(superAdmin);
        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long id)
    {
        var admin = await _context.SysSuperAdmins.FindAsync(id);
        if (admin == null) return 0;
        admin.IsActive = false;
        admin.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> ChangePasswordAsync(long id, string newPasswordHash, string updateUser)
    {
        var admin = await _context.SysSuperAdmins.FindAsync(id);
        if (admin == null) return 0;
        admin.Password = newPasswordHash;
        admin.UpdateUser = updateUser;
        admin.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> Enable2FAAsync(long id, string twoFaSecret, string updateUser)
    {
        var admin = await _context.SysSuperAdmins.FindAsync(id);
        if (admin == null) return 0;
        admin.TwoFaSecret = twoFaSecret;
        admin.TwoFaEnabled = true;
        admin.UpdateUser = updateUser;
        admin.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> Disable2FAAsync(long id, string updateUser)
    {
        var admin = await _context.SysSuperAdmins.FindAsync(id);
        if (admin == null) return 0;
        admin.TwoFaSecret = null;
        admin.TwoFaEnabled = false;
        admin.UpdateUser = updateUser;
        admin.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> UpdateLastLoginAsync(long id)
    {
        var admin = await _context.SysSuperAdmins.FindAsync(id);
        if (admin == null) return 0;
        admin.LastLoginDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<SysSuperAdmin?> AuthenticateAsync(string userName, string passwordHash) =>
        await _context.SysSuperAdmins.FirstOrDefaultAsync(s => s.UserName == userName && s.Password == passwordHash && s.IsActive);

    public async Task SaveRefreshTokenAsync(long superAdminId, string refreshToken, DateTime expiryDate)
    {
        var admin = await _context.SysSuperAdmins.FindAsync(superAdminId);
        if (admin != null)
        {
            admin.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<SysSuperAdmin?> ValidateRefreshTokenAsync(string refreshToken) =>
        await _context.SysSuperAdmins.FirstOrDefaultAsync(s => s.IsActive);
}