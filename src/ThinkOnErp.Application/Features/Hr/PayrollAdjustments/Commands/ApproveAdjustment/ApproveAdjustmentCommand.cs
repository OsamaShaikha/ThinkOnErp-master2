using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.ApproveAdjustment;

public sealed record ApproveAdjustmentCommand(
    long Id,
    string? Reason,
    string Username,
    long? BranchId = null
) : IRequest<ApiResponse<PayrollAdjustment>>;

public sealed class ApproveAdjustmentCommandValidator : AbstractValidator<ApproveAdjustmentCommand>
{
    public ApproveAdjustmentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class ApproveAdjustmentCommandHandler : IRequestHandler<ApproveAdjustmentCommand, ApiResponse<PayrollAdjustment>>
{
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;
    private readonly IEmployeeRepository? _employeeRepository;
    private readonly IAttendanceCorrectionRepository? _attendanceRepository;
    private readonly IPayrollPeriodRepository? _payrollPeriodRepository;
    private readonly IOvertimeCalculationService? _overtimeCalculationService;
    private readonly IHrUnitOfWork? _unitOfWork;

    public ApproveAdjustmentCommandHandler(
        IPayrollAdjustmentRepository adjustmentRepository,
        IEmployeeRepository? employeeRepository = null,
        IAttendanceCorrectionRepository? attendanceRepository = null,
        IPayrollPeriodRepository? payrollPeriodRepository = null,
        IOvertimeCalculationService? overtimeCalculationService = null,
        IHrUnitOfWork? unitOfWork = null)
    {
        _adjustmentRepository = adjustmentRepository;
        _employeeRepository = employeeRepository;
        _attendanceRepository = attendanceRepository;
        _payrollPeriodRepository = payrollPeriodRepository;
        _overtimeCalculationService = overtimeCalculationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PayrollAdjustment>> Handle(ApproveAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var result = await PayrollAdjustmentCommon.ExecuteLockedAsync(
            request.Id,
            _adjustmentRepository,
            _unitOfWork,
            _attendanceRepository,
            () =>
            {
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    return PayrollAdjustmentCommon.ApproveAdjustmentCoreAsync(
                        request.Id,
                        request.Username,
                        _adjustmentRepository,
                        _employeeRepository,
                        cancellationToken);
                }

                var normalizedReason = request.Reason.Trim();
                if (normalizedReason.Length > 1000)
                    throw new ArgumentException("A decision reason between 1 and 1000 characters is required.", nameof(request.Reason));

                return PayrollAdjustmentCommon.DecideAdjustmentCoreAsync(
                    request.Id,
                    true,
                    normalizedReason,
                    request.Username,
                    request.BranchId,
                    _adjustmentRepository,
                    _employeeRepository,
                    _attendanceRepository,
                    _payrollPeriodRepository,
                    _overtimeCalculationService,
                    cancellationToken);
            },
            cancellationToken);

        return ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment approved successfully.");
    }
}
