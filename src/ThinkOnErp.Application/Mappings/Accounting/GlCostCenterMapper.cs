using ThinkOnErp.Application.DTOs.Accounting.CostCenters;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Mappings.Accounting;

public static class GlCostCenterMapper
{
    public static GlCostCenterDto ToDto(GlCostCenter entity)
    {
        return new GlCostCenterDto
        {
            CostCenterCode = entity.CostCenterCode,
            ParentCostCenterCode = entity.ParentCostCenterCode,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            CostCenterLevel = entity.CostCenterLevel,
            CostCenterType = entity.CostCenterType,
            IsPostable = entity.IsPostable,
            IsActive = entity.IsActive,
            CreationUser = entity.CreationUser,
            CreationDate = entity.CreationDate,
            UpdateUser = entity.UpdateUser,
            UpdateDate = entity.UpdateDate
        };
    }

    public static GlCostCenterTreeDto ToTreeDto(GlCostCenter entity)
    {
        return new GlCostCenterTreeDto
        {
            CostCenterCode = entity.CostCenterCode,
            ParentCostCenterCode = entity.ParentCostCenterCode,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            CostCenterLevel = entity.CostCenterLevel,
            CostCenterType = entity.CostCenterType,
            IsPostable = entity.IsPostable,
            IsActive = entity.IsActive,
            Children = new List<GlCostCenterTreeDto>()
        };
    }
}
