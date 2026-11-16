using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Queries.GetAdvances;

public sealed record GetAdvancesQuery(
    string? EmployeeCode = null,
    string? TargetPayPeriod = null
) : IRequest<ApiResponse<IReadOnlyList<EmployeeAdvance>>>;

public sealed class GetAdvancesQueryHandler : IRequestHandler<GetAdvancesQuery, ApiResponse<IReadOnlyList<EmployeeAdvance>>>
{
    private readonly ILoanRepository _loanRepository;

    public GetAdvancesQueryHandler(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<EmployeeAdvance>>> Handle(GetAdvancesQuery request, CancellationToken cancellationToken)
    {
        var advances = await _loanRepository.GetAdvancesAsync(request.EmployeeCode, request.TargetPayPeriod, cancellationToken);
        return ApiResponse<IReadOnlyList<EmployeeAdvance>>.CreateSuccess(advances, "Advances retrieved successfully.");
    }
}
