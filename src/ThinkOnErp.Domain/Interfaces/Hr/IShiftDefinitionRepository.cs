using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IShiftDefinitionRepository
{
    Task<(IReadOnlyList<ShiftSchedule> Items, int Total)> ListAsync(bool activeOnly, string? search, int page, int size, CancellationToken ct = default);
    Task<ShiftSchedule?> GetAsync(string code, CancellationToken ct = default);
    Task<IReadOnlySet<string>> ReferencedCodesAsync(IReadOnlyList<string> codes, CancellationToken ct = default);
    Task<ShiftSchedule> CreateAsync(ShiftSchedule shift, CancellationToken ct = default);
    Task<ShiftSchedule> UpdateAsync(string code, ShiftSchedule proposed, string expectedVersion, string user, CancellationToken ct = default);
    Task DeleteAsync(string code, string expectedVersion, CancellationToken ct = default);
}
