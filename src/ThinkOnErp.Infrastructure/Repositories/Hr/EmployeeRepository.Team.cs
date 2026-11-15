using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed partial class EmployeeRepository
{
    public async Task<(IReadOnlyList<Employee> Items, int TotalCount)> GetDirectReportsAsync(long userId, bool activeOnly, string? search,
        int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!_context.HrScopeEnabled || _context.HrScopeCompanyId <= 0 || _context.HrScopeEmployeeCode != null || userId <= 0 ||
            !_context.HrScopeAllBranches && _context.HrScopeBranchIds.Count == 0)
            throw new InvalidOperationException("An explicit company/branch management scope is required to read a team.");
        if (pageIndex < 1 || pageSize is < 1 or > 200 || (long)(pageIndex - 1) * pageSize > int.MaxValue)
            throw new ArgumentException("Invalid team page; pageSize must be between 1 and 200.");
        // Resolve only the caller's own link, even if its branch is not among the
        // HR-view grants used for the reports below. No manager identity input.
        var manager = await _context.Employees.IgnoreQueryFilters().AsNoTracking().Where(e => e.UserId == userId && e.IsActive && e.EmploymentStatus == "ACTIVE" &&
            e.BranchId.HasValue && _context.SysBranches.Any(b => b.Id == e.BranchId && b.CompanyId == _context.HrScopeCompanyId && b.IsActive) &&
            _context.SysUsers.Any(u => u.Id == userId && u.CompanyId == _context.HrScopeCompanyId && u.IsActive) &&
            _context.SysUserBranches.Any(b => b.UserId == userId && b.BranchId == e.BranchId))
            .Select(e => e.EmployeeCode).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("No active employee with branch access is linked to this account.");
        var reports = _context.Employees.AsNoTracking().Where(e => e.ManagerEmployeeCode == manager && e.EmployeeCode != manager &&
            (!activeOnly || e.IsActive && e.EmploymentStatus == "ACTIVE"));
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            if (search.Length > 200) throw new ArgumentException("Team search must not exceed 200 characters.");
            reports = reports.Where(e => e.EmployeeCode.Contains(search) || e.NameLocal.Contains(search) || e.NameEn.Contains(search));
        }
        var total = await reports.CountAsync(cancellationToken);
        var items = await reports.OrderBy(e => e.EmployeeCode).Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(e => new Employee { EmployeeCode=e.EmployeeCode, NameLocal=e.NameLocal, NameEn=e.NameEn, DepartmentCode=e.DepartmentCode,
                PositionCode=e.PositionCode, BranchId=e.BranchId, EmploymentStatus=e.EmploymentStatus, IsActive=e.IsActive }).ToListAsync(cancellationToken);
        return (items, total);
    }
}
