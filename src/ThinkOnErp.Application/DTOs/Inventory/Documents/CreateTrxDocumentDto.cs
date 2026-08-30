using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Inventory.Documents;

public sealed class CreateTrxDocumentDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public int DocYear { get; set; }

    [Required]
    public int DocType { get; set; }

    [Required]
    public int TrxType { get; set; }

    public string? DocNo { get; set; }

    [Required]
    public DateTime DocDate { get; set; } = DateTime.UtcNow;

    public DateTime? DueDate { get; set; }

    public int PartyTypeCode { get; set; }
    public long? PartyId { get; set; }
    public string? PartyName { get; set; }

    public long? FromWarehouseId { get; set; }
    public long? ToWarehouseId { get; set; }

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [Range(0.000001, 999999)]
    public decimal ExchangeRate { get; set; } = 1;

    public int PaymentMethodCode { get; set; } = 1;

    public decimal DiscountAmount { get; set; }

    public long? BaseBranchId { get; set; }
    public int? BaseDocYear { get; set; }
    public int? BaseDocType { get; set; }
    public long? BaseDocId { get; set; }

    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Document must contain at least one line.")]
    public List<CreateTrxDocumentLineDto> Lines { get; set; } = new();
}

public sealed class CreateTrxDocumentLineDto
{
    [Required]
    public long ItemId { get; set; }

    public string? ItemDescription { get; set; }

    [Required]
    public string UomCode { get; set; } = string.Empty;

    public decimal UomFactor { get; set; } = 1;

    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }

    public long? FromBinId { get; set; }
    public long? ToBinId { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? GlAccountCode { get; set; }
    public int? BaseLineNo { get; set; }
}
