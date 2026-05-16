using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly OracleDbContext _context;

    public BranchRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysBranch>> GetAllAsync() =>
        await _context.SysBranches.Where(b => b.IsActive).ToListAsync();

    public async Task<SysBranch?> GetByIdAsync(long rowId) =>
        await _context.SysBranches.FindAsync(rowId);

    public async Task<long> CreateAsync(SysBranch branch)
    {
        _context.SysBranches.Add(branch);
        await _context.SaveChangesAsync();
        return branch.Id;
    }

    public async Task<long> UpdateAsync(SysBranch branch)
    {
        branch.UpdateDate = DateTime.Now;
        _context.SysBranches.Update(branch);
        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long rowId)
    {
        var branch = await _context.SysBranches.FindAsync(rowId);
        if (branch == null) return 0;
        branch.IsActive = false;
        branch.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<List<SysBranch>> GetByCompanyIdAsync(long companyId) =>
        await _context.SysBranches.Where(b => b.CompanyId == companyId && b.IsActive).ToListAsync();

    public async Task<long> UpdateLogoAsync(long rowId, byte[] logo, string userName)
    {
        var branch = await _context.SysBranches.FindAsync(rowId);
        if (branch == null) return 0;
        branch.BranchLogo = logo;
        branch.UpdateUser = userName;
        branch.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<byte[]?> GetLogoAsync(long rowId) =>
        await _context.SysBranches.Where(b => b.Id == rowId).Select(b => b.BranchLogo).FirstOrDefaultAsync();
}