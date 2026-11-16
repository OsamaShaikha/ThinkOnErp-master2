using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveMovements;

public sealed record GetLeaveMovementsQuery(
    string EmployeeCode,
    string? LeaveTypeCode = null,
    DateTime? CycleStart = null,
    int Page = 1,
    int Size = 50) : IRequest<ApiResponse<LeaveMovementPageDto>>;

public sealed class GetLeaveMovementsQueryValidator : AbstractValidator<GetLeaveMovementsQuery>
{
    public GetLeaveMovementsQueryValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.Size).InclusiveBetween(1, 200);
    }
}

public sealed class GetLeaveMovementsQueryHandler : IRequestHandler<GetLeaveMovementsQuery, ApiResponse<LeaveMovementPageDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public GetLeaveMovementsQueryHandler(ILeaveRepository leaveRepo, IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public async Task<ApiResponse<LeaveMovementPageDto>> Handle(GetLeaveMovementsQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.Page > 1000000 || request.Size < 1 || request.Size > 200)
            throw new ArgumentException("Page must be 1..1000000 and size 1..200.");

        if (await _employeeRepo.GetEmployeeByCodeAsync(request.EmployeeCode, cancellationToken) == null)
            throw new KeyNotFoundException("Employee not found in the current HR scope.");

        var result = await _leaveRepo.GetMovementsAsync(request.EmployeeCode, request.LeaveTypeCode, request.CycleStart, request.Page, request.Size, cancellationToken);
        var pageDto = new LeaveMovementPageDto(result.Items.Select(m => new LeaveMovementDto(m.Id, m.BalanceId, m.EmployeeCode, m.LeaveTypeCode,
            m.CycleStartDate, m.SequenceNo, m.Kind, m.Source, m.Reason, m.Actor, DateTime.SpecifyKind(m.CreatedDate, DateTimeKind.Utc),
            m.AccruedDelta, m.UsedDelta, m.CarriedDelta, m.AccruedAfter, m.UsedAfter, m.CarriedAfter,
            m.AccruedAfter + m.CarriedAfter - m.UsedAfter, m.ExpiredDays, m.TransferredDays, m.ReversesMovementId)).ToList(), result.Total, request.Page, request.Size);

        return ApiResponse<LeaveMovementPageDto>.CreateSuccess(pageDto);
    }
}
