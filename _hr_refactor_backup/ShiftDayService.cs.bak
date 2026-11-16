using ThinkOnErp.Application.DTOs.Hr;
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

public sealed class ShiftDayService(IShiftDayRepository repository) : IShiftDayService
{
    public async Task<ShiftDayPageDto> ListAsync(string employeeCode, DateOnly? from, DateOnly? to, bool activeOnly, int page, int size, CancellationToken ct = default)
    {
        if (page < 1 || page > 1000000 || size < 1 || size > 100 || from.HasValue && to.HasValue && from > to) throw new ArgumentException("Invalid daily roster date range or page.");
        await repository.EnsureEmployeeAsync(employeeCode, ct);
        var result = await repository.ListAsync(employeeCode, from?.ToDateTime(TimeOnly.MinValue), to?.ToDateTime(TimeOnly.MinValue), activeOnly, page, size, ct);
        var definitions = await repository.DefinitionsAsync(result.Items.Where(d => d.ShiftCode != null).Select(d => d.ShiftCode!).Distinct().ToArray(), ct);
        return new(result.Items.Select(d => Map(d, d.ShiftCode == null ? null : definitions.GetValueOrDefault(d.ShiftCode))).ToArray(), result.Total, page, size);
    }

    public async Task<ShiftDayDto> GetAsync(string employeeCode, long id, CancellationToken ct = default)
    {
        await repository.EnsureEmployeeAsync(employeeCode, ct);
        var day = await repository.GetAsync(employeeCode, id, ct) ?? throw new KeyNotFoundException("Daily roster entry not found.");
        var definitions = await repository.DefinitionsAsync(day.ShiftCode == null ? Array.Empty<string>() : new[] { day.ShiftCode }, ct);
        return Map(day, day.ShiftCode == null ? null : definitions.GetValueOrDefault(day.ShiftCode));
    }

    public async Task<ShiftDayDto> CreateAsync(string employeeCode, WriteShiftDayDto dto, string user, CancellationToken ct = default)
    {
        var day = Build(employeeCode, dto); day.CreationUser = user; day.CreationDate = DateTime.UtcNow;
        var result = await repository.CreateAsync(day, DefinitionVersion(dto), ct);
        return await GetAsync(employeeCode, result.Id, ct);
    }

    public async Task<ShiftDayDto> UpdateAsync(string employeeCode, long id, WriteShiftDayDto dto, string ifMatch, string user, CancellationToken ct = default)
    {
        await repository.UpdateAsync(employeeCode, id, Build(employeeCode, dto), ShiftAssignmentRules.ParseVersion(ifMatch), DefinitionVersion(dto), user, ct);
        return await GetAsync(employeeCode, id, ct);
    }

    public Task DeleteAsync(string employeeCode, long id, string ifMatch, CancellationToken ct = default)
        => repository.DeleteAsync(employeeCode, id, ShiftAssignmentRules.ParseVersion(ifMatch), ct);

    private static string? DefinitionVersion(WriteShiftDayDto dto)
    {
        if (dto.IsRestDay == true)
        {
            if (dto.ShiftVersion != null) throw new ArgumentException("Rest entries cannot specify a shift version.");
            return null;
        }
        return ShiftAssignmentRules.ParseVersion(dto.ShiftVersion);
    }

    private static EmployeeShiftDay Build(string employeeCode, WriteShiftDayDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (!dto.WorkDate.HasValue || !dto.IsRestDay.HasValue || !dto.IsActive.HasValue) throw new ArgumentException("WorkDate, IsRestDay and IsActive are required explicitly.");
        var day = new EmployeeShiftDay { EmployeeCode = employeeCode, WorkDate = dto.WorkDate.Value.ToDateTime(TimeOnly.MinValue),
            ShiftCode = dto.ShiftCode?.Trim(), IsRestDay = dto.IsRestDay.Value, IsActive = dto.IsActive.Value, Source = "MANUAL" };
        ShiftDayRules.Validate(day); return day;
    }

    private static ShiftDayDto Map(EmployeeShiftDay d, ShiftSchedule? shift) => new(d.Id, d.EmployeeCode, DateOnly.FromDateTime(d.WorkDate),
        d.ShiftCode, d.IsRestDay, d.IsActive, d.Source, ShiftDayRules.Version(d), shift == null ? null : ShiftDefinitionRules.Version(shift), shift?.IsActive,
        d.CreationUser, DateTime.SpecifyKind(d.CreationDate, DateTimeKind.Utc), d.UpdateUser,
        d.UpdateDate.HasValue ? DateTime.SpecifyKind(d.UpdateDate.Value, DateTimeKind.Utc) : null);
}
