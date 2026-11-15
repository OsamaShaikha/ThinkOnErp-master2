using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

/// <summary>Processes safe, retry-due raw attendance events across active tenant schemas.</summary>
public sealed class RawAttendanceProcessingBackgroundService : BackgroundService
{
    private const string SchedulerActor = "SYSTEM:HR_ATTENDANCE_RAW";
    private const string ConfigPrefix = "HrAttendance:RawProcessing:BackgroundService";
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RawAttendanceProcessingBackgroundService> _logger;
    private readonly HrTenantSchemaCatalog? _tenantSchemas;

    public RawAttendanceProcessingBackgroundService(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        ILogger<RawAttendanceProcessingBackgroundService> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _logger = logger;
        var connStr = configuration.GetConnectionString("OracleDb");
        if (!string.IsNullOrWhiteSpace(connStr))
        {
            _tenantSchemas = new HrTenantSchemaCatalog(connStr);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>($"{ConfigPrefix}:Enabled"))
        {
            _logger.LogInformation("HR raw attendance background processing is disabled by configuration.");
            return;
        }

        var intervalSeconds = _configuration.GetValue<int?>($"{ConfigPrefix}:IntervalSeconds") ?? 60;
        var retryDelayMinutes = _configuration.GetValue<int?>($"{ConfigPrefix}:RetryDelayMinutes") ?? 5;
        var batchSize = _configuration.GetValue<int?>($"{ConfigPrefix}:BatchSize") ?? 50;
        if (intervalSeconds is < 10 or > 3600 || retryDelayMinutes is < 1 or > 1440 || batchSize is < 1 or > 100)
        {
            _logger.LogCritical("HR raw attendance background processing has invalid configuration; expected interval 10..3600 seconds, retry delay 1..1440 minutes, and batch size 1..100.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunCycleAsync(batchSize, retryDelayMinutes, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "HR raw attendance processing cycle failed; eligible events will be retried."); }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task RunCycleAsync(int batchSize, int retryDelayMinutes, CancellationToken cancellationToken)
    {
        if (_tenantSchemas == null)
        {
            _logger.LogWarning("Connection string 'OracleDb' is not configured; tenant attendance cycle skipped.");
            return;
        }

        var tenants = await _tenantSchemas.GetActiveTenantSchemasAsync(cancellationToken);
        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HrTenantSchemaCatalog.IsValidSchema(tenant.Schema))
            {
                _logger.LogError("Company {CompanyId} has an invalid tenant schema identifier; automatic attendance processing was skipped.", tenant.CompanyId);
                continue;
            }
            try { await ProcessTenantAsync(tenant.CompanyId, tenant.Schema, batchSize, retryDelayMinutes, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { _logger.LogError(ex, "HR raw attendance processing failed for tenant {CompanyId}; its eligible events will be retried.", tenant.CompanyId); }
        }
    }

    internal async Task ProcessTenantAsync(long companyId, string schema, int batchSize, int retryDelayMinutes, CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OracleDbContext>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        try
        {
            if (_tenantSchemas != null)
            {
                await HrTenantSchemaCatalog.SetCurrentSchemaAsync(connection, schema, cancellationToken);
            }
            var repository = scope.ServiceProvider.GetRequiredService<IAttendanceCorrectionRepository>();
            var processor = scope.ServiceProvider.GetRequiredService<IRawAttendanceProcessingService>();
            await ProcessTenantEventsAsync(companyId, repository, processor, db, batchSize, retryDelayMinutes, cancellationToken);
        }
        finally
        {
            if (_tenantSchemas != null)
            {
                try { await HrTenantSchemaCatalog.SetCurrentSchemaAsync(connection, _tenantSchemas.GetMasterSchema(), CancellationToken.None); }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Could not reset Oracle session after tenant HR attendance processing for schema {Schema}; clearing its pool.", schema);
                    if (connection is OracleConnection oracleConnection) OracleConnection.ClearPool(oracleConnection);
                }
            }
            await db.Database.CloseConnectionAsync();
        }
    }

    internal async Task ProcessTenantEventsAsync(
        long companyId,
        IAttendanceCorrectionRepository repository,
        IRawAttendanceProcessingService processor,
        OracleDbContext? db,
        int batchSize,
        int retryDelayMinutes,
        CancellationToken cancellationToken)
    {
        var retryBeforeUtc = DateTime.UtcNow.AddMinutes(-retryDelayMinutes);
        var pending = await repository.GetAutomaticallyProcessableRawPunchesAsync(batchSize, retryBeforeUtc, cancellationToken);
        HrBackgroundMetrics.RawAttendanceBatches.Add(1, new KeyValuePair<string, object?>("outcome", "selected"));
        HrBackgroundMetrics.RawAttendanceEvents.Add(pending.Count, new KeyValuePair<string, object?>("outcome", "selected"));
        foreach (var raw in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await processor.ProcessAsync(raw.Id, SchedulerActor, cancellationToken);
                if (result.NeedsReview)
                {
                    HrBackgroundMetrics.RawAttendanceEvents.Add(1, new KeyValuePair<string, object?>("outcome", "review"));
                    _logger.LogWarning("HR raw attendance event {RawAttendanceId} for tenant {CompanyId} was isolated for review.", raw.Id, companyId);
                }
                else if (result.Applied)
                {
                    HrBackgroundMetrics.RawAttendanceEvents.Add(1, new KeyValuePair<string, object?>("outcome", "applied"));
                    _logger.LogInformation("HR raw attendance event {RawAttendanceId} was processed for tenant {CompanyId}.", raw.Id, companyId);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (InvalidOperationException ex)
            {
                HrBackgroundMetrics.RawAttendanceEvents.Add(1, new KeyValuePair<string, object?>("outcome", "review"));
                _logger.LogWarning("HR raw attendance event {RawAttendanceId} for tenant {CompanyId} requires review: {Reason}", raw.Id, companyId, ex.Message);
            }
            catch (ArgumentException ex)
            {
                HrBackgroundMetrics.RawAttendanceEvents.Add(1, new KeyValuePair<string, object?>("outcome", "review"));
                _logger.LogWarning("HR raw attendance event {RawAttendanceId} for tenant {CompanyId} requires review: {Reason}", raw.Id, companyId, ex.Message);
            }
            catch (Exception ex)
            {
                HrBackgroundMetrics.RawAttendanceEvents.Add(1, new KeyValuePair<string, object?>("outcome", "transient_failure"));
                _logger.LogError(ex, "HR raw attendance event {RawAttendanceId} failed transiently for tenant {CompanyId}; it will be retried after the configured delay.", raw.Id, companyId);
                db?.ChangeTracker.Clear();
            }
        }
    }
}
