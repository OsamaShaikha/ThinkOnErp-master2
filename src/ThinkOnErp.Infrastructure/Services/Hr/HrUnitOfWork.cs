using System.Data;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public sealed class HrUnitOfWork(OracleDbContext db) : IHrUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction != null) return await action();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try { var result = await action(); await transaction.CommitAsync(cancellationToken); return result; }
        catch { await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); throw; }
    }
}
