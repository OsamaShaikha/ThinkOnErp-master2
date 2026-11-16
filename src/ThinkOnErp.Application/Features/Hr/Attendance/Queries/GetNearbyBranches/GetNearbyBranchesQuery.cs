using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetNearbyBranches;

public sealed record GetNearbyBranchesQuery(decimal Latitude, decimal Longitude, long? CompanyId)
    : IRequest<ApiResponse<IReadOnlyList<NearbyBranchDto>>>;

public sealed class GetNearbyBranchesQueryValidator : AbstractValidator<GetNearbyBranchesQuery>
{
    public GetNearbyBranchesQueryValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m);
    }
}

public sealed class GetNearbyBranchesQueryHandler
    : IRequestHandler<GetNearbyBranchesQuery, ApiResponse<IReadOnlyList<NearbyBranchDto>>>
{
    private readonly IAttendanceBranchRepository _branchAccess;
    private readonly IGeoLocationService _geoService;

    public GetNearbyBranchesQueryHandler(
        IAttendanceBranchRepository branchAccess,
        IGeoLocationService geoService)
    {
        _branchAccess = branchAccess;
        _geoService = geoService;
    }

    public async Task<ApiResponse<IReadOnlyList<NearbyBranchDto>>> Handle(
        GetNearbyBranchesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Latitude is < -90m or > 90m || request.Longitude is < -180m or > 180m)
            throw new ArgumentException("Valid coordinates are required.");

        var branches = await _branchAccess.ListAsync(request.CompanyId, cancellationToken);
        var result = new List<NearbyBranchDto>();

        foreach (var b in branches)
        {
            if (!b.IsActive) continue;

            decimal distance = 0m;
            bool isWithin = false;
            var radius = b.GeofenceRadiusMeters > 0 ? b.GeofenceRadiusMeters : 100m;

            if (b.Latitude.HasValue && b.Longitude.HasValue)
            {
                distance = _geoService.CalculateDistanceMeters(request.Latitude, request.Longitude, b.Latitude.Value, b.Longitude.Value);
                isWithin = distance <= radius;
            }

            result.Add(new NearbyBranchDto(
                BranchId: b.Id,
                BranchNameLocal: b.BranchNameLocal,
                BranchNameEn: b.BranchNameEn,
                Latitude: b.Latitude,
                Longitude: b.Longitude,
                GeofenceRadiusMeters: radius,
                DistanceMeters: distance,
                IsWithinRange: isWithin
            ));
        }

        var sorted = result.OrderBy(x => x.DistanceMeters).ToList();
        return ApiResponse<IReadOnlyList<NearbyBranchDto>>.CreateSuccess(sorted);
    }
}
