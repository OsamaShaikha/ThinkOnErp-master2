using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvPriceListRepository
{
    Task<IReadOnlyList<InvPriceList>> GetPriceListsByBranchAsync(long branchId, PosOrderType? orderType = null, bool? isActive = null, CancellationToken ct = default);
    Task<InvPriceList?> GetPriceListByIdAsync(long id, CancellationToken ct = default);
    Task<InvPriceList?> GetByCodeAsync(long branchId, string code, CancellationToken ct = default);
    Task<InvPriceList?> GetDefaultPriceListAsync(long branchId, CancellationToken ct = default);
    Task AddPriceListAsync(InvPriceList priceList, CancellationToken ct = default);
    Task UpdatePriceListAsync(InvPriceList priceList, CancellationToken ct = default);
    Task DeletePriceListAsync(InvPriceList priceList, CancellationToken ct = default);
    Task<InvPriceListItem?> GetPriceListItemByIdAsync(long priceListId, long itemId, CancellationToken ct = default);
    Task AddPriceListItemAsync(InvPriceListItem item, CancellationToken ct = default);
    Task UpdatePriceListItemAsync(InvPriceListItem item, CancellationToken ct = default);
    Task DeletePriceListItemAsync(InvPriceListItem item, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
