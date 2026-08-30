using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.Documents;

public sealed class UpdateTrxDocumentDto
{
    public DateTime? DocDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? PartyName { get; set; }
    public long? FromWarehouseId { get; set; }
    public long? ToWarehouseId { get; set; }
    public int? PaymentMethodCode { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<CreateTrxDocumentLineDto>? Lines { get; set; }
}
