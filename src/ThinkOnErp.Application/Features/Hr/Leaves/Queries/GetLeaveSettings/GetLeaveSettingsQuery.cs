using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveSettings;

public sealed record GetLeaveSettingsQuery : IRequest<ApiResponse<HrLeaveSettingsDto>>;

public sealed class GetLeaveSettingsQueryHandler : IRequestHandler<GetLeaveSettingsQuery, ApiResponse<HrLeaveSettingsDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public GetLeaveSettingsQueryHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<HrLeaveSettingsDto>> Handle(GetLeaveSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var dto = new HrLeaveSettingsDto(
            settings.LeaveYearStartMonth,
            settings.LeaveYearStartDay,
            settings.BalanceDisplayFormat,
            settings.StandardWorkdayHours,
            settings.AutomaticAccrualEnabled,
            settings.AccrualTimeZoneId,
            settings.EligibleCitizenshipCountryCode,
            settings.LeaveCashoutWageBasisCode,
            settings.LeaveCashoutBasisComponentCode,
            settings.LeaveCashoutDayDivisor,
            settings.LeaveCashoutPayComponentCode);

        return ApiResponse<HrLeaveSettingsDto>.CreateSuccess(dto, "HR leave settings retrieved successfully.");
    }
}
