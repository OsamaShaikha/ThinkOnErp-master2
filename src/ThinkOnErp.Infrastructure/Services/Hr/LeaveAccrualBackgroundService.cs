using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

/// <summary>
/// Runs idempotent leave accrual targets only for active tenant schemas whose HR settings explicitly enable it.
/// </summary>
public sealed class LeaveAccrualBackgroundService : BackgroundService
{
    private const string SchedulerActor = "SYSTEM:HR_LEAVE_ACCRUAL";
    private readonly HashSet<string> _warnedForMissingSettingsColumns = new(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string, DateTime> _completedLocalDates = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LeaveAccrualBackgroundService> _logger;
    private readonly HrTenantSchemaCatalog? _tenantSchemas;

    public LeaveAccrualBackgroundService(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        ILogger<LeaveAccrualBackgroundService> logger)
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
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunCycleAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Tenant-aware HR leave accrual cycle failed; the next cycle will retry."); }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        if (_tenantSchemas == null)
        {
            _logger.LogWarning("Connection string 'OracleDb' is not configured; tenant accrual cycle skipped.");
            return;
        }

        var tenants = await _tenantSchemas.GetActiveTenantSchemasAsync(cancellationToken);
        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HrTenantSchemaCatalog.IsValidSchema(tenant.Schema))
            {
                _logger.LogError("Company {CompanyId} has an invalid tenant schema identifier; automatic leave accrual was skipped.", tenant.CompanyId);
                continue;
            }
            try { await ProcessTenantAsync(tenant.Schema, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR leave accrual failed for tenant {CompanyId}; it will be retried on a later cycle.", tenant.CompanyId);
            }
        }
    }

    internal async Task ProcessTenantAsync(string schema, CancellationToken cancellationToken)
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
            var leaveRepository = scope.ServiceProvider.GetRequiredService<ILeaveRepository>();
            var leaveService = scope.ServiceProvider.GetRequiredService<ILeaveService>();
            await ProcessTenantSettingsAndAccrualsAsync(schema, leaveRepository, leaveService, DateTime.UtcNow, cancellationToken);
        }
        finally
        {
            if (_tenantSchemas != null)
            {
                try { await HrTenantSchemaCatalog.SetCurrentSchemaAsync(connection, _tenantSchemas.GetMasterSchema(), CancellationToken.None); }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Could not reset Oracle session after tenant HR accrual for schema {Schema}; clearing its pool.", schema);
                    if (connection is OracleConnection oracleConnection) OracleConnection.ClearPool(oracleConnection);
                }
            }
            await db.Database.CloseConnectionAsync();
        }
    }

    internal async Task ProcessTenantSettingsAndAccrualsAsync(
        string schema,
        ILeaveRepository leaveRepository,
        ILeaveService leaveService,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        HrLeaveSettings settings;
        try { settings = await leaveRepository.GetHrLeaveSettingsAsync(cancellationToken); }
        catch (OracleException ex) when (ex.Number == 904)
        {
            if (_warnedForMissingSettingsColumns.Add(schema))
                _logger.LogWarning(ex, "HR accrual scheduler skipped tenant schema {Schema}: its HR settings migration is missing.", schema);
            return;
        }
        if (!settings.AutomaticAccrualEnabled) return;
        if (string.IsNullOrWhiteSpace(settings.AccrualTimeZoneId))
        {
            _logger.LogWarning("HR accrual scheduler skipped tenant schema {Schema}: enabled setting has no time zone.", schema);
            return;
        }

        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.AccrualTimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogWarning(ex, "HR accrual scheduler skipped tenant schema {Schema}: its configured time zone is invalid.", schema);
            return;
        }

        var runDates = LeaveAccrualSchedule.Resolve(utcNow, timeZone);
        var completedLocalDate = runDates.LastCompletedLocalDate.ToDateTime(TimeOnly.MinValue);
        if (_completedLocalDates.TryGetValue(schema, out var completed) && completed == completedLocalDate) return;
        var previousMonthEnd = runDates.PreviousMonthEnd.ToDateTime(TimeOnly.MinValue);
        var policies = await leaveService.GetLeavePoliciesAsync(cancellationToken);
        var leaveTypeCodes = policies.Where(p => p.IsActive).Select(p => p.LeaveTypeCode)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var cycleSucceeded = true;

        foreach (var leaveTypeCode in leaveTypeCodes)
        {
            cycleSucceeded &= await RunAccrualKindAsync(schema, leaveTypeCode, "DAILY", () => leaveService.RunDailyAccrualAsync(
                new DailyLeaveAccrualDto(completedLocalDate, leaveTypeCode, null, null), SchedulerActor, cancellationToken));
            cycleSucceeded &= await RunAccrualKindAsync(schema, leaveTypeCode, "MONTHLY", () => leaveService.RunMonthlyAccrualAsync(
                new MonthlyLeaveAccrualDto(previousMonthEnd.Year, previousMonthEnd.Month, leaveTypeCode, null), SchedulerActor, cancellationToken));
            cycleSucceeded &= await RunAccrualKindAsync(schema, leaveTypeCode, "YEARLY", () => leaveService.RunYearlyAllocationAsync(
                new YearlyLeaveAllocationDto(completedLocalDate.Year, leaveTypeCode, null, null, false, completedLocalDate), SchedulerActor, cancellationToken));
        }
        if (cycleSucceeded) _completedLocalDates[schema] = completedLocalDate;
        _warnedForMissingSettingsColumns.Remove(schema);
    }

    private async Task<bool> RunAccrualKindAsync(string schema, string leaveTypeCode, string kind,
        Func<Task<LeaveAccrualResultDto>> run)
    {
        try
        {
            var result = await run();
            if (result.UpdatedBalancesCount > 0)
                _logger.LogInformation("HR {AccrualKind} accrual applied for tenant schema {Schema}, leave type {LeaveTypeCode}: {EmployeeCount} employees, {Days} days.",
                    kind, schema, leaveTypeCode, result.UpdatedBalancesCount, result.TotalDaysAccrued);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HR {AccrualKind} accrual failed for tenant schema {Schema}, leave type {LeaveTypeCode}; it will be retried.",
                kind, schema, leaveTypeCode);
            return false;
        }
    }

}
