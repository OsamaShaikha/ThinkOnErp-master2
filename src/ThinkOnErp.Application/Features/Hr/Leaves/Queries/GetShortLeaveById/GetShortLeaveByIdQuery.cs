using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeaveById;

public sealed record GetShortLeaveByIdQuery(long Id) : IRequest<ShortLeaveDto?>;

public sealed class GetShortLeaveByIdQueryHandler : IRequestHandler<GetShortLeaveByIdQuery, ShortLeaveDto?>
{
    private readonly IShortLeaveRepository _repo;

    public GetShortLeaveByIdQueryHandler(IShortLeaveRepository repo)
    {
        _repo = repo;
    }

    public async Task<ShortLeaveDto?> Handle(GetShortLeaveByIdQuery request, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(request.Id, ct);
        return entity == null ? null : ShortLeaveCommon.MapToDto(entity, entity.Employee?.NameLocal);
    }
}
