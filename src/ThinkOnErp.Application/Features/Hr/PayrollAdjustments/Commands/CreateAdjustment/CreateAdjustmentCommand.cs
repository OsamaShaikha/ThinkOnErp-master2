using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.CreateAdjustment;

public sealed record CreateAdjustmentCommand(
    CreatePayrollAdjustmentDto Dto,
    string Username
) : IRequest<ApiResponse<PayrollAdjustment>>;

public sealed class CreateAdjustmentCommandValidator : AbstractValidator<CreateAdjustmentCommand>
{
    public CreateAdjustmentCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty();
        RuleFor(x => x.Dto.ComponentCode).NotEmpty();
        RuleFor(x => x.Dto.PayPeriod).NotEmpty();
        RuleFor(x => x.Dto.Amount).GreaterThan(0m);
    }
}

public sealed class CreateAdjustmentCommandHandler : IRequestHandler<CreateAdjustmentCommand, ApiResponse<PayrollAdjustment>>
{
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;
    private readonly IPayrollPeriodRepository _payrollPeriodRepository;
    private readonly IHrUnitOfWork? _unitOfWork;

    public CreateAdjustmentCommandHandler(
        IPayrollAdjustmentRepository adjustmentRepository,
        IPayrollPeriodRepository payrollPeriodRepository,
        IHrUnitOfWork? unitOfWork = null)
    {
        _adjustmentRepository = adjustmentRepository;
        _payrollPeriodRepository = payrollPeriodRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PayrollAdjustment>> Handle(CreateAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.CompanyId <= 0 || string.IsNullOrWhiteSpace(dto.EmployeeCode) || string.IsNullOrWhiteSpace(dto.ComponentCode) || string.IsNullOrWhiteSpace(dto.PayPeriod))
            throw new ArgumentException("Company, employee, payroll period and salary component are required.");
        if (dto.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(dto.Amount), "Adjustment amount must be positive.");
        var adjustmentType = dto.AdjustmentType?.Trim().ToUpperInvariant();
        if (adjustmentType is not ("EARNING" or "DEDUCTION"))
            throw new ArgumentException("Adjustment type must be EARNING or DEDUCTION.");

        var payPeriodCode = dto.PayPeriod.Trim();
        async Task<PayrollAdjustment> PersistAsync()
        {
            var period = await _payrollPeriodRepository.GetPeriodByCodeAsync(dto.CompanyId, payPeriodCode, cancellationToken)
                ?? throw new KeyNotFoundException("Payroll period was not found for this company.");
            if (period.CompanyId != dto.CompanyId || !string.Equals(period.Status?.Trim(), "OPEN", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Payroll adjustments can only be created for an open period belonging to the requested company.");

            var adjustment = new PayrollAdjustment
            {
                CompanyId = dto.CompanyId,
                EmployeeCode = dto.EmployeeCode.Trim(),
                PayPeriod = payPeriodCode,
                ComponentCode = dto.ComponentCode.Trim(),
                AdjustmentType = adjustmentType,
                Amount = dto.Amount,
                Description = dto.Description,
                Status = "PENDING",
                CreationUser = request.Username,
                CreationDate = DateTime.UtcNow
            };

            await _adjustmentRepository.AddAdjustmentAsync(adjustment, cancellationToken);
            await _adjustmentRepository.SaveChangesAsync(cancellationToken);
            return adjustment;
        }

        var result = _unitOfWork == null
            ? await PersistAsync()
            : await _unitOfWork.ExecuteForPayrollPeriodAsync(dto.CompanyId, payPeriodCode, PersistAsync, cancellationToken);

        return ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment created successfully.");
    }
}
