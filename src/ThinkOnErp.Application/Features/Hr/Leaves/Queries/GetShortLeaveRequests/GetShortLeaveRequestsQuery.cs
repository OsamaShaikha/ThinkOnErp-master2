using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeaveRequests;

public sealed record GetShortLeaveRequestsQuery(
    long CompanyId,
    long? BranchId = null,
    string? Status = null,
    DateTime? Date = null) : IRequest<IReadOnlyList<ShortLeaveDto>>;

public sealed class GetShortLeaveRequestsQueryHandler : IRequestHandler<GetShortLeaveRequestsQuery, IReadOnlyList<ShortLeaveDto>>
{
    private readonly IShortLeaveRepository _repo;

    public GetShortLeaveRequestsQueryHandler(IShortLeaveRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<ShortLeaveDto>> Handle(GetShortLeaveRequestsQuery request, CancellationToken ct)
    {
        var items = await _repo.GetRequestsAsync(request.CompanyId, request.BranchId, request.Status, request.Date, ct);
        return items.Select(r => ShortLeaveCommon.MapToDto(r, r.Employee?.NameLocal)).ToList();
    }
}
