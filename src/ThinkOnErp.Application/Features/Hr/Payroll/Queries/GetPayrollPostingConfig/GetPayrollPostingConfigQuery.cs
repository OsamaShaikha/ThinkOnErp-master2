using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollPostingConfig;

public record GetPayrollPostingConfigQuery(long BranchId) : IRequest<ApiResponse<PayrollPostingConfiguration?>>;

public class GetPayrollPostingConfigQueryHandler : IRequestHandler<GetPayrollPostingConfigQuery, ApiResponse<PayrollPostingConfiguration?>>
{
    private readonly IHrPayrollPostingService _postingService;

    public GetPayrollPostingConfigQueryHandler(IHrPayrollPostingService postingService)
    {
        _postingService = postingService;
    }

    public async Task<ApiResponse<PayrollPostingConfiguration?>> Handle(GetPayrollPostingConfigQuery request, CancellationToken cancellationToken)
    {
        var result = await _postingService.GetAsync(request.BranchId, cancellationToken);
        return ApiResponse<PayrollPostingConfiguration?>.CreateSuccess(result, "Payroll posting configuration retrieved.");
    }
}
