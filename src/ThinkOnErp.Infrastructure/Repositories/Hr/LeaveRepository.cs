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
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<LeaveType>> GetAllLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveTypes.AsNoTracking();
        if (activeOnly) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.LeaveTypeCode).ToListAsync(cancellationToken);
    }

    public async Task<LeaveType?> GetLeaveTypeByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveTypes
            .Include(t => t.Policies)
            .SingleOrDefaultAsync(t => t.LeaveTypeCode == code, cancellationToken);
    }

    public async Task<bool> LeaveTypeCodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveTypes.AsNoTracking().AnyAsync(t => t.LeaveTypeCode == code, cancellationToken);
    }

    public async Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaveType);
        await _context.LeaveTypes.AddAsync(leaveType, cancellationToken);
    }

    public void UpdateLeaveType(LeaveType leaveType)
    {
        ArgumentNullException.ThrowIfNull(leaveType);
        _context.LeaveTypes.Update(leaveType);
    }

    public async Task<IReadOnlyList<LeavePolicy>> GetPoliciesByLeaveTypeAsync(string leaveTypeCode, CancellationToken cancellationToken = default)
    {
        return await _context.LeavePolicies
            .Where(p => p.LeaveTypeCode == leaveTypeCode && p.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeavePolicy>> GetAllActivePoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LeavePolicies
            .Include(p => p.LeaveType)
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<LeavePolicy?> GetPolicyByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.LeavePolicies
            .Include(p => p.LeaveType)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task AddPolicyAsync(LeavePolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        await _context.LeavePolicies.AddAsync(policy, cancellationToken);
    }

    public void UpdatePolicy(LeavePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _context.LeavePolicies.Update(policy);
    }

    public async Task<IReadOnlyList<LeaveBalance>> GetBalancesByEmployeeAsync(string employeeCode, int year, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveBalances
            .Include(b => b.LeaveType)
            .AsNoTracking()
            .Where(b => b.EmployeeCode == employeeCode && b.Year == year)
            .OrderBy(b => b.LeaveTypeCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<LeaveBalance?> GetBalanceAsync(string employeeCode, string leaveTypeCode, int year, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveBalances
            .Include(b => b.LeaveType)
            .SingleOrDefaultAsync(b => b.EmployeeCode == employeeCode && b.LeaveTypeCode == leaveTypeCode && b.Year == year, cancellationToken);
    }

    public async Task AddBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(balance);
        await _context.LeaveBalances.AddAsync(balance, cancellationToken);
    }

    public void UpdateBalance(LeaveBalance balance)
    {
        ArgumentNullException.ThrowIfNull(balance);
        _context.LeaveBalances.Update(balance);
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetRequestsAsync(
        string? employeeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveRequests
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(r => r.EmployeeCode == employeeCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.StartDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.EndDate <= toDate.Value.Date);
        }

        return await query
            .OrderByDescending(r => r.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<LeaveRequest?> GetRequestByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveRequests
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _context.LeaveRequests.AddAsync(request, cancellationToken);
    }

    public void UpdateRequest(LeaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _context.LeaveRequests.Update(request);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
