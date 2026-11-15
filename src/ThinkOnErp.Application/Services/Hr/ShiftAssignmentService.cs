using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.CreateShiftAssignment;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.DeleteShiftAssignment;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.UpdateShiftAssignment;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftAssignmentById;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftAssignments;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IShiftAssignmentService
{
    Task<ShiftAssignmentPageDto> ListAsync(string employeeCode, DateOnly? from, DateOnly? to, bool activeOnly, int page, int size, CancellationToken ct = default);
    Task<ShiftAssignmentDto> GetAsync(string employeeCode, long id, CancellationToken ct = default);
    Task<ShiftAssignmentDto> CreateAsync(string employeeCode, WriteShiftAssignmentDto dto, string user, CancellationToken ct = default);
    Task<ShiftAssignmentDto> UpdateAsync(string employeeCode, long id, WriteShiftAssignmentDto dto, string ifMatch, string user, CancellationToken ct = default);
    Task DeleteAsync(string employeeCode, long id, string ifMatch, CancellationToken ct = default);
}

[Obsolete("Use MediatR requests in ThinkOnErp.Application.Features.Hr.Shifts. Kept for existing test suites.")]
public sealed class ShiftAssignmentService(IShiftAssignmentRepository repository) : IShiftAssignmentService
{
    private readonly GetShiftAssignmentsQueryHandler _listHandler = new(repository);
    private readonly GetShiftAssignmentByIdQueryHandler _getHandler = new(repository);
    private readonly CreateShiftAssignmentCommandHandler _createHandler = new(repository);
    private readonly UpdateShiftAssignmentCommandHandler _updateHandler = new(repository);
    private readonly DeleteShiftAssignmentCommandHandler _deleteHandler = new(repository);

    public async Task<ShiftAssignmentPageDto> ListAsync(string employeeCode, DateOnly? from, DateOnly? to, bool activeOnly, int page, int size, CancellationToken ct = default)
    {
        var response = await _listHandler.Handle(new GetShiftAssignmentsQuery(employeeCode, from, to, activeOnly, page, size), ct);
        return response.Data!;
    }

    public async Task<ShiftAssignmentDto> GetAsync(string employeeCode, long id, CancellationToken ct = default)
    {
        var response = await _getHandler.Handle(new GetShiftAssignmentByIdQuery(employeeCode, id), ct);
        if (!response.Success || response.Data == null)
            throw new KeyNotFoundException("Shift assignment not found.");
        return response.Data;
    }

    public async Task<ShiftAssignmentDto> CreateAsync(string employeeCode, WriteShiftAssignmentDto dto, string user, CancellationToken ct = default)
    {
        var response = await _createHandler.Handle(new CreateShiftAssignmentCommand(employeeCode, dto, user), ct);
        if (!response.Success)
            throw new ShiftVersionConflictException(response.Message ?? "Shift version changed.");
        return response.Data!;
    }

    public async Task<ShiftAssignmentDto> UpdateAsync(string employeeCode, long id, WriteShiftAssignmentDto dto, string ifMatch, string user, CancellationToken ct = default)
    {
        var response = await _updateHandler.Handle(new UpdateShiftAssignmentCommand(employeeCode, id, dto, ifMatch, user), ct);
        if (!response.Success)
            throw new ShiftAssignmentVersionConflictException(response.Message ?? "Shift assignment version changed.");
        return response.Data!;
    }

    public async Task DeleteAsync(string employeeCode, long id, string ifMatch, CancellationToken ct = default)
    {
        var response = await _deleteHandler.Handle(new DeleteShiftAssignmentCommand(employeeCode, id, ifMatch), ct);
        if (!response.Success)
            throw new ShiftAssignmentVersionConflictException(response.Message ?? "Shift assignment version changed.");
    }
}
