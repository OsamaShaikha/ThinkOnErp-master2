using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Application.Services.Validation;

public class BusinessValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationErrorItem> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public void AddError(string field, string message, string errorCode = "ERR_VALIDATION_ERROR")
    {
        Errors.Add(new ValidationErrorItem { Field = field, Message = message, ErrorCode = errorCode });
    }

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }
}

public class ValidationErrorItem
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
}

public interface IInvoiceBusinessValidationService
{
    /// <summary>
    /// Performs dynamic business, financial, stock, and fiscal period validations on a warehouse document DTO before creation.
    /// </summary>
    Task<BusinessValidationResult> ValidateWarehouseDocumentDtoAsync(CreateTrxDocumentDto dto, CancellationToken ct = default);

    /// <summary>
    /// Performs dynamic business validations on an existing warehouse document header before posting or updating.
    /// </summary>
    Task<BusinessValidationResult> ValidateWarehouseDocumentAsync(TrxDocumentHeader doc, CancellationToken ct = default);

    /// <summary>
    /// Performs dynamic business, inventory, shift, and pricing validations on a POS order DTO before creation.
    /// </summary>
    Task<BusinessValidationResult> ValidatePosOrderDtoAsync(CreatePosOrderDto dto, CancellationToken ct = default);

    /// <summary>
    /// Performs dynamic validations on an existing POS order entity.
    /// </summary>
    Task<BusinessValidationResult> ValidatePosOrderAsync(PosOrderHeader order, CancellationToken ct = default);

    /// <summary>
    /// Dynamically resolves the effective TaxRate entity for an inventory item without hardcoding.
    /// </summary>
    Task<TaxRate?> GetEffectiveTaxRateAsync(long itemId, long branchId, CancellationToken ct = default);

    /// <summary>
    /// Dynamically resolves the effective tax rate percentage for an inventory item.
    /// </summary>
    Task<decimal> GetEffectiveTaxRatePercentAsync(long itemId, long branchId, CancellationToken ct = default);
}
