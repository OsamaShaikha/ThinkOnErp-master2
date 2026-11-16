using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class EndOfServiceRepository : IEndOfServiceRepository, IEndOfServiceTransactionRunner
{
    private readonly OracleDbContext _context;

    public EndOfServiceRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteWithSettlementLockAsync<T>(long settlementId, System.Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (settlementId <= 0) throw new ArgumentOutOfRangeException(nameof(settlementId));
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var ownedTransaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        var transaction = ownedTransaction ?? _context.Database.CurrentTransaction!;
        try
        {
            await LockSettlementRowAsync(settlementId, transaction, cancellationToken);
            var tracked = _context.EndOfServiceSettlements.Local.FirstOrDefault(x => x.Id == settlementId);
            if (tracked != null)
            {
                var entry = _context.Entry(tracked);
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                    throw new InvalidOperationException("Cannot transition an end of service settlement with pending local changes.");
                await entry.ReloadAsync(cancellationToken);
            }

            var result = await action();
            if (ownedTransaction != null)
                await ownedTransaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (ownedTransaction != null)
            {
                await ownedTransaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }
            throw;
        }
    }

    public async Task LockEmployeeForCurrentSettlementAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) throw new ArgumentException("Employee code is required.", nameof(employeeCode));
        if (_context.Database.CurrentTransaction == null)
            throw new InvalidOperationException("An employee EOS lock requires the active settlement transaction.");

        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction.GetDbTransaction();
        command.CommandText = "SELECT EMPLOYEE_CODE FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE = :p_employee_code FOR UPDATE";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "p_employee_code";
        parameter.DbType = DbType.String;
        parameter.Value = employeeCode.Trim();
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result == null || result == DBNull.Value)
            throw new KeyNotFoundException($"Employee '{employeeCode}' not found.");

        var tracked = _context.Employees.Local.FirstOrDefault(x => string.Equals(x.EmployeeCode, employeeCode, System.StringComparison.OrdinalIgnoreCase));
        if (tracked != null)
        {
            var entry = _context.Entry(tracked);
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Cannot transition an end of service settlement with pending employee changes.");
            await entry.ReloadAsync(cancellationToken);
        }
    }

    private async Task LockSettlementRowAsync(long settlementId, IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT ID FROM HR_END_OF_SERVICE_SETTLEMENT WHERE ID = :p_id FOR UPDATE";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "p_id";
        parameter.DbType = DbType.Int64;
        parameter.Value = settlementId;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result == null || result == DBNull.Value)
            throw new KeyNotFoundException($"End of service settlement {settlementId} not found.");
    }

    public async Task<EndOfServiceSettlement?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.EndOfServiceSettlements
            .Include(s => s.Lines)
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<EndOfServiceSettlement?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EndOfServiceSettlements
            .Include(s => s.Lines)
            .Include(s => s.Employee)
            .Where(s => s.EmployeeCode == employeeCode && s.Status != EndOfServiceSettlementStatus.Cancelled)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EndOfServiceSettlement>> GetListAsync(long companyId, long? branchId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        IQueryable<EndOfServiceSettlement> query = _context.EndOfServiceSettlements
            .Include(s => s.Lines)
            .Include(s => s.Employee);

        if (branchId.HasValue)
            query = query.Where(s => s.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status == status);

        return (await query.OrderByDescending(s => s.Id).ToListAsync(cancellationToken)).StampAll(companyId);
    }

    public async Task<EndOfServicePolicy?> GetActivePolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return (await _context.EndOfServicePolicies
            .Where(p => (p.IsActive || p.EffectiveTo.HasValue) &&
                (!p.EffectiveFrom.HasValue || p.EffectiveFrom.Value <= effectiveDate) &&
                (!p.EffectiveTo.HasValue || p.EffectiveTo.Value >= effectiveDate))
            .OrderByDescending(p => p.EffectiveFrom)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken))?.Stamp(companyId);
    }

    public Task<EndOfServicePolicy?> GetActivePolicyAsync(long companyId, CancellationToken cancellationToken = default)
        => GetActivePolicyAsync(companyId, DateTime.UtcNow, cancellationToken);

    public async Task<IReadOnlyList<EndOfServicePolicy>> GetPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
        => (await _context.EndOfServicePolicies
            .OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.Id).ToListAsync(cancellationToken)).StampAll(companyId);

    public async Task AddPolicyAsync(EndOfServicePolicy policy, CancellationToken cancellationToken = default)
        => await _context.EndOfServicePolicies.AddAsync(policy, cancellationToken);

    public Task SavePolicyChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task AddAsync(EndOfServiceSettlement settlement, CancellationToken cancellationToken = default)
    {
        await _context.EndOfServiceSettlements.AddAsync(settlement, cancellationToken);
    }

    public Task UpdateAsync(EndOfServiceSettlement settlement, CancellationToken cancellationToken = default)
    {
        _context.EndOfServiceSettlements.Update(settlement);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal> GetRemainingLeaveBalanceDaysAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveBalances
            .Where(b => b.EmployeeCode == employeeCode)
            .SumAsync(b => b.CarriedForwardDays + b.AccruedDays - b.UsedDays, cancellationToken);
    }

    public async Task<decimal> GetOutstandingLoanBalanceAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeLoans
            .Where(l => l.EmployeeCode == employeeCode && l.Status == "ACTIVE")
            .SumAsync(l => l.RemainingBalance, cancellationToken);
    }

    public async Task<decimal> GetOutstandingAdvanceBalanceAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeAdvances
            .Where(a => a.EmployeeCode == employeeCode && a.Status == "APPROVED")
            .SumAsync(a => a.RemainingBalance, cancellationToken);
    }

    public async Task<bool> HasActiveAccountingHandoffAsync(long settlementId, CancellationToken cancellationToken = default)
    {
        var sourceId = settlementId.ToString();
        return await _context.HrAccountingHandoffs.AnyAsync(h =>
            h.SourceType == "EOS" &&
            h.SourceId == sourceId &&
            h.DeliveryStatus != "REVERSED" &&
            h.DeliveryStatus != "WITHDRAWN",
            cancellationToken);
    }

    public async Task<HrAccountingHandoff?> GetLatestHandoffAsync(long settlementId, CancellationToken cancellationToken = default)
    {
        var sourceId = settlementId.ToString();
        return await _context.HrAccountingHandoffs
            .Where(h => h.SourceType == "EOS" && h.SourceId == sourceId)
            .OrderByDescending(h => h.SourceVersion)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
