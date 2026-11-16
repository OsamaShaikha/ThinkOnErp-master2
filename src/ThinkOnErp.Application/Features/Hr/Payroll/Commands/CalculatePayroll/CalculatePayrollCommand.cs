using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.CalculatePayroll;

public sealed record CalculatePayrollCommand(
    CreatePayrollRunDto Dto,
    string User
) : IRequest<ApiResponse<PayrollCalculationResultDto>>;

public sealed class CalculatePayrollCommandValidator : AbstractValidator<CalculatePayrollCommand>
{
    public CalculatePayrollCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
        RuleFor(x => x.Dto.PayPeriod).NotEmpty();
        RuleFor(x => x.Dto.BranchId).NotNull().WithMessage("Calculate a separate payroll run for each branch.");
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class CalculatePayrollCommandHandler : IRequestHandler<CalculatePayrollCommand, ApiResponse<PayrollCalculationResultDto>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IPayrollCalculationEngine _calculationEngine;
    private readonly IPayrollValidationService _validationService;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IPayrollInputSnapshotService? _inputSnapshots;

    public CalculatePayrollCommandHandler(
        IPayrollPeriodRepository payrollPeriodRepo,
        IPayrollCalculationEngine calculationEngine,
        IPayrollValidationService validationService,
        IHrUnitOfWork? unitOfWork = null,
        IPayrollInputSnapshotService? inputSnapshots = null)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _calculationEngine = calculationEngine;
        _validationService = validationService;
        _unitOfWork = unitOfWork;
        _inputSnapshots = inputSnapshots;
    }

    public async Task<ApiResponse<PayrollCalculationResultDto>> Handle(CalculatePayrollCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dto);

        var result = _unitOfWork == null
            ? await PayrollCommon.CalculatePayrollRunCoreAsync(_payrollPeriodRepo, _calculationEngine, _validationService, _inputSnapshots, request.Dto, request.User, cancellationToken)
            : await _unitOfWork.ExecuteForPayrollPeriodAsync(request.Dto.CompanyId, request.Dto.PayPeriod,
                () => PayrollCommon.CalculatePayrollRunCoreAsync(_payrollPeriodRepo, _calculationEngine, _validationService, _inputSnapshots, request.Dto, request.User, cancellationToken),
                cancellationToken);

        return ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "Payroll run calculated successfully.");
    }
}
