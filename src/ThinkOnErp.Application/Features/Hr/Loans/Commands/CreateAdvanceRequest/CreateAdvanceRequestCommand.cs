using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Loans.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.CreateAdvanceRequest;

public sealed record CreateAdvanceRequestCommand(
    CreateAdvanceRequestDto Dto,
    string Username
) : IRequest<ApiResponse<EmployeeAdvance>>;

public sealed class CreateAdvanceRequestCommandValidator : AbstractValidator<CreateAdvanceRequestCommand>
{
    public CreateAdvanceRequestCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty();
        RuleFor(x => x.Dto.AdvanceAmount).GreaterThan(0m);
        RuleFor(x => x.Dto.TargetPayPeriod).NotEmpty();
    }
}

public sealed class CreateAdvanceRequestCommandHandler : IRequestHandler<CreateAdvanceRequestCommand, ApiResponse<EmployeeAdvance>>
{
    private readonly ILoanRepository _loanRepository;
    private readonly IHrUnitOfWork? _unitOfWork;

    public CreateAdvanceRequestCommandHandler(
        ILoanRepository loanRepository,
        IHrUnitOfWork? unitOfWork = null)
    {
        _loanRepository = loanRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<EmployeeAdvance>> Handle(CreateAdvanceRequestCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode))
            throw new ArgumentException("Employee is required.", nameof(dto.EmployeeCode));
        if (dto.AdvanceAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(dto.AdvanceAmount), "Advance amount must be greater than zero.");

        var targetStart = LoanCommon.ParsePayPeriod(dto.TargetPayPeriod);
        var normalizedCurrencyCode = LoanCommon.RequireCurrencyCode(dto.CurrencyCode, nameof(dto.CurrencyCode));
        await LoanCommon.EnsureCurrencyAmountPrecisionAsync(_loanRepository, dto.EmployeeCode, normalizedCurrencyCode, dto.AdvanceAmount, cancellationToken);

        var advance = new EmployeeAdvance
        {
            EmployeeCode = dto.EmployeeCode,
            AdvanceAmount = dto.AdvanceAmount,
            CurrencyCode = normalizedCurrencyCode,
            TargetPayPeriod = dto.TargetPayPeriod,
            DeductedAmount = 0m,
            RemainingBalance = dto.AdvanceAmount,
            Status = "PENDING",
            Reason = dto.Reason,
            CreationUser = request.Username,
            CreationDate = DateTime.UtcNow
        };

        async Task<EmployeeAdvance> PersistAsync()
        {
            await _loanRepository.AddAdvanceAsync(advance, cancellationToken);
            await _loanRepository.SaveChangesAsync(cancellationToken);
            return advance;
        }

        var result = _unitOfWork == null
            ? await PersistAsync()
            : await _unitOfWork.ExecuteForEmployeeDatesAsync(dto.EmployeeCode, targetStart,
                targetStart.AddMonths(1).AddDays(-1), PersistAsync, cancellationToken);

        return ApiResponse<EmployeeAdvance>.CreateSuccess(result, "Advance request created successfully.");
    }
}
