using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.UpdateLeaveType;

public sealed record UpdateLeaveTypeCommand(string LeaveTypeCode, UpdateLeaveTypeDto Dto, string User) : IRequest<ApiResponse<LeaveTypeDto>>;

public sealed class UpdateLeaveTypeCommandValidator : AbstractValidator<UpdateLeaveTypeCommand>
{
    public UpdateLeaveTypeCommandValidator()
    {
        RuleFor(x => x.LeaveTypeCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.NameLocal).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto.NameEn).MaximumLength(100);
    }
}

public sealed class UpdateLeaveTypeCommandHandler : IRequestHandler<UpdateLeaveTypeCommand, ApiResponse<LeaveTypeDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public UpdateLeaveTypeCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveTypeDto>> Handle(UpdateLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        var entity = await _leaveRepo.GetLeaveTypeByCodeAsync(request.LeaveTypeCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave type with code '{request.LeaveTypeCode}' not found.");

        entity.NameLocal = dto.NameLocal;
        entity.NameEn = dto.NameEn;
        entity.IsPaid = dto.IsPaid;
        if (dto.TracksBalance.HasValue)
            entity.TracksBalance = dto.TracksBalance.Value;
        entity.IsStatutory = dto.IsStatutory;
        entity.MaxDaysPerYear = dto.MaxDaysPerYear;
        entity.CarryForwardAllowed = dto.CarryForwardAllowed;
        entity.CarryForwardCapDays = dto.CarryForwardCapDays;
        if (dto.YearEndExcessAction != null)
            entity.YearEndExcessAction = LeaveCommon.NormalizeYearEndExcessAction(dto.YearEndExcessAction);
        entity.RequiresDocumentation = dto.RequiresDocumentation;
        entity.IsActive = dto.IsActive;
        entity.UpdateUser = request.User;
        entity.UpdateDate = DateTime.UtcNow;

        await _leaveRepo.UpdateLeaveTypeAsync(entity, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        var resultDto = new LeaveTypeDto(
            entity.LeaveTypeCode,
            entity.NameLocal,
            entity.NameEn,
            entity.IsPaid,
            entity.IsStatutory,
            entity.MaxDaysPerYear,
            entity.CarryForwardAllowed,
            entity.CarryForwardCapDays,
            entity.RequiresDocumentation,
            entity.IsActive,
            entity.TracksBalance,
            entity.YearEndExcessAction
        );

        return ApiResponse<LeaveTypeDto>.CreateSuccess(resultDto, "Leave type updated successfully.");
    }
}
