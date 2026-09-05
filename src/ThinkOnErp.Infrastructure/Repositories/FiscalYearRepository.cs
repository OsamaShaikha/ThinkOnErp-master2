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
        await _context.SysFiscalYears.ToListAsync();

    public async Task<SysFiscalYear?> GetByIdAsync(long rowId) =>
        await _context.SysFiscalYears.FindAsync(rowId);

    public async Task<List<SysFiscalYear>> GetByBranchIdAsync(long branchId) =>
        await _context.SysFiscalYears.Where(f => f.BranchId == branchId).ToListAsync();

    public async Task<long> CreateAsync(SysFiscalYear fiscalYear)
    {
        _context.SysFiscalYears.Add(fiscalYear);
        await _context.SaveChangesAsync();
        return fiscalYear.Id;
    }

    public async Task<long> UpdateAsync(SysFiscalYear fiscalYear)
    {
        var existing = await _context.SysFiscalYears.FindAsync(fiscalYear.Id);
        if (existing == null) return 0;

        existing.BranchId = fiscalYear.BranchId;
        existing.FiscalYearCode = fiscalYear.FiscalYearCode;
        existing.FiscalYearNameLocal = fiscalYear.FiscalYearNameLocal;
        existing.FiscalYearNameEn = fiscalYear.FiscalYearNameEn;
        existing.StartDate = fiscalYear.StartDate;
        existing.EndDate = fiscalYear.EndDate;
        existing.IsClosed = fiscalYear.IsClosed;
        existing.IsActive = fiscalYear.IsActive;
        existing.UpdateUser = fiscalYear.UpdateUser;
        existing.UpdateDate = DateTime.Now;

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