using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IShortLeaveRepository
{
    Task<ShortLeaveRequest?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShortLeaveRequest>> GetByEmployeeAsync(string employeeCode, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShortLeaveRequest>> GetApprovedForDateAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default);
    Task<int> GetMonthlyApprovedMinutesAsync(string employeeCode, int year, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShortLeaveRequest>> GetRequestsAsync(long companyId, long? branchId = null, string? status = null, DateTime? date = null, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingRequestAsync(string employeeCode, DateTime date, TimeSpan startTime, TimeSpan endTime, long? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasConflictingLeaveRequestAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default);
    
    Task<ShortLeavePolicy?> GetActivePolicyAsync(long companyId, CancellationToken cancellationToken = default);
    Task SavePolicyAsync(ShortLeavePolicy policy, CancellationToken cancellationToken = default);

    Task AddRequestAsync(ShortLeaveRequest request, CancellationToken cancellationToken = default);
    Task UpdateRequestAsync(ShortLeaveRequest request, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

