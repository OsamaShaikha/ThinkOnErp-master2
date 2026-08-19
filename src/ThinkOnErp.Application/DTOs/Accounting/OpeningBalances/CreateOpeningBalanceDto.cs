namespace ThinkOnErp.Application.DTOs.Accounting.OpeningBalances;

/// <summary>Request DTO — creates a new Opening Balance in Draft status.</summary>
public sealed class CreateOpeningBalanceDto
{
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public DateTime AsOfDate { get; set; }
    public string? Description { get; set; }

    /// <summary>Initial lines to add. Can be empty; lines can be added later.</summary>
    public List<OpeningBalanceLineDto> Lines { get; set; } = new();
}
