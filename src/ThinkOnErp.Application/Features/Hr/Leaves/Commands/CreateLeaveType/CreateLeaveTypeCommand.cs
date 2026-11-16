using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateLeaveType;

public sealed record CreateLeaveTypeCommand(CreateLeaveTypeDto Dto, string User) : IRequest<ApiResponse<LeaveTypeDto>>;

public sealed class CreateLeaveTypeCommandValidator : AbstractValidator<CreateLeaveTypeCommand>
{
    public CreateLeaveTypeCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.LeaveTypeCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Dto.NameLocal).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto.NameEn).MaximumLength(100);
    }
}

public sealed class CreateLeaveTypeCommandHandler : IRequestHandler<CreateLeaveTypeCommand, ApiResponse<LeaveTypeDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public CreateLeaveTypeCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveTypeDto>> Handle(CreateLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (string.IsNullOrWhiteSpace(dto.LeaveTypeCode))
            throw new ArgumentException("Leave type code is required.", nameof(dto.LeaveTypeCode));

        var normalizedCode = dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var excessAction = LeaveCommon.NormalizeYearEndExcessAction(dto.YearEndExcessAction);
        var existing = await _leaveRepo.GetLeaveTypeByCodeAsync(normalizedCode, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Leave type with code '{dto.LeaveTypeCode}' already exists.");

        var entity = new LeaveType
        {
            LeaveTypeCode = normalizedCode,
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            IsPaid = dto.IsPaid,
            TracksBalance = dto.TracksBalance,
            IsStatutory = dto.IsStatutory,
            MaxDaysPerYear = dto.MaxDaysPerYear,
            CarryForwardAllowed = dto.CarryForwardAllowed,
            CarryForwardCapDays = dto.CarryForwardCapDays,
            YearEndExcessAction = excessAction,
            RequiresDocumentation = dto.RequiresDocumentation,
            IsActive = dto.IsActive,
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepo.AddLeaveTypeAsync(entity, cancellationToken);
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

        return ApiResponse<LeaveTypeDto>.CreateSuccess(resultDto, "Leave type created successfully.", statusCode: 201);
    }
}
