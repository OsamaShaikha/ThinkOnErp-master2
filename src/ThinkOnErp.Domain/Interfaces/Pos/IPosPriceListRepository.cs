using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosPriceListRepository
{
    Task<IReadOnlyList<PosPriceList>> GetPriceListsByBranchAsync(long branchId, PosOrderType? orderType = null, bool? isActive = null, CancellationToken ct = default);
    Task<PosPriceList?> GetPriceListByIdAsync(long id, CancellationToken ct = default);
    Task<PosPriceList?> GetByCodeAsync(long branchId, string code, CancellationToken ct = default);
    Task<PosPriceList?> GetDefaultPriceListAsync(long branchId, CancellationToken ct = default);
    Task AddPriceListAsync(PosPriceList priceList, CancellationToken ct = default);
    Task UpdatePriceListAsync(PosPriceList priceList, CancellationToken ct = default);
    Task DeletePriceListAsync(PosPriceList priceList, CancellationToken ct = default);
    Task<PosPriceListItem?> GetPriceListItemByIdAsync(long priceListId, long itemId, CancellationToken ct = default);
    Task AddPriceListItemAsync(PosPriceListItem item, CancellationToken ct = default);
    Task UpdatePriceListItemAsync(PosPriceListItem item, CancellationToken ct = default);
    Task DeletePriceListItemAsync(PosPriceListItem item, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
