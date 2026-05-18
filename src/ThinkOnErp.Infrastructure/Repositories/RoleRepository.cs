using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly OracleDbContext _context;

    public RoleRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<List<SysRole>> GetAllAsync()
    {
        return await _context.SysRoles
            .Where(r => r.IsActive)
            .ToListAsync();
    }

    public async Task<SysRole?> GetByIdAsync(long rowId)
    {
        return await _context.SysRoles.FindAsync(rowId);
    }

    public async Task<long> CreateAsync(SysRole role)
    {
        _context.SysRoles.Add(role);
        await _context.SaveChangesAsync();
        return role.Id;
    }

    public async Task<long> UpdateAsync(SysRole role)
    {
        var existingRole = await _context.SysRoles.FindAsync(role.Id);
        if (existingRole == null) return 0;

        existingRole.RoleNameAr = role.RoleNameAr;
        existingRole.RoleNameEn = role.RoleNameEn;
        existingRole.Note = role.Note;
        existingRole.UpdateUser = role.UpdateUser;
        existingRole.UpdateDate = DateTime.Now;

        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long rowId)
    {
        var role = await _context.SysRoles.FindAsync(rowId);
        if (role == null) return 0;
        
        role.IsActive = false;
        role.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }
}
