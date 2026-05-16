using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class FiscalYearRepository : IFiscalYearRepository
{
    private readonly OracleDbContext _context;
    public FiscalYearRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysFiscalYear>> GetAllAsync() =>
        await _context.SysFiscalYears.Where(f => f.IsActive).ToListAsync();

    public async Task<SysFiscalYear?> GetByIdAsync(long rowId) =>
        await _context.SysFiscalYears.FindAsync(rowId);

    public async Task<List<SysFiscalYear>> GetByCompanyIdAsync(long companyId) =>
        await _context.SysFiscalYears.Where(f => f.CompanyId == companyId && f.IsActive).ToListAsync();

    public async Task<List<SysFiscalYear>> GetByBranchIdAsync(long branchId) =>
        await _context.SysFiscalYears.Where(f => f.BranchId == branchId && f.IsActive).ToListAsync();

    public async Task<long> CreateAsync(SysFiscalYear fiscalYear)
    {
        _context.SysFiscalYears.Add(fiscalYear);
        await _context.SaveChangesAsync();
        return fiscalYear.Id;
    }

    public async Task<long> UpdateAsync(SysFiscalYear fiscalYear)
    {
        fiscalYear.UpdateDate = DateTime.Now;
        _context.SysFiscalYears.Update(fiscalYear);
        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long rowId)
    {
        var fy = await _context.SysFiscalYears.FindAsync(rowId);
        if (fy == null) return 0;
        fy.IsActive = false;
        fy.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> CloseAsync(long rowId, string userName)
    {
        var fy = await _context.SysFiscalYears.FindAsync(rowId);
        if (fy == null) return 0;
        fy.IsClosed = true;
        fy.UpdateUser = userName;
        fy.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }
}