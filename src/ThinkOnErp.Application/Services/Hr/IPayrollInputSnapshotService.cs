using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollInputSnapshotService
{
    Task<string> CaptureAsync(PayrollPeriod period, long? branchId, CancellationToken cancellationToken = default);
}
