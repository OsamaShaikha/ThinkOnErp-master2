using System.Text.Json;
using ThinkOnErp.Application.DTOs.Hr;
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

public sealed class ShiftDefinitionService(IShiftDefinitionRepository repository) : IShiftDefinitionService
{
    public async Task<ShiftDefinitionPageDto> ListAsync(bool activeOnly, string? search, int page, int size, CancellationToken ct = default)
    {
        if (page < 1 || page > 1000000 || size < 1 || size > 100 || search?.Length > 200) throw new ArgumentException("Invalid shift page, size or search length.");
        var result = await repository.ListAsync(activeOnly, search?.Trim(), page, size, ct);
        var used = await repository.ReferencedCodesAsync(result.Items.Select(s => s.ShiftCode).ToArray(), ct);
        return new(result.Items.Select(s => Map(s, used.Contains(s.ShiftCode))).ToList(), result.Total, page, size);
    }

    public async Task<ShiftDefinitionDto> GetAsync(string code, CancellationToken ct = default)
    {
        code = Code(code);
        var shift = await repository.GetAsync(code, ct) ?? throw new KeyNotFoundException("Shift not found.");
        return Map(shift, (await repository.ReferencedCodesAsync(new[] { code }, ct)).Contains(code));
    }

    public async Task<ShiftDefinitionDto> CreateAsync(CreateShiftDefinitionDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var shift = Build(Code(dto.ShiftCode), dto.Definition);
        shift.CreationUser = user; shift.CreationDate = DateTime.UtcNow;
        return Map(await repository.CreateAsync(shift, ct), false);
    }

    public async Task<ShiftDefinitionDto> UpdateAsync(string code, WriteShiftDefinitionDto dto, string ifMatch, string user, CancellationToken ct = default)
    {
        code = Code(code);
        await repository.UpdateAsync(code, Build(code, dto), Version(ifMatch), user, ct);
        return await GetAsync(code, ct);
    }

    public Task DeleteAsync(string code, string ifMatch, CancellationToken ct = default)
        => repository.DeleteAsync(Code(code), Version(ifMatch), ct);

    private static string Code(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50 || code.Trim() is "." or ".." || code.Any(c => char.IsControl(c) || c is '/' or '\\'))
            throw new ArgumentException("Shift code is required, up to 50 characters, without path separators or control characters.");
        return code.Trim();
    }

    private static string Version(string value)
    {
        var version = value?.Trim().Trim('"');
        if (version == null || version.Length != 64 || version.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("If-Match must contain one shift version from GET.");
        return version.ToUpperInvariant();
    }

    private static ShiftSchedule Build(string code, WriteShiftDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.NameLocal) || string.IsNullOrWhiteSpace(dto.NameEn) || dto.NameLocal.Trim().Length > 200 || dto.NameEn.Trim().Length > 200)
            throw new ArgumentException("Both shift names are required, up to 200 characters.");
        if (!dto.StartTime.HasValue || !dto.EndTime.HasValue || !dto.BreakMinutes.HasValue || dto.WorkingDays == null || !dto.IsActive.HasValue)
            throw new ArgumentException("Start, end, break, working days and active state must be supplied explicitly.");
        var shift = new ShiftSchedule { ShiftCode = code, NameLocal = dto.NameLocal.Trim(), NameEn = dto.NameEn.Trim(),
            StartTime = dto.StartTime.Value, EndTime = dto.EndTime.Value, BreakMinutes = dto.BreakMinutes.Value,
            WorkingDaysJson = JsonSerializer.Serialize(dto.WorkingDays), IsActive = dto.IsActive.Value };
        ShiftDefinitionRules.GrossMinutes(shift);
        var days = ShiftDefinitionRules.WorkingDays(shift);
        shift.WorkingDaysJson = JsonSerializer.Serialize(days.OrderBy(d => d));
        return shift;
    }

    private static ShiftDefinitionDto Map(ShiftSchedule shift, bool used)
    {
        var errors = new List<string>();
        int[] days = Array.Empty<int>();
        try { days = ShiftDefinitionRules.WorkingDays(shift); } catch (InvalidOperationException ex) { errors.Add(ex.Message); }
        try { ShiftDefinitionRules.GrossMinutes(shift); } catch (InvalidOperationException ex) { errors.Add(ex.Message); }
        return new(shift.ShiftCode, shift.NameLocal, shift.NameEn, shift.StartTime, shift.EndTime, shift.BreakMinutes,
            days, shift.IsActive, used, ShiftDefinitionRules.Version(shift),
            DateTime.SpecifyKind(shift.CreationDate, DateTimeKind.Utc), shift.UpdateDate.HasValue ? DateTime.SpecifyKind(shift.UpdateDate.Value, DateTimeKind.Utc) : null,
            shift.WorkingDaysJson, errors);
    }
}
