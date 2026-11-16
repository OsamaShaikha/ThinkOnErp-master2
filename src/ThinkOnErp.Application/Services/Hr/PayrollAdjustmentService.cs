using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.ApproveAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.CreateAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.CreateOvertimeAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.DecideAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.DeleteAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.RejectAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.UnpostAdjustment;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Queries.GetAdjustmentById;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Queries.GetAdjustments;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollAdjustmentService
{
    Task<PayrollAdjustment> CreateAdjustmentAsync(CreatePayrollAdjustmentDto dto, string user, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> CreateOvertimeAdjustmentAsync(CreateOvertimePayrollAdjustmentDto dto, string user, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> ApproveAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> RejectAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> DecideAdjustmentAsync(long id, bool approve, string reason, string user, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> DecideAdjustmentAsync(long id, bool approve, string reason, string user, long? branchId, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> UnpostAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> UnpostAdjustmentAsync(long id, string user, long? branchId, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment> UnpostAdjustmentAsync(long id, string user, long? branchId, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollAdjustmentDto>> GetAdjustmentsAsync(long companyId, string? employeeCode, string? payPeriod, string? status, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment?> GetAdjustmentByIdAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteAdjustmentAsync(long id, CancellationToken cancellationToken = default);
}

[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.PayrollAdjustments. Kept for backwards compatibility.")]
public sealed class PayrollAdjustmentService : IPayrollAdjustmentService
{
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IAttendanceCorrectionRepository? _attendanceRepository;
    private readonly IEmployeeRepository? _employeeRepository;
    private readonly IPayrollPeriodRepository? _payrollPeriodRepository;
    private readonly IOvertimeCalculationService? _overtimeCalculationService;

    public PayrollAdjustmentService(
        IPayrollAdjustmentRepository adjustmentRepository,
        IHrUnitOfWork? unitOfWork = null,
        IAttendanceCorrectionRepository? attendanceRepository = null,
        IEmployeeRepository? employeeRepository = null,
        IPayrollPeriodRepository? payrollPeriodRepository = null,
        IOvertimeCalculationService? overtimeCalculationService = null)
    {
        _adjustmentRepository = adjustmentRepository;
        _unitOfWork = unitOfWork;
        _attendanceRepository = attendanceRepository;
        _employeeRepository = employeeRepository;
        _payrollPeriodRepository = payrollPeriodRepository;
        _overtimeCalculationService = overtimeCalculationService;
    }

    public async Task<PayrollAdjustment> CreateAdjustmentAsync(CreatePayrollAdjustmentDto dto, string user, CancellationToken cancellationToken = default)
    {
        if (_payrollPeriodRepository == null)
            throw new InvalidOperationException("Payroll period validation is not configured.");
        return Data(await new CreateAdjustmentCommandHandler(_adjustmentRepository, _payrollPeriodRepository, _unitOfWork)
            .Handle(new CreateAdjustmentCommand(dto, user), cancellationToken));
    }

    public async Task<PayrollAdjustment> CreateOvertimeAdjustmentAsync(CreateOvertimePayrollAdjustmentDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateOvertimeAdjustmentCommandHandler(
            _adjustmentRepository,
            _attendanceRepository,
            _employeeRepository,
            _payrollPeriodRepository,
            _overtimeCalculationService,
            _unitOfWork)
            .Handle(new CreateOvertimeAdjustmentCommand(dto, user), cancellationToken));

    public async Task<PayrollAdjustment> ApproveAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default)
        => Data(await new ApproveAdjustmentCommandHandler(
            _adjustmentRepository,
            _employeeRepository,
            _attendanceRepository,
            _payrollPeriodRepository,
            _overtimeCalculationService,
            _unitOfWork)
            .Handle(new ApproveAdjustmentCommand(id, null, user), cancellationToken));

    public async Task<PayrollAdjustment> RejectAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default)
        => Data(await new RejectAdjustmentCommandHandler(
            _adjustmentRepository,
            _employeeRepository,
            _attendanceRepository,
            _payrollPeriodRepository,
            _overtimeCalculationService,
            _unitOfWork)
            .Handle(new RejectAdjustmentCommand(id, null, user), cancellationToken));

    public Task<PayrollAdjustment> DecideAdjustmentAsync(long id, bool approve, string reason, string user, CancellationToken cancellationToken = default)
        => DecideAdjustmentAsync(id, approve, reason, user, null, cancellationToken);

    public async Task<PayrollAdjustment> DecideAdjustmentAsync(long id, bool approve, string reason, string user, long? branchId, CancellationToken cancellationToken = default)
        => Data(await new DecideAdjustmentCommandHandler(
            _adjustmentRepository,
            _employeeRepository,
            _attendanceRepository,
            _payrollPeriodRepository,
            _overtimeCalculationService,
            _unitOfWork)
            .Handle(new DecideAdjustmentCommand(id, approve, reason, user, branchId), cancellationToken));

    public Task<PayrollAdjustment> UnpostAdjustmentAsync(long id, string user, CancellationToken cancellationToken = default)
        => UnpostAdjustmentAsync(id, user, null, cancellationToken);

    public Task<PayrollAdjustment> UnpostAdjustmentAsync(long id, string user, long? branchId, CancellationToken cancellationToken = default)
        => UnpostAdjustmentAsync(id, user, branchId, null, cancellationToken);

    public async Task<PayrollAdjustment> UnpostAdjustmentAsync(long id, string user, long? branchId, string? reason, CancellationToken cancellationToken = default)
        => Data(await new UnpostAdjustmentCommandHandler(
            _adjustmentRepository,
            _attendanceRepository,
            _unitOfWork)
            .Handle(new UnpostAdjustmentCommand(id, user, branchId, reason), cancellationToken));

    public async Task<IReadOnlyList<PayrollAdjustmentDto>> GetAdjustmentsAsync(long companyId, string? employeeCode, string? payPeriod, string? status, CancellationToken cancellationToken = default)
        => Data(await new GetAdjustmentsQueryHandler(_adjustmentRepository)
            .Handle(new GetAdjustmentsQuery(companyId, employeeCode, payPeriod, status), cancellationToken));

    public async Task<PayrollAdjustment?> GetAdjustmentByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await new GetAdjustmentByIdQueryHandler(_adjustmentRepository)
            .Handle(new GetAdjustmentByIdQuery(id), cancellationToken);
        return response.Success ? response.Data : null;
    }

    public async Task DeleteAdjustmentAsync(long id, CancellationToken cancellationToken = default)
        => await new DeleteAdjustmentCommandHandler(_adjustmentRepository, _attendanceRepository, _unitOfWork)
            .Handle(new DeleteAdjustmentCommand(id), cancellationToken);

    private static T Data<T>(ApiResponse<T> response) => response.Data!;
}
