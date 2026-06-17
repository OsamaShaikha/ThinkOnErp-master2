using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class SysCodeRepository : ISysCodeRepository
{
    private readonly OracleDbContext _context;

    public SysCodeRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<List<SysCode>> GetAllAsync()
    {
        return await _context.SysCodes
            .OrderBy(c => c.CodeMgr)
            .ThenBy(c => c.CodeMnr)
            .ThenBy(c => c.CodeLang)
            .ToListAsync();
    }

    public async Task<List<SysCode>> GetByCodeMgrAsync(int codeMgr)
    {
        return await _context.SysCodes
            .Where(c => c.CodeMgr == codeMgr)
            .OrderBy(c => c.CodeMnr)
            .ThenBy(c => c.CodeLang)
            .ToListAsync();
    }

    public async Task<SysCode?> GetByCodeMgrAndCodeMnrAndCodeLangAsync(int codeMgr, int codeMnr, int codeLang)
    {
        return await _context.SysCodes
            .FirstOrDefaultAsync(c => c.CodeMgr == codeMgr && c.CodeMnr == codeMnr && c.CodeLang == codeLang);
    }

    public async Task<List<SysCode>> GetActiveByCodeMgrAsync(int codeMgr)
    {
        return await _context.SysCodes
            .Where(c => c.CodeMgr == codeMgr && c.IsActive == 1)
            .OrderBy(c => c.CodeMnr)
            .ThenBy(c => c.CodeLang)
            .ToListAsync();
    }

    public async Task<List<int>> GetDistinctCodeMgrsAsync()
    {
        return await _context.SysCodes
            .Select(c => c.CodeMgr)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();
    }

    public async Task<SysCode> AddAsync(SysCode code)
    {
        _context.SysCodes.Add(code);
        await _context.SaveChangesAsync();
        return code;
    }

    public async Task UpdateAsync(SysCode code)
    {
        _context.SysCodes.Update(code);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int codeMgr, int codeMnr, int codeLang)
    {
        var code = await _context.SysCodes
            .FirstOrDefaultAsync(c => c.CodeMgr == codeMgr && c.CodeMnr == codeMnr && c.CodeLang == codeLang);
        if (code != null)
        {
            _context.SysCodes.Remove(code);
            await _context.SaveChangesAsync();
        }
    }
}
