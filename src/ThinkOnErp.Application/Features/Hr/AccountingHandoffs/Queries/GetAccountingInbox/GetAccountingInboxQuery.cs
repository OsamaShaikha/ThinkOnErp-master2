using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Queries.GetAccountingInbox;

public record GetAccountingInboxQuery : IRequest<ApiResponse<IReadOnlyList<AccountingHandoffInboxDto>>>;

public class GetAccountingInboxQueryHandler : IRequestHandler<GetAccountingInboxQuery, ApiResponse<IReadOnlyList<AccountingHandoffInboxDto>>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public GetAccountingInboxQueryHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<IReadOnlyList<AccountingHandoffInboxDto>>> Handle(GetAccountingInboxQuery request, CancellationToken cancellationToken)
    {
        var inbox = await _handoffs.GetAccountingInboxAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<AccountingHandoffInboxDto>>.CreateSuccess(inbox);
    }
}
