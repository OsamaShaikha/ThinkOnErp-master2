using ThinkOnErp.Domain.Entities.Hr;
namespace ThinkOnErp.Domain.Interfaces.Hr;
public interface IOrganizationRepository
{
    Task<IReadOnlyList<HrDepartment>> DepartmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<HrPosition>> PositionsAsync(string? departmentCode, CancellationToken ct = default);
    Task<HrDepartment?> DepartmentAsync(string code, CancellationToken ct = default);
    Task<HrPosition?> PositionAsync(string code, CancellationToken ct = default);
    Task<OrganizationReferenceIntegritySummary> VerifyReferenceIntegrityAsync(CancellationToken ct = default);
    Task<HrDepartment> WriteDepartmentAsync(string code, DepartmentWrite value, string? expectedVersion, string user, CancellationToken ct = default);
    Task<HrPosition> WritePositionAsync(string code, PositionWrite value, string? expectedVersion, string user, CancellationToken ct = default);
}
