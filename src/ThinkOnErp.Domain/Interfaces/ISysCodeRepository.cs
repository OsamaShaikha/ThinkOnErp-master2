using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface ISysCodeRepository
{
    Task<List<SysCode>> GetAllAsync();
    Task<List<SysCode>> GetByCodeMgrAsync(int codeMgr);
    Task<SysCode?> GetByCodeMgrAndCodeMnrAndCodeLangAsync(int codeMgr, int codeMnr, int codeLang);
    Task<List<SysCode>> GetActiveByCodeMgrAsync(int codeMgr);
}
