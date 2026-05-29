using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

public class SysCodeService : ISysCodeService
{
    private readonly ISysCodeRepository _repository;
    private readonly ILogger<SysCodeService> _logger;
    private static readonly ConcurrentDictionary<(int mgr, int mnr, int lang), SysCode> _cache = new();
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private static readonly Dictionary<(int mgr, int mnr), string> _fallbackValues = new()
    {
        // Actor Types (mgr=7)
        [(7, 1)] = "USER",
        [(7, 2)] = "SUPER_ADMIN",
        [(7, 3)] = "SYSTEM",
        [(7, 4)] = "ANONYMOUS",
        [(7, 5)] = "COMPANY_ADMIN",
        [(7, 6)] = "ADMIN",
        // Event Categories (mgr=6)
        [(6, 1)] = "Authentication",
        [(6, 2)] = "Authorization",
        [(6, 3)] = "DataChange",
        [(6, 4)] = "Configuration",
        [(6, 5)] = "Security",
        [(6, 6)] = "System",
        [(6, 7)] = "Integration",
        [(6, 8)] = "Permission",
        [(6, 9)] = "Exception",
        [(6, 10)] = "Request",
        // Audit Severity (mgr=5)
        [(5, 1)] = "Info",
        [(5, 2)] = "Warning",
        [(5, 3)] = "Error",
        [(5, 4)] = "Critical",
        // Document Categories (mgr=1)
        [(1, 1)] = "Contracts", [(1, 2)] = "Reports", [(1, 3)] = "Invoices",
        [(1, 4)] = "Receipts", [(1, 5)] = "Identification", [(1, 6)] = "Certificates",
        [(1, 7)] = "Financial", [(1, 8)] = "HR", [(1, 9)] = "Legal",
        [(1, 10)] = "Technical", [(1, 11)] = "Marketing", [(1, 12)] = "Other",
        // Owner Types (mgr=2)
        [(2, 1)] = "Company", [(2, 2)] = "Branch", [(2, 3)] = "Super Admin",
        // Threat Types (mgr=3)
        [(3, 1)] = "Brute Force Attack", [(3, 2)] = "SQL Injection", [(3, 3)] = "Cross-Site Scripting",
        [(3, 4)] = "Denial of Service", [(3, 5)] = "Suspicious Login", [(3, 6)] = "Unauthorized Access",
        [(3, 7)] = "Data Exfiltration", [(3, 8)] = "Malware Detected",
        // Threat Severity (mgr=4)
        [(4, 1)] = "Low", [(4, 2)] = "Medium", [(4, 3)] = "High", [(4, 4)] = "Critical",
        // Audit Event Types (mgr=8)
        [(8, 1)] = "Request", [(8, 2)] = "Exception", [(8, 3)] = "Security",
        // Payload Logging Levels (mgr=9)
        [(9, 1)] = "None", [(9, 2)] = "Metadata Only", [(9, 3)] = "Full",
        // System Health Status (mgr=10)
        [(10, 1)] = "Healthy", [(10, 2)] = "Degraded", [(10, 3)] = "Unhealthy", [(10, 4)] = "Unresponsive",
        // Memory Pressure Severity (mgr=11)
        [(11, 1)] = "Normal", [(11, 2)] = "Warning", [(11, 3)] = "Critical", [(11, 4)] = "Severe", [(11, 5)] = "Unknown",
        // Key Types (mgr=12)
        [(12, 1)] = "API Key", [(12, 2)] = "Signing Key", [(12, 3)] = "Encryption Key",
        [(12, 4)] = "Internal Key", [(12, 5)] = "External Key",
        // Alert Types (mgr=13)
        [(13, 1)] = "Security Alert", [(13, 2)] = "Performance Alert", [(13, 3)] = "System Alert", [(13, 4)] = "Business Alert",
        // Languages (mgr=14)
        [(14, 1)] = "Arabic", [(14, 2)] = "English",
        // Audit Status (mgr=15)
        [(15, 1)] = "Unresolved", [(15, 2)] = "In Progress", [(15, 3)] = "Resolved", [(15, 4)] = "Critical",
    };

    private static readonly Dictionary<(int mgr, int mnr), string> _fallbackDescs = new()
    {
        [(7, 1)] = "User", [(7, 2)] = "Super Admin", [(7, 3)] = "System", [(7, 4)] = "Anonymous", [(7, 5)] = "Company Admin", [(7, 6)] = "Admin",
        [(6, 1)] = "Authentication", [(6, 2)] = "Authorization", [(6, 3)] = "Data Change", [(6, 4)] = "Configuration",
        [(6, 5)] = "Security", [(6, 6)] = "System", [(6, 7)] = "Integration", [(6, 8)] = "Permission",
        [(6, 9)] = "Exception", [(6, 10)] = "Request",
        [(5, 1)] = "Info", [(5, 2)] = "Warning", [(5, 3)] = "Error", [(5, 4)] = "Critical",
        // Audit Status (mgr=15)
        [(15, 1)] = "Unresolved", [(15, 2)] = "In Progress", [(15, 3)] = "Resolved", [(15, 4)] = "Critical",
    };

