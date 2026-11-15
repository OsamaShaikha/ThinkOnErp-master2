using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

/// <summary>Relays durable HR outbox events to the Accounting inbox using one database transaction.</summary>
public sealed class AccountingHandoffOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<AccountingHandoffOutboxWorker> logger;
    private readonly HrTenantSchemaCatalog? tenantSchemas;

    public AccountingHandoffOutboxWorker(
        IServiceScopeFactory scopes,
        ILogger<AccountingHandoffOutboxWorker> logger,
        IConfiguration? configuration = null)
    {
        this.scopes = scopes;
        this.logger = logger;
        var connectionString = configuration?.GetConnectionString("OracleDb");
        if (!string.IsNullOrWhiteSpace(connectionString))
            tenantSchemas = new HrTenantSchemaCatalog(connectionString);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunTenantCycleAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Accounting handoff outbox tenant cycle failed; pending events will be retried."); }
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task RunTenantCycleAsync(CancellationToken ct)
    {
        if (tenantSchemas == null)
        {
            logger.LogWarning("Connection string 'OracleDb' is not configured; tenant outbox cycle skipped.");
            return;
        }

        var tenants = await tenantSchemas.GetActiveTenantSchemasAsync(ct);
        foreach (var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();
            if (!HrTenantSchemaCatalog.IsValidSchema(tenant.Schema))
            {
                logger.LogError("Company {CompanyId} has an invalid tenant schema identifier; its outbox was skipped.", tenant.CompanyId);
                continue;
            }

            try { await RelayTenantBatchAsync(tenant.Schema, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Accounting handoff outbox relay failed for tenant {CompanyId}; pending events will be retried.", tenant.CompanyId);
            }
        }
    }

    private async Task RelayTenantBatchAsync(string schema, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OracleDbContext>();
        await db.Database.OpenConnectionAsync(ct);
        var connection = db.Database.GetDbConnection();
        try
        {
            await HrTenantSchemaCatalog.SetCurrentSchemaAsync(connection, schema, ct);
            await RelayBatchAsync(db, ct);
        }
        finally
        {
            try { await HrTenantSchemaCatalog.SetCurrentSchemaAsync(connection, tenantSchemas!.GetMasterSchema(), CancellationToken.None); }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Could not reset Oracle session after outbox processing for schema {Schema}; clearing its pool.", schema);
                if (connection is OracleConnection oracleConnection) OracleConnection.ClearPool(oracleConnection);
            }
            await db.Database.CloseConnectionAsync();
        }
    }

    internal async Task RelayBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OracleDbContext>();
        await RelayBatchAsync(db, ct);
    }

    private async Task RelayBatchAsync(OracleDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var batch = await LoadDueBatchAsync(db, now, ct);
        HrBackgroundMetrics.OutboxBatches.Add(1, new KeyValuePair<string, object?>("outcome", "selected"));
        HrBackgroundMetrics.OutboxMessages.Add(batch.Count, new KeyValuePair<string, object?>("outcome", "selected"));
        foreach (var message in batch)
        {
            try
            {
                var outcome = await RelayOneAsync(db, message.Id, ct);
                HrBackgroundMetrics.OutboxMessages.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                HrBackgroundMetrics.OutboxMessages.Add(1, new KeyValuePair<string, object?>("outcome", "retry"));
                logger.LogWarning(ex, "Accounting handoff outbox message {OutboxMessageId} will be retried with backoff.", message.Id);
                db.ChangeTracker.Clear();
                try { await RecordFailureAsync(db, message.Id, ex, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception failureEx)
                {
                    logger.LogError(failureEx, "Could not record retry state for Accounting handoff outbox message {OutboxMessageId}.", message.Id);
                    db.ChangeTracker.Clear();
                }
            }
        }
    }

    private static async Task<List<ThinkOnErp.Domain.Entities.Hr.HrAccountingOutboxMessage>> LoadDueBatchAsync(
        OracleDbContext db, DateTime now, CancellationToken ct)
    {
        return await QueryDueMessages(db, now).Take(25).ToListAsync(ct);
    }

    internal static IQueryable<ThinkOnErp.Domain.Entities.Hr.HrAccountingOutboxMessage> QueryDueMessages(
        OracleDbContext db, DateTime now)
    {
        var retryAfter5Seconds = now.AddSeconds(-5);
        var retryAfter10Seconds = now.AddSeconds(-10);
        var retryAfter20Seconds = now.AddSeconds(-20);
        var retryAfter40Seconds = now.AddSeconds(-40);
        var retryAfter80Seconds = now.AddSeconds(-80);
        var retryAfter160Seconds = now.AddSeconds(-160);
        var retryAfter320Seconds = now.AddSeconds(-320);
        var retryAfter640Seconds = now.AddSeconds(-640);
        var retryAfter900Seconds = now.AddSeconds(-900);

        return db.HrAccountingOutboxMessages.AsNoTracking()
            .Where(x => x.Status == "PENDING" &&
                (!x.LastAttemptAtUtc.HasValue ||
                 (x.AttemptCount <= 1 && x.LastAttemptAtUtc <= retryAfter5Seconds) ||
                 (x.AttemptCount == 2 && x.LastAttemptAtUtc <= retryAfter10Seconds) ||
                 (x.AttemptCount == 3 && x.LastAttemptAtUtc <= retryAfter20Seconds) ||
                 (x.AttemptCount == 4 && x.LastAttemptAtUtc <= retryAfter40Seconds) ||
                 (x.AttemptCount == 5 && x.LastAttemptAtUtc <= retryAfter80Seconds) ||
                 (x.AttemptCount == 6 && x.LastAttemptAtUtc <= retryAfter160Seconds) ||
                 (x.AttemptCount == 7 && x.LastAttemptAtUtc <= retryAfter320Seconds) ||
                 (x.AttemptCount == 8 && x.LastAttemptAtUtc <= retryAfter640Seconds) ||
                 (x.AttemptCount >= 9 && x.LastAttemptAtUtc <= retryAfter900Seconds)))
            .OrderBy(x => x.LastAttemptAtUtc.HasValue ? 1 : 0)
            .ThenBy(x => x.LastAttemptAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id);
    }

    internal static TimeSpan GetRetryDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 8);
        return TimeSpan.FromSeconds(Math.Min(900, 5 * Math.Pow(2, exponent)));
    }

    internal static bool IsRetryDue(ThinkOnErp.Domain.Entities.Hr.HrAccountingOutboxMessage message, DateTime now)
    {
        return !message.LastAttemptAtUtc.HasValue || message.LastAttemptAtUtc.Value <= now - GetRetryDelay(message.AttemptCount);
    }

    private static async Task<string> RelayOneAsync(OracleDbContext db, long messageId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await db.HrAccountingOutboxMessages.SingleOrDefaultAsync(x => x.Id == messageId && x.Status == "PENDING", ct);
        if (current == null) { await tx.RollbackAsync(ct); return "already_claimed"; }
        var existing = await db.AccountingHandoffInboxMessages.SingleOrDefaultAsync(x => x.EventKey == current.EventKey, ct);
        if (existing == null)
        {
            var isWithdrawal = current.EventType == "PAYROLL_WITHDRAW";
            db.AccountingHandoffInboxMessages.Add(new ThinkOnErp.Domain.Entities.Hr.AccountingHandoffInboxMessage
            {
                HandoffId = current.HandoffId, EventKey = current.EventKey, PayloadHash = current.PayloadHash,
                ReceivedAtUtc = DateTime.UtcNow, Status = isWithdrawal ? "WITHDRAWN" : "RECEIVED"
            });
        }
        else if (existing.HandoffId != current.HandoffId ||
                 !string.Equals(existing.PayloadHash, current.PayloadHash, StringComparison.Ordinal))
        {
            current.Status = "CONFLICT";
            current.LastError = "Inbox event key already exists with a different payload hash or handoff.";
            current.LastAttemptAtUtc = DateTime.UtcNow;
            current.AttemptCount++;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return "conflict";
        }
        current.Status = "DELIVERED";
        current.LastAttemptAtUtc = DateTime.UtcNow;
        current.LastError = null;
        current.AttemptCount++;
        var handoff = await db.HrAccountingHandoffs.SingleAsync(x => x.Id == current.HandoffId, ct);
        if (current.EventType != "PAYROLL_WITHDRAW" && handoff.DeliveryStatus == "QUEUED")
        {
            handoff.DeliveryStatus = "DELIVERED";
            handoff.Transitions.Add(new ThinkOnErp.Domain.Entities.Hr.HrAccountingHandoffTransition
                {
                    FromStatus = "QUEUED", ToStatus = "DELIVERED", Actor = "OUTBOX", OccurredAtUtc = DateTime.UtcNow
                });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return "delivered";
    }

    private static async Task RecordFailureAsync(OracleDbContext db, long messageId, Exception error, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await db.HrAccountingOutboxMessages.SingleOrDefaultAsync(x => x.Id == messageId && x.Status == "PENDING", ct);
        if (current == null) { await tx.RollbackAsync(ct); return; }
        current.AttemptCount++;
        current.LastAttemptAtUtc = DateTime.UtcNow;
        var reason = error.GetBaseException().Message;
        current.LastError = reason.Length <= 1000 ? reason : reason[..1000];
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
