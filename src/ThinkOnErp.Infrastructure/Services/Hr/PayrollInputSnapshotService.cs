using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

// Queries detached rows so a second capture observes database changes rather than EF's tracker.
public sealed class PayrollInputSnapshotService(OracleDbContext db) : IPayrollInputSnapshotService
{
    public async Task<string> CaptureAsync(PayrollPeriod period, long? branchId, CancellationToken cancellationToken = default)
    {
        var storedPeriod = await db.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == period.Id, cancellationToken);
        if (storedPeriod.StartDate != period.StartDate || storedPeriod.EndDate != period.EndDate || storedPeriod.Status != period.Status)
            throw new InvalidOperationException("Payroll period changed. Reload the period before continuing.");
        var employees = db.Employees.AsNoTracking().Where(e => !branchId.HasValue || e.BranchId == branchId);
        var codes = employees.Select(e => e.EmployeeCode);
        var structures = db.SalaryStructures.AsNoTracking().Where(s => codes.Contains(s.EmployeeCode));
        var structureIds = structures.Select(s => s.Id);
        var payload = new
        {
            version = 3,
            period = new { period.CompanyId, period.PeriodCode, period.StartDate, period.EndDate, period.Status },
            employees = await employees.OrderBy(e => e.EmployeeCode).ToListAsync(cancellationToken),
            dependents = await db.EmployeeDependents.AsNoTracking().Where(d => codes.Contains(d.EmployeeCode)).OrderBy(d => d.Id).ToListAsync(cancellationToken),
            prorationPolicies = await db.ProrationPolicies.AsNoTracking().Where(p => p.CompanyId == period.CompanyId).OrderBy(p => p.Id).ToListAsync(cancellationToken),
            sscPolicies = await db.SSCPolicies.AsNoTracking().Where(p => p.CompanyId == period.CompanyId).OrderBy(p => p.Id).ToListAsync(cancellationToken),
            deductionPolicies = await db.DeductionPolicies.AsNoTracking().Where(p => p.CompanyId == period.CompanyId).OrderBy(p => p.Id).ToListAsync(cancellationToken),
            taxPolicies = await db.TaxPolicies.AsNoTracking().Where(p => p.CompanyId == period.CompanyId).OrderBy(p => p.Id).ToListAsync(cancellationToken),
            taxBrackets = await db.TaxBrackets.AsNoTracking().Where(b => db.TaxPolicies.Where(p => p.CompanyId == period.CompanyId).Select(p => p.Id).Contains(b.TaxPolicyId)).OrderBy(b => b.Id).ToListAsync(cancellationToken),
            overtimeRules = await db.OvertimeRules.AsNoTracking()
                .Where(r => r.CompanyId == period.CompanyId && r.EffectiveFrom <= period.EndDate &&
                    (!r.EffectiveTo.HasValue || r.EffectiveTo.Value >= period.StartDate))
                .OrderBy(r => r.Id).ToListAsync(cancellationToken),
            currencyPrecision = await db.PayrollCurrencyPrecisions.AsNoTracking()
                .Where(p => p.CompanyId == period.CompanyId).OrderBy(p => p.CurrencyCode).ToListAsync(cancellationToken),
            branchRoundingRules = await db.SysBranches.AsNoTracking()
                .Where(b => b.CompanyId == period.CompanyId && (!branchId.HasValue || b.Id == branchId.Value))
                .OrderBy(b => b.Id).Select(b => new { b.Id, b.RoundingRules }).ToListAsync(cancellationToken),
            workCalendars = await db.WorkCalendars.AsNoTracking().Include(c => c.Days)
                .Where(c => c.CompanyId == period.CompanyId && c.EffectiveFrom <= period.EndDate &&
                    (!c.EffectiveTo.HasValue || c.EffectiveTo.Value >= period.StartDate))
                .OrderBy(c => c.Id).ToListAsync(cancellationToken),
            holidays = await db.PublicHolidays.AsNoTracking()
                .Where(h => h.CompanyId == period.CompanyId &&
                    ((h.HolidayDate >= period.StartDate.Date && h.HolidayDate < period.EndDate.Date.AddDays(1)) || h.IsRecurring))
                .OrderBy(h => h.Id).ToListAsync(cancellationToken),
            salaries = await structures.OrderBy(s => s.Id).ToListAsync(cancellationToken),
            salaryLines = await db.SalaryStructureLines.AsNoTracking().Where(s => structureIds.Contains(s.StructureId)).OrderBy(s => s.Id).ToListAsync(cancellationToken),
            components = await db.SalaryComponents.AsNoTracking().OrderBy(s => s.ComponentCode).ToListAsync(cancellationToken),
            attendance = await db.AttendanceDays.AsNoTracking().Where(d => codes.Contains(d.EmployeeCode) && d.AttendanceDate >= period.StartDate && d.AttendanceDate <= period.EndDate).OrderBy(d => d.Id).ToListAsync(cancellationToken),
            leaves = await db.LeaveRequests.AsNoTracking().Where(l => codes.Contains(l.EmployeeCode) && l.StartDate <= period.EndDate && l.EndDate >= period.StartDate).OrderBy(l => l.Id).ToListAsync(cancellationToken),
            leaveTypes = await db.LeaveTypes.AsNoTracking().OrderBy(l => l.LeaveTypeCode).ToListAsync(cancellationToken),
            loanSchedules = await db.LoanRepaymentSchedules.AsNoTracking()
                .Where(s => string.Compare(s.PayPeriod, period.PeriodCode) <= 0 && db.EmployeeLoans.AsNoTracking()
                    .Any(l => l.Id == s.EmployeeLoanId && codes.Contains(l.EmployeeCode)))
                .OrderBy(s => s.Id).ToListAsync(cancellationToken),
            loans = await db.EmployeeLoans.AsNoTracking()
                .Where(l => codes.Contains(l.EmployeeCode) && db.LoanRepaymentSchedules.AsNoTracking()
                    .Any(s => s.EmployeeLoanId == l.Id && string.Compare(s.PayPeriod, period.PeriodCode) <= 0))
                .OrderBy(l => l.Id).Select(l => new { l.Id, l.EmployeeCode, l.Status }).ToListAsync(cancellationToken),
            advances = await db.EmployeeAdvances.AsNoTracking()
                .Where(a => codes.Contains(a.EmployeeCode) && a.TargetPayPeriod == period.PeriodCode)
                .OrderBy(a => a.Id).ToListAsync(cancellationToken),
            adjustments = await PayrollCalculationAdjustmentQuery.ForPeriod(db, period.CompanyId, period.PeriodCode)
                .Where(a => codes.Contains(a.EmployeeCode))
                .AsNoTracking().OrderBy(a => a.Id)
                .Select(a => new
                {
                    a.Id,
                    a.CompanyId,
                    a.EmployeeCode,
                    a.PayPeriod,
                    a.ComponentCode,
                    a.AdjustmentType,
                    a.Amount,
                    a.Status,
                    a.SourceType,
                    a.SourceId,
                    a.SourceBranchId,
                    a.SourceAttendanceDate,
                    a.SourceOvertimeHours,
                    a.SourceBaseSalary,
                    a.SourceDayType,
                    a.SourceCurrencyCode
                }).ToListAsync(cancellationToken)
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
    }
}
