using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

/// <summary>
/// Restores the tenant identity to transient API/domain fields after materialization.
/// Company ownership is carried by the selected schema and is never persisted on HR rows.
/// </summary>
internal static class HrTenantCompanyStamp
{
    public static T Stamp<T>(this T entity, long companyId) where T : class
    {
        switch (entity)
        {
            case AttendancePolicy value: value.CompanyId = companyId; break;
            case AttendanceTimeSettings value: value.CompanyId = companyId; break;
            case BranchAttendanceLocation value: value.CompanyId = companyId; break;
            case DeductionPolicy value: value.CompanyId = companyId; break;
            case EndOfServicePolicy value: value.CompanyId = companyId; break;
            case EndOfServiceSettlement value: value.CompanyId = companyId; break;
            case HrAccountingHandoff value: value.CompanyId = companyId; break;
            case OvertimeRule value: value.CompanyId = companyId; break;
            case PayrollAdjustment value: value.CompanyId = companyId; break;
            case PayrollCurrencyPrecision value: value.CompanyId = companyId; break;
            case PayrollPeriod value: value.CompanyId = companyId; break;
            case ProrationPolicy value: value.CompanyId = companyId; break;
            case PublicHoliday value: value.CompanyId = companyId; break;
            case SSCPolicy value: value.CompanyId = companyId; break;
            case ShortLeavePolicy value: value.CompanyId = companyId; break;
            case ShortLeaveRequest value: value.CompanyId = companyId; break;
            case TaxPolicy value: value.CompanyId = companyId; break;
            case WorkCalendar value: value.CompanyId = companyId; break;
        }
        return entity;
    }

    public static IReadOnlyList<T> StampAll<T>(this IReadOnlyList<T> entities, long companyId) where T : class
    {
        foreach (var entity in entities) entity.Stamp(companyId);
        return entities;
    }
}
