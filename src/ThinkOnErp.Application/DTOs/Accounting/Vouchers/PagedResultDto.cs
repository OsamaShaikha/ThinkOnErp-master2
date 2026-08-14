namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

public sealed class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public long TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => PageIndex < TotalPages;
    public bool HasPreviousPage => PageIndex > 1;
}
