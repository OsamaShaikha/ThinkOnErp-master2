using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrLeaveWorkerVerification
{
    public static async Task VerifyDisabledTenantAsync(string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        var before = await GetBalanceAggregateAsync(safeConnectionString);
        HrLeaveSettings settings;
        await using (var connection = new OracleConnection(safeConnectionString))
        {
            await connection.OpenAsync();
            await SetTemplateSchemaAsync(connection);
            var options = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
            await using var db = new OracleDbContext(options);
            settings = await new LeaveRepository(db).GetHrLeaveSettingsAsync();
        }

        if (settings.AutomaticAccrualEnabled)
            throw new InvalidOperationException("Refusing worker verification because automatic accrual is enabled for DEV_TEMPLATE.");

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OracleDb"] = safeConnectionString
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<OracleDbContext>(options => options.UseOracle(safeConnectionString,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)));
        services.AddScoped<ILeaveRepository, LeaveRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ILeaveService, LeaveService>();
        await using var provider = services.BuildServiceProvider();
        var worker = new LeaveAccrualBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), configuration,
            NullLogger<LeaveAccrualBackgroundService>.Instance);

        // This bypasses the tenant registry and invokes only the template schema's real tenant-processing path.
        await worker.ProcessTenantAsync("DEV_TEMPLATE", CancellationToken.None);

        var after = await GetBalanceAggregateAsync(safeConnectionString);
        if (!string.Equals(before, after, StringComparison.Ordinal))
            throw new InvalidOperationException("Disabled leave worker changed the DEV_TEMPLATE balance aggregate.");
        report.Add("Oracle LEV-11 disabled-worker check: processed only DEV_TEMPLATE, observed the disabled opt-in, returned without accrual, and left the balance aggregate unchanged.");
    }

    private static async Task<string> GetBalanceAggregateAsync(string connectionString)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();
        await SetTemplateSchemaAsync(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*), NVL(SUM(ACCRUED_DAYS),0), NVL(SUM(USED_DAYS),0), NVL(SUM(CARRIED_FORWARD_DAYS),0) FROM HR_LEAVE_BALANCE";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("Could not read the DEV_TEMPLATE leave balance aggregate.");
        return string.Join("|", Enumerable.Range(0, reader.FieldCount)
            .Select(index => Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)));
    }

    private static async Task SetTemplateSchemaAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA = DEV_TEMPLATE";
        await command.ExecuteNonQueryAsync();
    }
}
