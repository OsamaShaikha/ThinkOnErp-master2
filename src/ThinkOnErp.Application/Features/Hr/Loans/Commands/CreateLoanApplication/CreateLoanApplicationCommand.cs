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

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.CreateLoanApplication;

public sealed record CreateLoanApplicationCommand(
    CreateLoanApplicationDto Dto,
    string Username
) : IRequest<ApiResponse<EmployeeLoan>>;

public sealed class CreateLoanApplicationCommandValidator : AbstractValidator<CreateLoanApplicationCommand>
{
    public CreateLoanApplicationCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty();
        RuleFor(x => x.Dto.LoanType).NotEmpty();
        RuleFor(x => x.Dto.PrincipalAmount).GreaterThan(0m);
        RuleFor(x => x.Dto.TotalInstallments).GreaterThan(0);
        RuleFor(x => x.Dto.StartDate).NotEmpty();
    }
}

public sealed class CreateLoanApplicationCommandHandler : IRequestHandler<CreateLoanApplicationCommand, ApiResponse<EmployeeLoan>>
{
    private readonly ILoanRepository _loanRepository;
    private readonly IHrUnitOfWork? _unitOfWork;

    public CreateLoanApplicationCommandHandler(
        ILoanRepository loanRepository,
        IHrUnitOfWork? unitOfWork = null)
    {
        _loanRepository = loanRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<EmployeeLoan>> Handle(CreateLoanApplicationCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode) || string.IsNullOrWhiteSpace(dto.LoanType))
            throw new ArgumentException("Employee and loan type are required.");
        if (dto.PrincipalAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(dto.PrincipalAmount), "Loan principal must be greater than zero.");
        if (dto.TotalInstallments <= 0)
            throw new ArgumentOutOfRangeException(nameof(dto.TotalInstallments), "Loan must have at least one installment.");
        if (dto.StartDate == default)
            throw new ArgumentException("Loan start date is required.", nameof(dto.StartDate));

        var normalizedCurrencyCode = LoanCommon.RequireCurrencyCode(dto.CurrencyCode, nameof(dto.CurrencyCode));
        var currencyRules = await LoanCommon.EnsureCurrencyAmountPrecisionAsync(
            _loanRepository, dto.EmployeeCode, normalizedCurrencyCode, dto.PrincipalAmount, cancellationToken);
        var minorUnit = 1m;
        for (var i = 0; i < currencyRules.DecimalPlaces; i++)
            minorUnit /= 10m;
        if (dto.PrincipalAmount < minorUnit * dto.TotalInstallments)
            throw new ArgumentOutOfRangeException(nameof(dto.TotalInstallments),
                "Loan principal must cover at least one currency minor unit per installment.");

        var unroundedInstallment = dto.PrincipalAmount / dto.TotalInstallments;
        var installmentAmount = PayrollCurrencyRules.Round(
            unroundedInstallment, currencyRules.DecimalPlaces, currencyRules.RoundingRule);
        var finalInstallment = dto.PrincipalAmount - installmentAmount * (dto.TotalInstallments - 1);
        if (finalInstallment < minorUnit)
        {
            installmentAmount = PayrollCurrencyRules.Round(unroundedInstallment, currencyRules.DecimalPlaces, 4);
            finalInstallment = dto.PrincipalAmount - installmentAmount * (dto.TotalInstallments - 1);
        }
        if (installmentAmount < minorUnit || finalInstallment < minorUnit ||
            !LoanCommon.HasCurrencyAmountPrecision(installmentAmount, currencyRules.DecimalPlaces) ||
            !LoanCommon.HasCurrencyAmountPrecision(finalInstallment, currencyRules.DecimalPlaces))
            throw new InvalidOperationException("Loan installments cannot be distributed as positive amounts at the configured currency precision.");

        var loan = new EmployeeLoan
        {
            EmployeeCode = dto.EmployeeCode,
            LoanType = dto.LoanType,
            PrincipalAmount = dto.PrincipalAmount,
            CurrencyCode = normalizedCurrencyCode,
            MonthlyInstallmentAmount = installmentAmount,
            TotalInstallments = dto.TotalInstallments,
            TotalPaidAmount = 0m,
            RemainingBalance = dto.PrincipalAmount,
            StartDate = dto.StartDate,
            EndDate = dto.StartDate.AddMonths(dto.TotalInstallments),
            Status = "PENDING",
            Notes = dto.Notes,
            CreationUser = request.Username,
            CreationDate = DateTime.UtcNow
        };

        var currentPeriodDate = dto.StartDate;
        for (int i = 1; i <= dto.TotalInstallments; i++)
        {
            var periodCode = currentPeriodDate.ToString("yyyy-MM");
            decimal instAmount = (i == dto.TotalInstallments)
                ? finalInstallment
                : loan.MonthlyInstallmentAmount;

            loan.Schedules.Add(new LoanRepaymentSchedule
            {
                InstallmentNo = i,
                PayPeriod = periodCode,
                ScheduledAmount = instAmount,
                PaidAmount = 0,
                CarriedForwardAmount = 0,
                Status = "PENDING"
            });

            currentPeriodDate = currentPeriodDate.AddMonths(1);
        }

        async Task<EmployeeLoan> PersistAsync()
        {
            await _loanRepository.AddLoanAsync(loan, cancellationToken);
            await _loanRepository.SaveChangesAsync(cancellationToken);
            return loan;
        }

        var result = _unitOfWork == null
            ? await PersistAsync()
            : await _unitOfWork.ExecuteForEmployeeDatesAsync(dto.EmployeeCode, dto.StartDate,
                dto.StartDate.AddMonths(dto.TotalInstallments), PersistAsync, cancellationToken);

        return ApiResponse<EmployeeLoan>.CreateSuccess(result, "Loan application created successfully.");
    }
}
