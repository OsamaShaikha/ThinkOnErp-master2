using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvVariantRepository
{
    // Attributes
    Task<IReadOnlyList<InvItemAttribute>> GetAllAttributesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<InvItemAttribute?> GetAttributeByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<InvItemAttribute?> GetAttributeByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddAttributeAsync(InvItemAttribute attribute, CancellationToken cancellationToken = default);
    Task UpdateAttributeAsync(InvItemAttribute attribute, CancellationToken cancellationToken = default);

    // Attribute Values
    Task<InvItemAttributeValue?> GetAttributeValueByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddAttributeValueAsync(InvItemAttributeValue value, CancellationToken cancellationToken = default);

    // Variants
    Task<(IReadOnlyList<InvItemVariant> Items, int TotalCount)> GetVariantsPagedAsync(
        int pageNumber,
        int pageSize,
        long? itemId = null,
        string? search = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvItemVariant>> GetVariantsByItemIdAsync(long itemId, CancellationToken cancellationToken = default);
    Task<InvItemVariant?> GetVariantByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<InvItemVariant?> GetVariantBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<InvItemVariant?> GetVariantByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<bool> ExistsSkuAsync(string sku, long? excludeVariantId = null, CancellationToken cancellationToken = default);
    Task AddVariantAsync(InvItemVariant variant, CancellationToken cancellationToken = default);
    Task UpdateVariantAsync(InvItemVariant variant, CancellationToken cancellationToken = default);
    Task DeleteVariantAsync(InvItemVariant variant, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
