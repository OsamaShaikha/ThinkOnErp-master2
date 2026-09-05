using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IAssetAssignmentRepository
{
    Task<IReadOnlyList<AssetAssignment>> GetAllAssignmentsAsync(
        string? employeeCode = null,
        string? status = null,
        string? category = null,
        CancellationToken cancellationToken = default);

    Task<AssetAssignment?> GetAssignmentByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<AssetAssignment?> GetAssignmentByTagAsync(string assetTag, CancellationToken cancellationToken = default);
    Task AddAssignmentAsync(AssetAssignment assignment, CancellationToken cancellationToken = default);
    void UpdateAssignment(AssetAssignment assignment);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
