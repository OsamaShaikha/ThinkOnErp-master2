using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.Loans.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.CalculateAndApplyDeductions;

public sealed record CalculateAndApplyDeductionsCommand(
    long? RunLineId,
    string EmployeeCode,
    string PayPeriod,
    decimal NetPayBeforeLoans,
    DeductionPolicy? Policy,
    bool PersistChanges = false,
    string? PayrollCurrencyCode = null
) : IRequest<ApiResponse<LoanDeductionCalculationResult>>;

public sealed class CalculateAndApplyDeductionsCommandHandler : IRequestHandler<CalculateAndApplyDeductionsCommand, ApiResponse<LoanDeductionCalculationResult>>
{
    private readonly ILoanRepository _loanRepository;

    public CalculateAndApplyDeductionsCommandHandler(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    public async Task<ApiResponse<LoanDeductionCalculationResult>> Handle(CalculateAndApplyDeductionsCommand request, CancellationToken cancellationToken)
    {
        var result = await LoanCommon.CalculateAndApplyDeductionsCoreAsync(
            _loanRepository,
            request.RunLineId,
            request.EmployeeCode,
            request.PayPeriod,
            request.NetPayBeforeLoans,
            request.Policy,
            request.PersistChanges,
            cancellationToken,
            request.PayrollCurrencyCode);

        return ApiResponse<LoanDeductionCalculationResult>.CreateSuccess(result, "Loan deductions calculated successfully.");
    }
}
