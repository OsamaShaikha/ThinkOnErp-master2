using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetBalanceMigration;

public sealed record GetBalanceMigrationQuery(long Id) : IRequest<ApiResponse<LeaveBalanceMigrationDto>>;

public sealed class GetBalanceMigrationQueryHandler : IRequestHandler<GetBalanceMigrationQuery, ApiResponse<LeaveBalanceMigrationDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public GetBalanceMigrationQueryHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveBalanceMigrationDto>> Handle(GetBalanceMigrationQuery request, CancellationToken cancellationToken)
    {
        var migration = await _leaveRepo.GetLeaveBalanceMigrationAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-balance migration '{request.Id}' was not found.");

        return ApiResponse<LeaveBalanceMigrationDto>.CreateSuccess(LeaveCommon.MapLeaveBalanceMigration(migration));
    }
}
