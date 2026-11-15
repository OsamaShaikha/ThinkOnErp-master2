using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class PayrollAdjustmentRepository : IPayrollAdjustmentRepository
{
    private readonly OracleDbContext _context;

    public PayrollAdjustmentRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PayrollAdjustment>> GetAdjustmentsAsync(long companyId, string? employeeCode, string? payPeriod, string? status, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollAdjustments
            .Include(a => a.Employee)
            .Include(a => a.Component)
            .Where(a => a.CompanyId == companyId);

        if (!string.IsNullOrEmpty(employeeCode))
            query = query.Where(a => a.EmployeeCode == employeeCode);

        if (!string.IsNullOrEmpty(payPeriod))
            query = query.Where(a => a.PayPeriod == payPeriod);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.Status == status);

        return await query.OrderByDescending(a => a.CreationDate).ToListAsync(cancellationToken);
    }

    public async Task<PayrollAdjustment?> GetAdjustmentByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollAdjustments
            .AsNoTracking()
            .Include(a => a.Employee)
            .Include(a => a.Component)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public Task<PayrollAdjustment?> GetAdjustmentBySourceAsync(long companyId, string sourceType, string sourceId, CancellationToken cancellationToken = default)
        => _context.PayrollAdjustments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.SourceType == sourceType && a.SourceId == sourceId, cancellationToken);

    public async Task<IReadOnlyList<PayrollAdjustment>> GetPayrollCalculationAdjustmentsAsync(long companyId, string payPeriod, CancellationToken cancellationToken = default)
    {
        return await PayrollCalculationAdjustmentQuery.ForPeriod(_context, companyId, payPeriod)
            .Include(a => a.Component)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        await _context.PayrollAdjustments.AddAsync(adjustment, cancellationToken);
    }

    public async Task UpdateAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        // Reload the current row so EF retains the persisted concurrency-token values as
        // OriginalValue; marking a detached entity Modified would use the new status as the
        // original token and make every status transition fail with a false concurrency error.
        var current = await _context.PayrollAdjustments
            .SingleOrDefaultAsync(candidate => candidate.Id == adjustment.Id, cancellationToken);
        var expectedStatus = adjustment.Status switch
        {
            "APPROVED" or "REJECTED" => "PENDING",
            "PENDING" => "APPROVED",
            _ => null
        };
        if (current == null || expectedStatus == null || current.Status != expectedStatus ||
            current.PayrollRunLineId != adjustment.PayrollRunLineId)
            throw new DbUpdateConcurrencyException("The payroll adjustment changed before this decision could be saved.");

        _context.Entry(current).CurrentValues.SetValues(adjustment);
    }

    public Task DeleteAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        _context.Entry(adjustment).State = EntityState.Deleted;
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasCalculatedPayrollAsync(string employeeCode, string payPeriod, CancellationToken cancellationToken = default)
        => _context.PayrollRuns.AnyAsync(r => r.PayPeriod == payPeriod && r.Status != "DRAFT" && r.Status != "CANCELLED" &&
            r.Lines.Any(l => l.EmployeeCode == employeeCode), cancellationToken);
}
