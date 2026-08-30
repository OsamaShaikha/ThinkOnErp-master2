using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvItemRepository
{
    Task<InvItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<InvItem?> GetByCodeAsync(string itemCode, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<InvItem> Items, long TotalCount)> GetAllAsync(
        string? searchKeyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(InvItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(InvItem item, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string itemCode, CancellationToken cancellationToken = default);
    Task<InvItemBarcode?> GetBarcodeByCodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
