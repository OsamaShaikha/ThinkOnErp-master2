using System;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.WorkCalendars.Commands.CreatePublicHoliday;

public sealed record CreatePublicHolidayCommand(CreatePublicHolidayDto Dto, string Username)
    : IRequest<ApiResponse<PublicHoliday>>;

public sealed class CreatePublicHolidayCommandValidator : AbstractValidator<CreatePublicHolidayCommand>
{
    public CreatePublicHolidayCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull().WithMessage("Public holiday payload is required.");
        RuleFor(x => x.Dto.NameAr).NotEmpty().WithMessage("Arabic name is required.");
        RuleFor(x => x.Dto.NameEn).NotEmpty().WithMessage("English name is required.");
    }
}

public sealed class CreatePublicHolidayCommandHandler : IRequestHandler<CreatePublicHolidayCommand, ApiResponse<PublicHoliday>>
{
    private readonly IWorkCalendarRepository _repository;

    public CreatePublicHolidayCommandHandler(IWorkCalendarRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<PublicHoliday>> Handle(CreatePublicHolidayCommand request, CancellationToken ct)
    {
        var dto = request.Dto;
        ArgumentNullException.ThrowIfNull(dto);

        var holiday = new PublicHoliday
        {
            CompanyId = dto.CompanyId,
            BranchId = dto.BranchId,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            HolidayDate = dto.HolidayDate.Date,
            IsPaid = dto.IsPaid,
            IsRecurring = dto.IsRecurring,
            Description = dto.Description,
            CreationUser = request.Username,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddHolidayAsync(holiday, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<PublicHoliday>.CreateSuccess(holiday, "Holiday created successfully.");
    }
}
