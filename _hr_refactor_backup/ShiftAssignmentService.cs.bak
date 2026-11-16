using ThinkOnErp.Application.DTOs.Hr;
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

public sealed class ShiftAssignmentService(IShiftAssignmentRepository repository) : IShiftAssignmentService
{
    public async Task<ShiftAssignmentPageDto> ListAsync(string employeeCode, DateOnly? from, DateOnly? to, bool activeOnly, int page, int size, CancellationToken ct = default)
    {
        if (page < 1 || page > 1000000 || size < 1 || size > 100 || from.HasValue && to.HasValue && from > to) throw new ArgumentException("Invalid assignment date range or page.");
        await repository.EnsureEmployeeAsync(employeeCode, ct);
        var result = await repository.ListAsync(employeeCode, from?.ToDateTime(TimeOnly.MinValue), to?.ToDateTime(TimeOnly.MinValue), activeOnly, page, size, ct);
        var definitions = await repository.DefinitionsAsync(result.Items.Select(a => a.ShiftCode).Distinct().ToArray(), ct);
        return new(result.Items.Select(a => Map(a, definitions.GetValueOrDefault(a.ShiftCode))).ToList(), result.Total, page, size);
    }

    public async Task<ShiftAssignmentDto> GetAsync(string employeeCode, long id, CancellationToken ct = default)
    {
        await repository.EnsureEmployeeAsync(employeeCode, ct);
        var assignment = await repository.GetAsync(employeeCode, id, ct) ?? throw new KeyNotFoundException("Shift assignment not found.");
        var definitions = await repository.DefinitionsAsync(new[] { assignment.ShiftCode }, ct);
        return Map(assignment, definitions.GetValueOrDefault(assignment.ShiftCode));
    }

    public async Task<ShiftAssignmentDto> CreateAsync(string employeeCode, WriteShiftAssignmentDto dto, string user, CancellationToken ct = default)
    {
        var proposed = Build(employeeCode, dto);
        proposed.CreationUser = user; proposed.CreationDate = DateTime.UtcNow;
        var result = await repository.CreateAsync(proposed, ShiftAssignmentRules.ParseVersion(dto.ShiftVersion), ct);
        return await GetAsync(employeeCode, result.Id, ct);
    }

    public async Task<ShiftAssignmentDto> UpdateAsync(string employeeCode, long id, WriteShiftAssignmentDto dto, string ifMatch, string user, CancellationToken ct = default)
    {
        await repository.UpdateAsync(employeeCode, id, Build(employeeCode, dto), ShiftAssignmentRules.ParseVersion(ifMatch), ShiftAssignmentRules.ParseVersion(dto.ShiftVersion), user, ct);
        return await GetAsync(employeeCode, id, ct);
    }

    public Task DeleteAsync(string employeeCode, long id, string ifMatch, CancellationToken ct = default)
        => repository.DeleteAsync(employeeCode, id, ShiftAssignmentRules.ParseVersion(ifMatch), ct);

    private static EmployeeShiftAssignment Build(string employeeCode, WriteShiftAssignmentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var kind = dto.AssignmentKind?.Trim().ToUpperInvariant();
        if (kind is not ("BASE" or "TEMPORARY")) throw new ArgumentException("AssignmentKind must explicitly be BASE or TEMPORARY.");
        if (!dto.EffectiveFrom.HasValue || !dto.IsActive.HasValue || string.IsNullOrWhiteSpace(dto.ShiftCode) || dto.ShiftCode.Trim().Length > 50)
            throw new ArgumentException("Shift code, start date and active state are required.");
        if (kind == "TEMPORARY" && !dto.EffectiveTo.HasValue || dto.EffectiveTo.HasValue && dto.EffectiveTo.Value < dto.EffectiveFrom.Value)
            throw new ArgumentException("Temporary assignments need an end date and dates must be ordered.");
        return new EmployeeShiftAssignment { EmployeeCode = employeeCode, ShiftCode = dto.ShiftCode.Trim(), AssignmentKind = kind,
            EffectiveFrom = dto.EffectiveFrom.Value.ToDateTime(TimeOnly.MinValue), EffectiveTo = dto.EffectiveTo?.ToDateTime(TimeOnly.MinValue), IsActive = dto.IsActive.Value };
    }

    private static ShiftAssignmentDto Map(EmployeeShiftAssignment a, ShiftSchedule? shift)
        => new(a.Id, a.EmployeeCode, a.ShiftCode, a.AssignmentKind, ShiftAssignmentRules.Kind(a), a.AssignmentKind == null,
            DateOnly.FromDateTime(a.EffectiveFrom), a.EffectiveTo.HasValue ? DateOnly.FromDateTime(a.EffectiveTo.Value) : null, a.IsActive,
            ShiftAssignmentRules.Version(a), shift == null ? null : ShiftDefinitionRules.Version(shift), shift?.IsActive,
            DateTime.SpecifyKind(a.CreationDate, DateTimeKind.Utc), a.UpdateDate.HasValue ? DateTime.SpecifyKind(a.UpdateDate.Value, DateTimeKind.Utc) : null);
}
