using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class TicketAttachmentRepository : ITicketAttachmentRepository
{
    private readonly ThinkOnErpDbContext _context;
    public TicketAttachmentRepository(ThinkOnErpDbContext context) => _context = context;

    public async Task<List<SysTicketAttachment>> GetByTicketIdAsync(long ticketId) =>
        await _context.SysTicketAttachments.Where(a => a.TicketId == ticketId).ToListAsync();

    public async Task<SysTicketAttachment?> GetByIdAsync(long rowId) =>
        await _context.SysTicketAttachments.FindAsync(rowId);

    public async Task<long> CreateAsync(SysTicketAttachment attachment)
    {
        _context.SysTicketAttachments.Add(attachment);
        await _context.SaveChangesAsync();
        return attachment.RowId;
    }

    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        var attachment = await _context.SysTicketAttachments.FindAsync(rowId);
        if (attachment == null) return 0;
        _context.SysTicketAttachments.Remove(attachment);
        return await _context.SaveChangesAsync();
    }

    public async Task<int> GetAttachmentCountAsync(long ticketId) =>
        await _context.SysTicketAttachments.CountAsync(a => a.TicketId == ticketId);

    public async Task<long> GetTotalAttachmentSizeAsync(long ticketId) =>
        await _context.SysTicketAttachments.Where(a => a.TicketId == ticketId).SumAsync(a => a.FileSize);

    public async Task<List<SysTicketAttachment>> GetAttachmentMetadataAsync(long ticketId) =>
        await _context.SysTicketAttachments.Where(a => a.TicketId == ticketId).Select(a => new SysTicketAttachment
        {
            RowId = a.RowId,
            TicketId = a.TicketId,
            FileName = a.FileName,
            FileSize = a.FileSize,
            MimeType = a.MimeType,
            CreationUser = a.CreationUser,
            CreationDate = a.CreationDate
        }).ToListAsync();

    public async Task<byte[]?> GetFileContentAsync(long rowId) =>
        await _context.SysTicketAttachments.Where(a => a.RowId == rowId).Select(a => a.FileContent).FirstOrDefaultAsync();

    public async Task<bool> CanAddAttachmentAsync(long ticketId, long newFileSize)
    {
        var count = await GetAttachmentCountAsync(ticketId);
        var totalSize = await GetTotalAttachmentSizeAsync(ticketId);
        return count < SysTicketAttachment.MaxAttachmentsPerTicket && (totalSize + newFileSize) <= SysTicketAttachment.MaxFileSizeBytes;
    }

    public async Task<List<SysTicketAttachment>> GetByFileTypeAsync(string mimeType, long? companyId = null, long? branchId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.SysTicketAttachments.Where(a => a.MimeType == mimeType).AsQueryable();
        if (fromDate.HasValue) query = query.Where(a => a.CreationDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(a => a.CreationDate <= toDate.Value);
        return await query.ToListAsync();
    }

    public async Task<Dictionary<string, object>> GetAttachmentStatisticsAsync(long? companyId = null, long? branchId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.SysTicketAttachments.AsQueryable();
        if (fromDate.HasValue) query = query.Where(a => a.CreationDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(a => a.CreationDate <= toDate.Value);
        return new Dictionary<string, object>
        {
            ["TotalAttachments"] = await query.CountAsync(),
            ["TotalSize"] = await query.SumAsync(a => a.FileSize),
            ["ByType"] = await query.GroupBy(a => a.MimeType).Select(g => new { Type = g.Key, Count = g.Count() }).ToListAsync()
        };
    }
}

public class SavedSearchRepository : ISavedSearchRepository
{
    private readonly ThinkOnErpDbContext _context;
    public SavedSearchRepository(ThinkOnErpDbContext context) => _context = context;

    public async Task<long> CreateAsync(SysSavedSearch savedSearch)
    {
        _context.SysSavedSearches.Add(savedSearch);
        await _context.SaveChangesAsync();
        return savedSearch.RowId;
    }

    public async Task<long> UpdateAsync(SysSavedSearch savedSearch)
    {
        savedSearch.UpdateDate = DateTime.Now;
        _context.SysSavedSearches.Update(savedSearch);
        return await _context.SaveChangesAsync();
    }

    public async Task<List<SysSavedSearch>> GetByUserIdAsync(long userId) =>
        await _context.SysSavedSearches.Where(s => (s.UserId == userId || s.IsPublic) && s.IsActive).ToListAsync();

    public async Task<SysSavedSearch?> GetByIdAsync(long rowId) =>
        await _context.SysSavedSearches.FindAsync(rowId);

    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        var search = await _context.SysSavedSearches.FindAsync(rowId);
        if (search == null) return 0;
        search.IsActive = false;
        search.UpdateUser = userName;
        search.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task IncrementUsageAsync(long rowId)
    {
        var search = await _context.SysSavedSearches.FindAsync(rowId);
        if (search != null)
        {
            search.UsageCount++;
            search.LastUsedDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }
}

public class SearchAnalyticsRepository : ISearchAnalyticsRepository
{
    private readonly ThinkOnErpDbContext _context;
    public SearchAnalyticsRepository(ThinkOnErpDbContext context) => _context = context;

    public async Task<long> LogSearchAsync(SysSearchAnalytics analytics)
    {
        _context.SysSearchAnalytics.Add(analytics);
        await _context.SaveChangesAsync();
        return analytics.RowId;
    }

    public async Task<List<TopSearchResult>> GetTopSearchesAsync(int daysBack = 30, int topCount = 10)
    {
        var cutoff = DateTime.Now.AddDays(-daysBack);
        return await _context.SysSearchAnalytics
            .Where(s => s.SearchDate >= cutoff && s.SearchTerm != null)
            .GroupBy(s => s.SearchTerm)
            .Select(g => new TopSearchResult
            {
                SearchTerm = g.Key!,
                SearchCount = g.Count(),
                AvgResults = g.Average(s => (double)s.ResultCount),
                AvgExecutionTime = g.Average(s => (double)s.ExecutionTimeMs)
            })
            .OrderByDescending(r => r.SearchCount)
            .Take(topCount)
            .ToListAsync();
    }

    public async Task<List<SysSearchAnalytics>> GetUserSearchHistoryAsync(long userId, int daysBack = 30)
    {
        var cutoff = DateTime.Now.AddDays(-daysBack);
        return await _context.SysSearchAnalytics
            .Where(s => s.UserId == userId && s.SearchDate >= cutoff)
            .OrderByDescending(s => s.SearchDate)
            .ToListAsync();
    }

    public async Task<List<SearchPerformanceMetric>> GetSearchPerformanceAsync(int daysBack = 7)
    {
        var cutoff = DateTime.Now.AddDays(-daysBack);
        return await _context.SysSearchAnalytics
            .Where(s => s.SearchDate >= cutoff)
            .GroupBy(s => s.SearchDate.Date)
            .Select(g => new SearchPerformanceMetric
            {
                SearchDay = g.Key,
                TotalSearches = g.Count(),
                AvgResults = g.Average(s => (double)s.ResultCount),
                AvgExecutionTime = g.Average(s => (double)s.ExecutionTimeMs),
                MaxExecutionTime = g.Max(s => s.ExecutionTimeMs),
                MinExecutionTime = g.Min(s => s.ExecutionTimeMs)
            })
            .OrderBy(m => m.SearchDay)
            .ToListAsync();
    }
}

public class TicketConfigRepository : ITicketConfigRepository
{
    private readonly ThinkOnErpDbContext _context;
    public TicketConfigRepository(ThinkOnErpDbContext context) => _context = context;

    public async Task<List<SysTicketConfig>> GetAllAsync() =>
        await _context.SysTicketConfigs.Where(c => c.IsActive).ToListAsync();

    public async Task<SysTicketConfig?> GetByKeyAsync(string key) =>
        await _context.SysTicketConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key && c.IsActive);

    public async Task<List<SysTicketConfig>> GetByTypeAsync(string configType) =>
        await _context.SysTicketConfigs.Where(c => c.ConfigType == configType && c.IsActive).ToListAsync();

    public async Task<long> CreateAsync(SysTicketConfig config)
    {
        _context.SysTicketConfigs.Add(config);
        await _context.SaveChangesAsync();
        return config.RowId;
    }

    public async Task<long> UpdateAsync(SysTicketConfig config)
    {
        config.UpdateDate = DateTime.Now;
        _context.SysTicketConfigs.Update(config);
        return await _context.SaveChangesAsync();
    }

    public async Task<bool> UpdateByKeyAsync(string configKey, string configValue, string updateUser)
    {
        var config = await _context.SysTicketConfigs.FirstOrDefaultAsync(c => c.ConfigKey == configKey);
        if (config == null) return false;
        config.ConfigValue = configValue;
        config.UpdateUser = updateUser;
        config.UpdateDate = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long rowId, string updateUser)
    {
        var config = await _context.SysTicketConfigs.FindAsync(rowId);
        if (config == null) return false;
        config.IsActive = false;
        config.UpdateUser = updateUser;
        config.UpdateDate = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }
}