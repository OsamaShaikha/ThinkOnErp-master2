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

public sealed class LeaveRepository : ILeaveRepository
{
    private readonly OracleDbContext _context;

    public LeaveRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LeaveType>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveTypes.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(t => t.IsActive);
        }
        return await query.OrderBy(t => t.LeaveTypeCode).ToListAsync(cancellationToken);
    }

    public async Task<LeaveType?> GetLeaveTypeByCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveTypes.FirstOrDefaultAsync(t => t.LeaveTypeCode == leaveTypeCode, cancellationToken);
    }

    public async Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default)
    {
        await _context.LeaveTypes.AddAsync(leaveType, cancellationToken);
    }

    public async Task<(IReadOnlyList<LeaveRequest> Items, int TotalCount)> GetLeaveRequestsPagedAsync(
        string? employeeCode = null,
        string? leaveTypeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(r => r.EmployeeCode == employeeCode);
        }

        if (!string.IsNullOrWhiteSpace(leaveTypeCode))
        {
            query = query.Where(r => r.LeaveTypeCode == leaveTypeCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.EndDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.StartDate <= toDate.Value.Date);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreationDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<LeaveRequest?> GetLeaveRequestByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddLeaveRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default)
    {
        await _context.LeaveRequests.AddAsync(request, cancellationToken);
    }

    public Task UpdateLeaveRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default)
    {
        _context.LeaveRequests.Update(request);
        return Task.CompletedTask;
    }

    public async Task<LeaveBalance?> GetLeaveBalanceAsync(string employeeCode, string leaveTypeCode, int year, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveBalances
            .Include(b => b.LeaveType)
            .FirstOrDefaultAsync(b => b.EmployeeCode == employeeCode && b.LeaveTypeCode == leaveTypeCode && b.YearNo == year, cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveBalance>> GetEmployeeBalancesAsync(string employeeCode, int year, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveBalances
            .Include(b => b.LeaveType)
            .Where(b => b.EmployeeCode == employeeCode && b.YearNo == year)
            .ToListAsync(cancellationToken);
    }

    public async Task AddLeaveBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default)
    {
        await _context.LeaveBalances.AddAsync(balance, cancellationToken);
    }

    public Task UpdateLeaveBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default)
    {
        _context.LeaveBalances.Update(balance);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetApprovedUnpaidLeavesInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Where(r => r.EmployeeCode == employeeCode &&
                        r.Status == "APPROVED" &&
                        r.LeaveType != null &&
                        !r.LeaveType.IsPaid &&
                        r.StartDate <= periodEnd &&
                        r.EndDate >= periodStart)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeavePolicy>> GetActiveLeavePoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LeavePolicies
            .Include(p => p.LeaveType)
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
