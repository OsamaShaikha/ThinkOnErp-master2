using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class ShortLeaveRepository : IShortLeaveRepository
{
    private readonly OracleDbContext _context;

    public ShortLeaveRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<ShortLeaveRequest?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.ShortLeaveRequests
            .Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ShortLeaveRequest>> GetByEmployeeAsync(string employeeCode, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ShortLeaveRequests.Where(r => r.EmployeeCode == employeeCode);

        if (fromDate.HasValue)
            query = query.Where(r => r.PermissionDate >= fromDate.Value.Date);
        if (toDate.HasValue)
            query = query.Where(r => r.PermissionDate <= toDate.Value.Date);

        return await query.OrderByDescending(r => r.PermissionDate).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShortLeaveRequest>> GetApprovedForDateAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default)
    {
        var d = date.Date;
        return await _context.ShortLeaveRequests
            .Where(r => r.EmployeeCode == employeeCode && r.PermissionDate == d && r.Status == ShortLeaveStatus.Approved)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetMonthlyApprovedMinutesAsync(string employeeCode, int year, int month, CancellationToken cancellationToken = default)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        return await _context.ShortLeaveRequests
            .Where(r => r.EmployeeCode == employeeCode &&
                        r.PermissionDate >= start &&
                        r.PermissionDate < end &&
                        r.Status == ShortLeaveStatus.Approved)
            .SumAsync(r => r.DurationMinutes, cancellationToken);
    }

    public async Task<IReadOnlyList<ShortLeaveRequest>> GetRequestsAsync(long companyId, long? branchId = null, string? status = null, DateTime? date = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ShortLeaveRequests.Where(r => r.CompanyId == companyId);

        if (branchId.HasValue)
            query = query.Where(r => r.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(r => r.PermissionDate == targetDate);
        }

        return await query
            .Include(r => r.Employee)
            .OrderByDescending(r => r.PermissionDate)
            .ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasOverlappingRequestAsync(string employeeCode, DateTime date, TimeSpan startTime, TimeSpan endTime, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var d = date.Date;
        var query = _context.ShortLeaveRequests
            .Where(r => r.EmployeeCode == employeeCode &&
                        r.PermissionDate == d &&
                        r.Status != ShortLeaveStatus.Rejected &&
                        r.Status != ShortLeaveStatus.Cancelled);

        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId.Value);

        return await query.AnyAsync(r => r.StartTime < endTime && r.EndTime > startTime, cancellationToken);
    }

    public Task<bool> HasConflictingLeaveRequestAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return _context.LeaveRequests.AsNoTracking().AnyAsync(r =>
            r.EmployeeCode == employeeCode &&
            (r.Status == "PENDING" || r.Status == "APPROVED") &&
            r.StartDate <= targetDate && r.EndDate >= targetDate,
            cancellationToken);
    }

    public async Task<ShortLeavePolicy?> GetActivePolicyAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.ShortLeavePolicies
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.IsActive, cancellationToken);
    }

    public async Task SavePolicyAsync(ShortLeavePolicy policy, CancellationToken cancellationToken = default)
    {
        if (policy.Id == 0)
            await _context.ShortLeavePolicies.AddAsync(policy, cancellationToken);
        else
            _context.ShortLeavePolicies.Update(policy);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRequestAsync(ShortLeaveRequest request, CancellationToken cancellationToken = default)
    {
        await _context.ShortLeaveRequests.AddAsync(request, cancellationToken);
    }

    public Task UpdateRequestAsync(ShortLeaveRequest request, CancellationToken cancellationToken = default)
    {
        _context.ShortLeaveRequests.Update(request);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

