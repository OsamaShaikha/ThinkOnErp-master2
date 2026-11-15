using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.CreateShiftDay;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.DeleteShiftDay;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.UpdateShiftDay;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDayById;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDays;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IShiftDayService
{
    Task<ShiftDayPageDto> ListAsync(string employeeCode, DateOnly? from, DateOnly? to, bool activeOnly, int page, int size, CancellationToken ct = default);
    Task<ShiftDayDto> GetAsync(string employeeCode, long id, CancellationToken ct = default);
    Task<ShiftDayDto> CreateAsync(string employeeCode, WriteShiftDayDto dto, string user, CancellationToken ct = default);
    Task<ShiftDayDto> UpdateAsync(string employeeCode, long id, WriteShiftDayDto dto, string ifMatch, string user, CancellationToken ct = default);
    Task DeleteAsync(string employeeCode, long id, string ifMatch, CancellationToken ct = default);
}

[Obsolete("Use MediatR requests in ThinkOnErp.Application.Features.Hr.Shifts. Kept for existing test suites.")]
public sealed class ShiftDayService(IShiftDayRepository repository) : IShiftDayService
{
    private readonly GetShiftDaysQueryHandler _listHandler = new(repository);
    private readonly GetShiftDayByIdQueryHandler _getHandler = new(repository);
    private readonly CreateShiftDayCommandHandler _createHandler = new(repository);
    private readonly UpdateShiftDayCommandHandler _updateHandler = new(repository);
    private readonly DeleteShiftDayCommandHandler _deleteHandler = new(repository);

    public async Task<ShiftDayPageDto> ListAsync(string employeeCode, DateOnly? from, DateOnly? to, bool activeOnly, int page, int size, CancellationToken ct = default)
    {
        var response = await _listHandler.Handle(new GetShiftDaysQuery(employeeCode, from, to, activeOnly, page, size), ct);
        return response.Data!;
    }

    public async Task<ShiftDayDto> GetAsync(string employeeCode, long id, CancellationToken ct = default)
    {
        var response = await _getHandler.Handle(new GetShiftDayByIdQuery(employeeCode, id), ct);
        if (!response.Success || response.Data == null)
            throw new KeyNotFoundException("Daily roster entry not found.");
        return response.Data;
    }

    public async Task<ShiftDayDto> CreateAsync(string employeeCode, WriteShiftDayDto dto, string user, CancellationToken ct = default)
    {
        var response = await _createHandler.Handle(new CreateShiftDayCommand(employeeCode, dto, user), ct);
        if (!response.Success)
            throw new ShiftVersionConflictException(response.Message ?? "Shift version changed.");
        return response.Data!;
    }

    public async Task<ShiftDayDto> UpdateAsync(string employeeCode, long id, WriteShiftDayDto dto, string ifMatch, string user, CancellationToken ct = default)
    {
        var response = await _updateHandler.Handle(new UpdateShiftDayCommand(employeeCode, id, dto, ifMatch, user), ct);
        if (!response.Success)
            throw new ShiftDayVersionConflictException(response.Message ?? "Daily roster entry version changed.");
        return response.Data!;
    }

    public async Task DeleteAsync(string employeeCode, long id, string ifMatch, CancellationToken ct = default)
    {
        var response = await _deleteHandler.Handle(new DeleteShiftDayCommand(employeeCode, id, ifMatch), ct);
        if (!response.Success)
            throw new ShiftDayVersionConflictException(response.Message ?? "Daily roster entry version changed.");
    }
}
