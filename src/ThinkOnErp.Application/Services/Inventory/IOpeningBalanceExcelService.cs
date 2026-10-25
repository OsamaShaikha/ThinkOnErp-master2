namespace ThinkOnErp.Application.Services.Inventory;

public interface IOpeningBalanceExcelService
{
    Task<byte[]> ExportAsync(long branchId, long? batchId, bool template, CancellationToken ct);
    Task<OpeningBalanceExcelResult> ImportAsync(Stream workbook, long branchId, long fiscalYearId, DateTime batchDate, string? description, string username, CancellationToken ct);
}

public sealed class OpeningBalanceExcelResult
{
    public int TotalRows { get; set; }
    public int Imported { get; set; }
    public long? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public string? Status { get; set; }
    public List<InventoryExcelError> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}
