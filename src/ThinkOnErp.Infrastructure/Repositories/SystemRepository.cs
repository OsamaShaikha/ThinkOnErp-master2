using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class SystemRepository : ISystemRepository
{
    private readonly OracleDbContext _context;
    public SystemRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysSystem>> GetAllSystemsAsync() =>
        await _context.SysSystems.Where(s => s.IsActive).ToListAsync();

    public async Task<SysSystem?> GetSystemByIdAsync(long systemId) =>
        await _context.SysSystems.FindAsync(systemId);

    public async Task<long> CreateSystemAsync(SysSystem system)
    {
        _context.SysSystems.Add(system);
        await _context.SaveChangesAsync();
        return system.Id;
    }

    public async Task UpdateSystemAsync(SysSystem system)
    {
        system.UpdateDate = DateTime.Now;
        _context.SysSystems.Update(system);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteSystemAsync(long systemId, string updateUser)
    {
        var system = await _context.SysSystems.FindAsync(systemId);
        if (system != null)
        {
            system.IsActive = false;
            system.UpdateUser = updateUser;
            system.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }
}