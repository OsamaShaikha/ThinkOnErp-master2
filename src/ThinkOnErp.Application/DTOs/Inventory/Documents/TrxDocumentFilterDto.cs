namespace ThinkOnErp.Application.DTOs.Inventory.Documents;

public sealed class TrxDocumentFilterDto
{
    public long BranchId { get; set; }
    public int? DocYear { get; set; }
    public int? DocType { get; set; }
    public int? TrxType { get; set; }
    public int? PartyTypeCode { get; set; }
    public long? PartyId { get; set; }
    public int? StatusCode { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
