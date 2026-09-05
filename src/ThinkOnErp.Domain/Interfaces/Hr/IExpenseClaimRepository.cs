using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IExpenseClaimRepository
{
    Task<IReadOnlyList<ExpenseClaim>> GetAllClaimsAsync(
        string? employeeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    Task<ExpenseClaim?> GetClaimByIdAsync(long id, bool includeLines = true, CancellationToken cancellationToken = default);
    Task<ExpenseClaim?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default);
    Task AddClaimAsync(ExpenseClaim claim, CancellationToken cancellationToken = default);
    void UpdateClaim(ExpenseClaim claim);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
