using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeaveByEmployee;

public sealed record GetShortLeaveByEmployeeQuery(
    string EmployeeCode,
    DateTime? FromDate = null,
    DateTime? ToDate = null) : IRequest<IReadOnlyList<ShortLeaveDto>>;

public sealed class GetShortLeaveByEmployeeQueryHandler : IRequestHandler<GetShortLeaveByEmployeeQuery, IReadOnlyList<ShortLeaveDto>>
{
    private readonly IShortLeaveRepository _repo;

    public GetShortLeaveByEmployeeQueryHandler(IShortLeaveRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<ShortLeaveDto>> Handle(GetShortLeaveByEmployeeQuery request, CancellationToken ct)
    {
        var items = await _repo.GetByEmployeeAsync(request.EmployeeCode, request.FromDate, request.ToDate, ct);
        return items.Select(r => ShortLeaveCommon.MapToDto(r, r.Employee?.NameLocal)).ToList();
    }
}
