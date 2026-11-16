using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.SetCurrencyPrecision;

public sealed record SetCurrencyPrecisionCommand(
    PayrollCurrencyPrecisionDto Dto,
    string User
) : IRequest<ApiResponse<PayrollCurrencyPrecisionDto>>;

public sealed class SetCurrencyPrecisionCommandValidator : AbstractValidator<SetCurrencyPrecisionCommand>
{
    public SetCurrencyPrecisionCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
            RuleFor(x => x.Dto.CurrencyCode).NotEmpty().MaximumLength(10);
            RuleFor(x => x.Dto.DecimalPlaces).InclusiveBetween(0, 4);
        });
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class SetCurrencyPrecisionCommandHandler : IRequestHandler<SetCurrencyPrecisionCommand, ApiResponse<PayrollCurrencyPrecisionDto>>
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public SetCurrencyPrecisionCommandHandler(
        IPolicyRepository policyRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _policyRepo = policyRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PayrollCurrencyPrecisionDto>> Handle(SetCurrencyPrecisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dto);

        var code = request.Dto.CurrencyCode?.Trim().ToUpperInvariant();
        if (request.Dto.CompanyId <= 0) throw new ArgumentException("CompanyId must be positive.");
        if (string.IsNullOrWhiteSpace(code) || code.Length > 10) throw new ArgumentException("CurrencyCode is required and cannot exceed 10 characters.");
        if (request.Dto.DecimalPlaces is < 0 or > 4) throw new ArgumentException("DecimalPlaces must be between 0 and 4.");

        var precision = new PayrollCurrencyPrecision
        {
            CompanyId = request.Dto.CompanyId,
            CurrencyCode = code,
            DecimalPlaces = request.Dto.DecimalPlaces,
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        Func<Task> write = async () =>
        {
            await _policyRepo.SavePayrollCurrencyPrecisionAsync(precision, cancellationToken);
            await _policyRepo.SaveChangesAsync(cancellationToken);
        };

        if (_unitOfWork == null)
        {
            await write();
        }
        else
        {
            await _unitOfWork.ExecuteForCompanyPayrollDatesAsync(request.Dto.CompanyId, DateTime.MinValue.Date, DateTime.MaxValue.Date, async () =>
            {
                await write();
                return true;
            }, cancellationToken);
        }

        request.Dto.CurrencyCode = code;
        return ApiResponse<PayrollCurrencyPrecisionDto>.CreateSuccess(request.Dto, "Payroll currency precision saved.");
    }
}
