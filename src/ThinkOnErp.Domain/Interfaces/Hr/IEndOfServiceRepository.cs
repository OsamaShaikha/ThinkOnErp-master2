using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IEndOfServiceRepository
{
    // Provisions
    Task<IReadOnlyList<EndOfServiceProvisionAccrual>> GetProvisionsByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<EndOfServiceProvisionAccrual?> GetProvisionAsync(string employeeCode, string period, CancellationToken cancellationToken = default);
    Task AddProvisionAsync(EndOfServiceProvisionAccrual provision, CancellationToken cancellationToken = default);
    void UpdateProvision(EndOfServiceProvisionAccrual provision);

    // Final Settlements
    Task<IReadOnlyList<FinalSettlement>> GetAllSettlementsAsync(string? status = null, CancellationToken cancellationToken = default);
    Task<FinalSettlement?> GetSettlementByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<FinalSettlement?> GetSettlementByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task AddSettlementAsync(FinalSettlement settlement, CancellationToken cancellationToken = default);
    void UpdateSettlement(FinalSettlement settlement);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
