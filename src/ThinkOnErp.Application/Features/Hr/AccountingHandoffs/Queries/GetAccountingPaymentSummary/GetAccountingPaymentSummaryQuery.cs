using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Queries.GetAccountingPaymentSummary;

public record GetAccountingPaymentSummaryQuery(long Id) : IRequest<ApiResponse<AccountingHandoffPaymentSummaryDto>>;

public class GetAccountingPaymentSummaryQueryHandler : IRequestHandler<GetAccountingPaymentSummaryQuery, ApiResponse<AccountingHandoffPaymentSummaryDto>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public GetAccountingPaymentSummaryQueryHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<AccountingHandoffPaymentSummaryDto>> Handle(GetAccountingPaymentSummaryQuery request, CancellationToken cancellationToken)
    {
        var status = await _handoffs.GetAccountingPaymentSummaryAsync(request.Id, cancellationToken);
        if (status == null)
            return ApiResponse<AccountingHandoffPaymentSummaryDto>.CreateFailure("Payroll handoff not found.", statusCode: 404);

        return ApiResponse<AccountingHandoffPaymentSummaryDto>.CreateSuccess(status);
    }
}
