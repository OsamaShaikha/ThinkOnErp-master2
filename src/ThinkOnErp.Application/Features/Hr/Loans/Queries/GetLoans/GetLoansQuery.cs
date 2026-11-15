using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Queries.GetLoans;

public sealed record GetLoansQuery(
    string? EmployeeCode = null,
    string? Status = null
) : IRequest<ApiResponse<IReadOnlyList<EmployeeLoan>>>;

public sealed class GetLoansQueryHandler : IRequestHandler<GetLoansQuery, ApiResponse<IReadOnlyList<EmployeeLoan>>>
{
    private readonly ILoanRepository _loanRepository;

    public GetLoansQueryHandler(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<EmployeeLoan>>> Handle(GetLoansQuery request, CancellationToken cancellationToken)
    {
        var loans = await _loanRepository.GetLoansAsync(request.EmployeeCode, request.Status, cancellationToken);
        return ApiResponse<IReadOnlyList<EmployeeLoan>>.CreateSuccess(loans, "Loans retrieved successfully.");
    }
}
