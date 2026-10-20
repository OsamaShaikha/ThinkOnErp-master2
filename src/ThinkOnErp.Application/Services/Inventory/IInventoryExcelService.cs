namespace ThinkOnErp.Application.Services.Inventory;

public interface IInventoryExcelService
{
    Task<byte[]> ExportAsync(long branchId, bool items, bool template, CancellationToken ct);
    Task<InventoryExcelResult> ImportAsync(Stream workbook, long branchId, bool items, string username, CancellationToken ct);
}

public sealed class InventoryExcelResult
{
    public int TotalRows { get; set; }
    public int Imported { get; set; }
    public List<InventoryExcelError> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public sealed record InventoryExcelError(int Row, string Column, string Message);
