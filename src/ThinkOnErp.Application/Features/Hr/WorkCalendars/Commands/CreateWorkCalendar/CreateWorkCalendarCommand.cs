using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.WorkCalendars.Commands.CreateWorkCalendar;

public sealed record CreateWorkCalendarCommand(CreateWorkCalendarDto Dto, string Username)
    : IRequest<ApiResponse<WorkCalendar>>;

public sealed class CreateWorkCalendarCommandValidator : AbstractValidator<CreateWorkCalendarCommand>
{
    public CreateWorkCalendarCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull().WithMessage("Work calendar payload is required.");
        RuleFor(x => x.Dto.Code).NotEmpty().WithMessage("Work calendar code is required.");
        RuleFor(x => x.Dto.NameAr).NotEmpty().WithMessage("Arabic name is required.");
        RuleFor(x => x.Dto.NameEn).NotEmpty().WithMessage("English name is required.");
    }
}

public sealed class CreateWorkCalendarCommandHandler : IRequestHandler<CreateWorkCalendarCommand, ApiResponse<WorkCalendar>>
{
    private readonly IWorkCalendarRepository _repository;

    public CreateWorkCalendarCommandHandler(IWorkCalendarRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<WorkCalendar>> Handle(CreateWorkCalendarCommand request, CancellationToken ct)
    {
        var dto = request.Dto;
        ArgumentNullException.ThrowIfNull(dto);

        var calendar = new WorkCalendar
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsDefault = dto.IsDefault,
            IsActive = true,
            CreationUser = request.Username,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Days != null && dto.Days.Count > 0)
        {
            foreach (var d in dto.Days)
            {
                calendar.Days.Add(new WorkCalendarDay
                {
                    DayOfWeek = d.DayOfWeek,
                    IsWorkingDay = d.IsWorkingDay,
                    StandardWorkingHours = d.StandardWorkingHours,
                    DefaultShiftCode = d.DefaultShiftCode
                });
            }
        }
        else
        {
            for (int i = 0; i < 7; i++)
            {
                bool isWork = (i >= 0 && i <= 4);
                calendar.Days.Add(new WorkCalendarDay
                {
                    DayOfWeek = i,
                    IsWorkingDay = isWork,
                    StandardWorkingHours = isWork ? 8.0m : 0.0m
                });
            }
        }

        await _repository.AddCalendarAsync(calendar, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<WorkCalendar>.CreateSuccess(calendar, "Work calendar created successfully.");
    }
}