    public SysCodeService(ISysCodeRepository repository, ILogger<SysCodeService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<string> GetCodeValueAsync(int codeMgr, int codeMnr, int codeLang = 2)
    {
        var code = await GetCodeAsync(codeMgr, codeMnr, codeLang);
        if (code != null && !string.IsNullOrEmpty(code.CodeValue))
            return code.CodeValue;

        if (_fallbackValues.TryGetValue((codeMgr, codeMnr), out var fallback))
            return fallback;

        _logger.LogWarning("No SysCode or fallback for ({Mgr}, {Mnr})", codeMgr, codeMnr);
        return string.Empty;
    }

    public string GetCodeValue(int codeMgr, int codeMnr)
    {
        var key = (codeMgr, codeMnr, 2);
        if (_cache.TryGetValue(key, out var code) && !string.IsNullOrEmpty(code.CodeValue))
            return code.CodeValue;

        if (_fallbackValues.TryGetValue((codeMgr, codeMnr), out var fallback))
            return fallback;

        _logger.LogWarning("No SysCode or fallback for ({Mgr}, {Mnr})", codeMgr, codeMnr);
        return string.Empty;
    }

    public async Task<string> GetCodeDescAsync(int codeMgr, int codeMnr, int codeLang = 2)
    {
        var code = await GetCodeAsync(codeMgr, codeMnr, codeLang);
        if (code != null && !string.IsNullOrEmpty(code.CodeDesc))
            return code.CodeDesc;

        if (_fallbackDescs.TryGetValue((codeMgr, codeMnr), out var fallback))
            return fallback;

        _logger.LogWarning("No SysCode or fallback desc for ({Mgr}, {Mnr})", codeMgr, codeMnr);
        return string.Empty;
    }

    public string GetCodeDesc(int codeMgr, int codeMnr)
    {
        var key = (codeMgr, codeMnr, 2);
        if (_cache.TryGetValue(key, out var code) && !string.IsNullOrEmpty(code.CodeDesc))
            return code.CodeDesc;

        if (_fallbackDescs.TryGetValue((codeMgr, codeMnr), out var fallback))
            return fallback;

        _logger.LogWarning("No SysCode or fallback desc for ({Mgr}, {Mnr})", codeMgr, codeMnr);
        return string.Empty;
    }

    public async Task<List<SysCode>> GetCodesByManagerAsync(int codeMgr)
    {
        if (NeedsRefresh())
            await RefreshCacheAsync();

        return _cache.Values
            .Where(c => c.CodeMgr == codeMgr)
            .OrderBy(c => c.CodeMnr)
            .ThenBy(c => c.CodeLang)
            .ToList();
    }

    public async Task<bool> ValidateCodeValueAsync(int codeMgr, string value)
    {
        var codes = await GetCodesByManagerAsync(codeMgr);
        return codes.Any(c =>
            c.CodeValue?.Equals(value, StringComparison.OrdinalIgnoreCase) == true ||
            c.CodeDesc?.Equals(value, StringComparison.OrdinalIgnoreCase) == true);
    }

    public Task InvalidateCacheAsync()
    {
        _cache.Clear();
        _lastRefresh = DateTime.MinValue;
        return Task.CompletedTask;
    }

    private async Task<SysCode?> GetCodeAsync(int codeMgr, int codeMnr, int codeLang)
    {
        var key = (codeMgr, codeMnr, codeLang);

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        if (NeedsRefresh())
            await RefreshCacheAsync();

        _cache.TryGetValue(key, out cached);
        return cached;
    }

    private bool NeedsRefresh() => _cache.IsEmpty || DateTime.UtcNow - _lastRefresh > CacheDuration;

    private async Task RefreshCacheAsync()
    {
        if (_cache.IsEmpty == false && DateTime.UtcNow - _lastRefresh <= CacheDuration)
            return;

        await _refreshLock.WaitAsync();
        try
        {
            if (_cache.IsEmpty == false && DateTime.UtcNow - _lastRefresh <= CacheDuration)
                return;

            var allCodes = await _repository.GetAllAsync();
            _cache.Clear();
            foreach (var code in allCodes)
                _cache.TryAdd((code.CodeMgr, code.CodeMnr, code.CodeLang), code);

            _lastRefresh = DateTime.UtcNow;
            _logger.LogDebug("Refreshed SysCode cache with {Count} entries", allCodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh SysCode cache, using fallback values");
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}