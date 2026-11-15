using System.Threading;
using System.Threading.Tasks;
using System.Net;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.CheckOut;

public sealed record CheckOutCommand(CheckOutRequestDto Dto, string CurrentUser) : IRequest<ApiResponse<AttendancePunchResultDto>>;

public sealed class CheckOutCommandValidator : AbstractValidator<CheckOutCommand>
{
    public CheckOutCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Dto.Longitude).InclusiveBetween(-180m, 180m);
        RuleFor(x => x.Dto.AccuracyMeters).InclusiveBetween(0m, 99999.99m).When(x => x.Dto.AccuracyMeters.HasValue);
        RuleFor(x => x.Dto.DeviceId).MaximumLength(200);
        RuleFor(x => x.Dto.Source).MaximumLength(100);
    }
}

public sealed class CheckOutCommandHandler : IRequestHandler<CheckOutCommand, ApiResponse<AttendancePunchResultDto>>
{
    private readonly IAttendanceCorrectionRepository _attendanceRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IBranchRepository _branchRepo;
    private readonly IPolicyRepository _policyRepo;
    private readonly IGeoLocationService _geoService;
    private readonly ILogger<CheckOutCommandHandler> _logger;
    private readonly IRawAttendanceProcessingService? _processing;
    private readonly IAttendanceBranchRepository? _branchAccess;

    public CheckOutCommandHandler(
        IAttendanceCorrectionRepository attendanceRepo,
        IEmployeeRepository employeeRepo,
        IBranchRepository branchRepo,
        IPolicyRepository policyRepo,
        IGeoLocationService geoService,
        ILogger<CheckOutCommandHandler> logger,
        IRawAttendanceProcessingService? processing = null,
        IAttendanceBranchRepository? branchAccess = null)
    {
        _attendanceRepo = attendanceRepo;
        _employeeRepo = employeeRepo;
        _branchRepo = branchRepo;
        _policyRepo = policyRepo;
        _geoService = geoService;
        _logger = logger;
        _processing = processing;
        _branchAccess = branchAccess;
    }

    public async Task<ApiResponse<AttendancePunchResultDto>> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        AttendancePunchResultDto result;
        try
        {
            result = await MobilePunchProcessor.RecordPunchAsync(
                _attendanceRepo,
                _employeeRepo,
                _branchRepo,
                _policyRepo,
                _geoService,
                _logger,
                _processing,
                _branchAccess,
                dto.EmployeeCode,
                dto.Latitude,
                dto.Longitude,
                dto.BranchId,
                dto.AccuracyMeters,
                dto.DeviceId,
                dto.Source,
                dto.EventId,
                "OUT",
                request.CurrentUser,
                cancellationToken);
        }
        catch (AttendanceEventConflictException ex)
        {
            return ApiResponse<AttendancePunchResultDto>.CreateFailure(ex.Message, statusCode: (int)HttpStatusCode.Conflict);
        }

        if (!result.Success)
        {
            return new ApiResponse<AttendancePunchResultDto>
            {
                Success = false,
                Message = result.Message,
                Data = result,
                StatusCode = (int)HttpStatusCode.BadRequest
            };
        }

        if (result.EvidenceOnly)
        {
            return ApiResponse<AttendancePunchResultDto>.CreateSuccess(result, result.Message, (int)HttpStatusCode.Accepted);
        }

        return ApiResponse<AttendancePunchResultDto>.CreateSuccess(result, result.Message);
    }
}
