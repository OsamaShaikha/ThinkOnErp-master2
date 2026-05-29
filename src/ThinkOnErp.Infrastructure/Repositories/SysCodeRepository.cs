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
}
