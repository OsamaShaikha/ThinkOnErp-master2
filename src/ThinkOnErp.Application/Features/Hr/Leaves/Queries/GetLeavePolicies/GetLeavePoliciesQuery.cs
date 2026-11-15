using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeavePolicies;

public sealed record GetLeavePoliciesQuery : IRequest<ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>>;

public sealed class GetLeavePoliciesQueryHandler : IRequestHandler<GetLeavePoliciesQuery, ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>>
{
    private readonly ILeaveRepository _leaveRepo;

    public GetLeavePoliciesQueryHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>> Handle(GetLeavePoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = new List<LeavePolicy>();
        foreach (var type in await _leaveRepo.GetLeaveTypesAsync(activeOnly: false, cancellationToken))
            policies.AddRange(await _leaveRepo.GetLeavePolicyVersionsAsync(type.LeaveTypeCode, cancellationToken));

        var dtos = policies.Select(p => new LeavePolicyDetailsDto(
            p.Id,
            p.PolicyName,
            p.LeaveTypeCode,
            p.AccrualMethod,
            p.AccrualRate,
            p.ApplicableTo,
            p.MinServiceMonths,
            p.Tier1YearsThreshold,
            p.Tier1Days,
            p.Tier2Days,
            p.IsActive,
            p.AnnualEntitlementDays,
            p.CycleBasis,
            p.ProrateFirstCycle,
            p.EffectiveFrom,
            p.EffectiveTo
        )).ToList();

        return ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>.CreateSuccess(dtos, "Leave policies retrieved successfully.");
    }
}
