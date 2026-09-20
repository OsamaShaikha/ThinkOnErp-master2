using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosPromotionRepository
{
    Task<IReadOnlyList<PosPromotion>> GetActivePromotionsForBranchAsync(long branchId, DateTime date, CancellationToken ct = default);
    Task<PosPromotion?> GetPromotionByIdAsync(long promotionId, CancellationToken ct = default);
    Task<(IReadOnlyList<PosPromotion> Items, long TotalCount)> GetPromotionsPagedAsync(
        long branchId,
        PosPromotionType? promotionType = null,
        bool? isActive = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task AddPromotionAsync(PosPromotion promotion, CancellationToken ct = default);
    Task UpdatePromotionAsync(PosPromotion promotion, CancellationToken ct = default);
    Task DeletePromotionAsync(PosPromotion promotion, CancellationToken ct = default);
    Task<InvPriceList?> GetPriceListByIdAsync(long priceListId, CancellationToken ct = default);
    Task<InvPriceList?> GetDefaultPriceListAsync(long branchId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
