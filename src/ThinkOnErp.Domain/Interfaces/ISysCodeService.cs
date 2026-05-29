using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface ISysCodeService
{
    Task<string> GetCodeValueAsync(int codeMgr, int codeMnr, int codeLang = 2);
    string GetCodeValue(int codeMgr, int codeMnr);
    Task<string> GetCodeDescAsync(int codeMgr, int codeMnr, int codeLang = 2);
    string GetCodeDesc(int codeMgr, int codeMnr);
    Task<List<SysCode>> GetCodesByManagerAsync(int codeMgr);
    Task<bool> ValidateCodeValueAsync(int codeMgr, string value);
    Task InvalidateCacheAsync();
}