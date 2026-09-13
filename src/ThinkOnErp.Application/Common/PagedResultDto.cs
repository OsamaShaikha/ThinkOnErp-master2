using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.Common;

/// <summary>
/// Generic container for paginated query results across all modules.
/// </summary>
public sealed class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public long TotalCount { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => PageSize > 0 && PageIndex > 0 && PageIndex < TotalPages;
    public bool HasPreviousPage => PageIndex > 1 && TotalPages > 0 && PageIndex <= TotalPages;

    public PagedResultDto() { }

    public PagedResultDto(IReadOnlyList<T> items, long totalCount, int pageIndex, int pageSize)
    {
        Items = items ?? Array.Empty<T>();
        TotalCount = totalCount;
        PageIndex = pageIndex < 1 ? 1 : pageIndex;
        PageSize = pageSize < 1 ? 20 : pageSize;
    }
}
