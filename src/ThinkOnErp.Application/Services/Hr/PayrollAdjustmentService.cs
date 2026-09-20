using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollAdjustmentService
{
    Task<PayrollAdjustment> CreateAdjustmentAsync(CreatePayrollAdjustmentDto dto, string user, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> ApproveAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> RejectAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollAdjustmentDto>> GetAdjustmentsAsync(long companyId, string? employeeCode, string? payPeriod, string? status, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment?> GetAdjustmentByIdAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteAdjustmentAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class PayrollAdjustmentService : IPayrollAdjustmentService
{
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;

    public PayrollAdjustmentService(IPayrollAdjustmentRepository adjustmentRepository)
    {
        _adjustmentRepository = adjustmentRepository;
    }

    public async Task<PayrollAdjustment> CreateAdjustmentAsync(CreatePayrollAdjustmentDto dto, string user, CancellationToken cancellationToken = default)
    {
        var adjustment = new PayrollAdjustment
        {
            CompanyId = dto.CompanyId,
            EmployeeCode = dto.EmployeeCode,
            PayPeriod = dto.PayPeriod,
            ComponentCode = dto.ComponentCode,
            AdjustmentType = dto.AdjustmentType.ToUpperInvariant(), // EARNING or DEDUCTION
            Amount = dto.Amount,
            Description = dto.Description,
            Status = "APPROVED", // Approved by default on creation or PENDING if workflow requires
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _adjustmentRepository.AddAdjustmentAsync(adjustment, cancellationToken);
        await _adjustmentRepository.SaveChangesAsync(cancellationToken);
        return adjustment;
    }

    public async Task<PayrollAdjustment> ApproveAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default)
    {
        var adj = await _adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken);
        if (adj == null) throw new KeyNotFoundException($"Payroll adjustment with ID {id} not found.");

        adj.Status = "APPROVED";
        adj.UpdateUser = user;
        adj.UpdateDate = DateTime.UtcNow;

        await _adjustmentRepository.UpdateAdjustmentAsync(adj, cancellationToken);
        await _adjustmentRepository.SaveChangesAsync(cancellationToken);
        return adj;
    }

    public async Task<PayrollAdjustment> RejectAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default)
    {
        var adj = await _adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken);
        if (adj == null) throw new KeyNotFoundException($"Payroll adjustment with ID {id} not found.");

        adj.Status = "REJECTED";
        adj.UpdateUser = user;
        adj.UpdateDate = DateTime.UtcNow;

        await _adjustmentRepository.UpdateAdjustmentAsync(adj, cancellationToken);
        await _adjustmentRepository.SaveChangesAsync(cancellationToken);
        return adj;
    }

    public async Task<IReadOnlyList<PayrollAdjustmentDto>> GetAdjustmentsAsync(long companyId, string? employeeCode, string? payPeriod, string? status, CancellationToken cancellationToken = default)
    {
        var list = await _adjustmentRepository.GetAdjustmentsAsync(companyId, employeeCode, payPeriod, status, cancellationToken);
        return list.Select(a => new PayrollAdjustmentDto(
            a.Id,
            a.CompanyId,
            a.EmployeeCode,
            a.Employee?.NameLocal ?? a.EmployeeCode,
            a.PayPeriod,
            a.ComponentCode,
            a.Component?.NameLocal ?? a.ComponentCode,
            a.AdjustmentType,
            a.Amount,
            a.Description,
            a.Status
        )).ToList();
    }

    public async Task<PayrollAdjustment?> GetAdjustmentByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAdjustmentAsync(long id, CancellationToken cancellationToken = default)
    {
        var adj = await _adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken);
        if (adj != null)
        {
            await _adjustmentRepository.DeleteAdjustmentAsync(adj, cancellationToken);
            await _adjustmentRepository.SaveChangesAsync(cancellationToken);
        }
    }
}
