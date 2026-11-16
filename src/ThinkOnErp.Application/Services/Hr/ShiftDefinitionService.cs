using System;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.CreateShiftDefinition;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.DeleteShiftDefinition;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.UpdateShiftDefinition;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDefinitionByCode;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDefinitions;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IShiftDefinitionService
{
    Task<ShiftDefinitionPageDto> ListAsync(bool activeOnly, string? search, int page, int size, CancellationToken ct = default);
    Task<ShiftDefinitionDto> GetAsync(string code, CancellationToken ct = default);
    Task<ShiftDefinitionDto> CreateAsync(CreateShiftDefinitionDto dto, string user, CancellationToken ct = default);
    Task<ShiftDefinitionDto> UpdateAsync(string code, WriteShiftDefinitionDto dto, string ifMatch, string user, CancellationToken ct = default);
    Task DeleteAsync(string code, string ifMatch, CancellationToken ct = default);
}

[Obsolete("Use MediatR requests in ThinkOnErp.Application.Features.Hr.Shifts. Kept for existing test suites.")]
public sealed class ShiftDefinitionService(IShiftDefinitionRepository repository) : IShiftDefinitionService
{
    private readonly GetShiftDefinitionsQueryHandler _listHandler = new(repository);
    private readonly GetShiftDefinitionByCodeQueryHandler _getHandler = new(repository);
    private readonly CreateShiftDefinitionCommandHandler _createHandler = new(repository);
    private readonly UpdateShiftDefinitionCommandHandler _updateHandler = new(repository);
    private readonly DeleteShiftDefinitionCommandHandler _deleteHandler = new(repository);

    public async Task<ShiftDefinitionPageDto> ListAsync(bool activeOnly, string? search, int page, int size, CancellationToken ct = default)
    {
        var response = await _listHandler.Handle(new GetShiftDefinitionsQuery(activeOnly, search, page, size), ct);
        return response.Data!;
    }

    public async Task<ShiftDefinitionDto> GetAsync(string code, CancellationToken ct = default)
    {
        var response = await _getHandler.Handle(new GetShiftDefinitionByCodeQuery(code), ct);
        if (!response.Success || response.Data == null)
            throw new KeyNotFoundException("Shift not found.");
        return response.Data;
    }

    public async Task<ShiftDefinitionDto> CreateAsync(CreateShiftDefinitionDto dto, string user, CancellationToken ct = default)
    {
        var response = await _createHandler.Handle(new CreateShiftDefinitionCommand(dto, user), ct);
        return response.Data!;
    }

    public async Task<ShiftDefinitionDto> UpdateAsync(string code, WriteShiftDefinitionDto dto, string ifMatch, string user, CancellationToken ct = default)
    {
        var response = await _updateHandler.Handle(new UpdateShiftDefinitionCommand(code, dto, ifMatch, user), ct);
        if (!response.Success)
            throw new ShiftVersionConflictException(response.Message ?? "Shift version changed. Read it again before editing.");
        return response.Data!;
    }

    public async Task DeleteAsync(string code, string ifMatch, CancellationToken ct = default)
    {
        var response = await _deleteHandler.Handle(new DeleteShiftDefinitionCommand(code, ifMatch), ct);
        if (!response.Success)
            throw new ShiftVersionConflictException(response.Message ?? "Shift version changed. Read it again before editing.");
    }
}
