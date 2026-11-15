using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed partial class EmployeeRepository
{
    public async Task<EmployeeImportImpact> GetImportImpactAsync(string employeeCode,long? proposedBranchId,DateTime from,CancellationToken cancellationToken=default)
    {
        if (!_context.HrScopeEnabled || _context.HrScopeCompanyId<=0 || _context.HrScopeEmployeeCode!=null)
            throw new UnauthorizedAccessException("Import impact requires company/branch management scope.");
        var existing=await _context.Employees.AsNoTracking().Where(e=>e.EmployeeCode==employeeCode).Select(e=>new { e.BranchId }).SingleOrDefaultAsync(cancellationToken);
        if(existing==null && await _context.Employees.IgnoreQueryFilters().AnyAsync(e=>e.EmployeeCode==employeeCode,cancellationToken))
            throw new UnauthorizedAccessException("Employee is outside the editable scope.");
        var branches=(existing==null?new[] { proposedBranchId }:new[] { existing.BranchId,proposedBranchId }).Distinct().ToArray();
        foreach(var branch in branches)
        {
            if(!branch.HasValue && !_context.HrScopeAllBranches || branch.HasValue &&
                (!_context.HrScopeAllBranches && !_context.HrScopeBranchIds.Contains(branch.Value) ||
                 !await _context.SysBranches.AnyAsync(b=>b.Id==branch && b.CompanyId==_context.HrScopeCompanyId && b.IsActive,cancellationToken)))
                throw new UnauthorizedAccessException("Import branches must remain inside the active permitted company scope.");
        }
        var ids=branches.Where(b=>b.HasValue).Select(b=>b!.Value).ToArray();
        // Related historical approvals protect this visible employee even if their
        // root branch is now hidden. Return only blocking facts, never run data.
        var approved=_context.PayrollRuns.IgnoreQueryFilters().AsNoTracking().Where(r=>(r.Status=="APPROVED" || r.Status=="POSTED_TO_GL" || r.Status=="PAID") &&
            (existing!=null && r.Lines.Any(l=>l.EmployeeCode==employeeCode) || r.BranchId.HasValue && ids.Contains(r.BranchId.Value)));
        var periods=await _context.PayrollPeriods.AsNoTracking().Where(p=>p.CompanyId==_context.HrScopeCompanyId && p.EndDate>=from.Date)
            .OrderBy(p=>p.StartDate).ThenBy(p=>p.PeriodCode)
            .Select(p=>new EmployeeImportPeriodImpact(p.PeriodCode,p.StartDate,p.EndDate,p.Status,approved.Any(r=>r.PayPeriod==p.PeriodCode))).ToListAsync(cancellationToken);
        var unmapped=await approved.AnyAsync(r=>!_context.PayrollPeriods.Any(p=>p.CompanyId==_context.HrScopeCompanyId && p.PeriodCode==r.PayPeriod),cancellationToken);
        return new(periods,unmapped);
    }
}
