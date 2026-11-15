using System;
using System.Threading;
using System.Threading.Tasks;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IEndOfServiceTransactionRunner
{
    Task<T> ExecuteWithSettlementLockAsync<T>(long settlementId, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task LockEmployeeForCurrentSettlementAsync(string employeeCode, CancellationToken cancellationToken = default);
}
