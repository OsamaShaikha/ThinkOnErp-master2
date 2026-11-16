using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.SaveBranchAttendanceLocation;

public sealed record SaveBranchAttendanceLocationCommand(
    long BranchId,
    WriteBranchAttendanceLocationDto Dto,
    string? Version,
    string CurrentUser) : IRequest<ApiResponse<BranchAttendanceLocationDto>>;

public sealed class SaveBranchAttendanceLocationCommandValidator : AbstractValidator<SaveBranchAttendanceLocationCommand>
{
    public SaveBranchAttendanceLocationCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
    }
}

public sealed class SaveBranchAttendanceLocationCommandHandler
    : IRequestHandler<SaveBranchAttendanceLocationCommand, ApiResponse<BranchAttendanceLocationDto>>
{
    private readonly IBranchAttendanceLocationRepository _locations;

    public SaveBranchAttendanceLocationCommandHandler(IBranchAttendanceLocationRepository locations)
    {
        _locations = locations;
    }

    public async Task<ApiResponse<BranchAttendanceLocationDto>> Handle(
        SaveBranchAttendanceLocationCommand request,
        CancellationToken cancellationToken)
    {
        var proposed = new BranchAttendanceLocation
        {
            BranchId = request.BranchId,
            Latitude = request.Dto.Latitude,
            Longitude = request.Dto.Longitude,
            RadiusMeters = request.Dto.RadiusMeters,
            EnforceGeofence = request.Dto.EnforceGeofence
        };

        try
        {
            var saved = await _locations.SaveLocationAsync(
                proposed, request.Version, request.CurrentUser, cancellationToken);

            return ApiResponse<BranchAttendanceLocationDto>.CreateSuccess(
                AttendanceCommon.MapBranchLocation(saved));
        }
        catch (BranchLocationVersionException ex)
        {
            return ApiResponse<BranchAttendanceLocationDto>.CreateFailure(
                ex.Message, statusCode: (int)HttpStatusCode.PreconditionFailed);
        }
    }
}
