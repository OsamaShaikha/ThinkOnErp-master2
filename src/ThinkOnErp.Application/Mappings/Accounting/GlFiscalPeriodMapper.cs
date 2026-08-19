using ThinkOnErp.Application.DTOs.Accounting.FiscalPeriods;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Mappings.Accounting;

public static class GlFiscalPeriodMapper
{
    public static GlFiscalPeriodDto ToDto(GlFiscalPeriod entity)
    {
        return new GlFiscalPeriodDto
        {
            Id = entity.Id,
            FiscalYearId = entity.FiscalYearId,
            PeriodNumber = entity.PeriodNumber,
            PeriodNameAr = entity.PeriodNameAr,
            PeriodNameEn = entity.PeriodNameEn,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            IsAdjustment = entity.IsAdjustment,
            CloseReason = entity.CloseReason,
            ClosedBy = entity.ClosedBy,
            ClosedDate = entity.ClosedDate,
            CreationUser = entity.CreationUser,
            CreationDate = entity.CreationDate,
            UpdateUser = entity.UpdateUser,
            UpdateDate = entity.UpdateDate
        };
    }
}
