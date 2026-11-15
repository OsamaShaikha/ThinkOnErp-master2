using System.Linq;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

/// <summary>
/// Selects ordinary approved adjustments and leave cashout adjustments that remain
/// valid inputs after Accounting reverses a payroll and HR supersedes its run.
/// </summary>
internal static class PayrollCalculationAdjustmentQuery
{
    public static IQueryable<PayrollAdjustment> ForPeriod(
        OracleDbContext db,
        long companyId,
        string payPeriod)
    {
        var approved = db.PayrollAdjustments.Where(a => a.CompanyId == companyId &&
            a.PayPeriod == payPeriod && a.Status == "APPROVED");

        var correctionCashouts =
            from adjustment in db.PayrollAdjustments
            join cashoutLine in db.LeaveCashoutSettlementLines on adjustment.Id equals cashoutLine.PayrollAdjustmentId
            join settlement in db.LeaveCashoutSettlements on cashoutLine.SettlementId equals settlement.Id
            join payrollLine in db.PayrollRunLines on adjustment.PayrollRunLineId equals (long?)payrollLine.Id
            join payrollRun in db.PayrollRuns on payrollLine.PayrollRunId equals payrollRun.Id
            where adjustment.CompanyId == companyId && adjustment.PayPeriod == payPeriod &&
                adjustment.Status == "PROCESSED" && settlement.Status == "POSTED" &&
                payrollRun.Status == "SUPERSEDED"
            select adjustment;

        return approved.Concat(correctionCashouts).Distinct();
    }
}
