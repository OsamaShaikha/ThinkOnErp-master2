using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.API.Middleware;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;
using Xunit.Abstractions;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class PayrollCurrencyPrecisionOracleHttpTests(ITestOutputHelper output)
{
    private const string AuthScheme = "HrOracleVerification";
    private const string CurrencyCode = "HRHTTP";

    [Fact]
    public async Task AttendanceOracleHttpWritesUseDevTemplateAndRollbackAfterRestoringMasterSchema()
    {
        var configured = Environment.GetEnvironmentVariable("HR_ORACLE_VERIFY_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured))
        {
            output.WriteLine("Oracle attendance HTTP integration was not run. Set HR_ORACLE_VERIFY_CONNECTION_STRING to enable this DEV_TEMPLATE-only rollback check.");
            return;
        }

        var connectionString = new OracleConnectionStringBuilder(configured) { ConnectionTimeout = 10, Pooling = false }.ConnectionString;
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();
        var masterSchema = new OracleConnectionStringBuilder(connectionString).UserID?.Trim().Trim('"')
            ?? throw new InvalidOperationException("Oracle connection user is not configured.");
        var branch = await ReadDevTemplateBranchAsync(connection);
        await using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        var observedSchemas = new ConcurrentQueue<string>();
        var schemaCapture = new CurrentSchemaCaptureInterceptor(observedSchemas);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration["ConnectionStrings:OracleDb"] = connectionString;
        builder.Services.AddHttpContextAccessor();
        ITenantSchemaResolver tenantResolver = new OracleTenantSchemaResolver(builder.Configuration, NullLogger<OracleTenantSchemaResolver>.Instance);
        var registeredTenant = await tenantResolver.ResolveAsync(branch.CompanyId, null, "DEV_TEMPLATE", CancellationToken.None);
        var resolvedFromRegistry = registeredTenant != null;
        if (resolvedFromRegistry)
            Assert.Equal(branch.CompanyId, registeredTenant!.CompanyId);
        else
            tenantResolver = new VerificationTenantResolver();
        builder.Services.AddSingleton<ITenantSchemaResolver>(tenantResolver);
        builder.Services.AddSingleton<ITenantSchemaSession, OracleTenantSchemaSession>();
        builder.Services.AddAuthentication(AuthScheme).AddScheme<AuthenticationSchemeOptions, VerificationAuthenticationHandler>(AuthScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<EmployeeFieldAccess>();
        var dbOptions = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle(connection, options => options.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .AddInterceptors(schemaCapture)
            .Options;
        builder.Services.AddScoped(_ =>
        {
            var db = new OracleDbContext(dbOptions);
            db.Database.UseTransaction(transaction);
            return db;
        });
        builder.Services.AddScoped<IAttendanceTimeSettingsRepository, AttendanceTimeSettingsRepository>();
        builder.Services.AddScoped<IBranchAttendanceLocationRepository, AttendanceBranchRepository>();
        builder.Services.AddControllers(options => options.Filters.Add<HrAccessFilter>())
            .AddApplicationPart(typeof(AttendanceController).Assembly);

        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseMiddleware<SchemaRoutingMiddleware>(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var settingsRequest = new HttpRequestMessage(HttpMethod.Get, "api/hr/attendance/time-settings");
            settingsRequest.Headers.Add("X-Company-Id", branch.CompanyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var settingsResponse = await client.SendAsync(settingsRequest);
            Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
            Assert.Equal(masterSchema, await ReadCurrentSchemaAsync(connection, transaction));

            var path = $"api/hr/attendance/branches/{branch.BranchId}/location";
            using var createRequest = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new WriteBranchAttendanceLocationDto(0m, 0m, 155.5m, true))
            };
            createRequest.Headers.Add("X-Company-Id", branch.CompanyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var createResponse = await client.SendAsync(createRequest);
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
            var created = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            Assert.Equal(branch.CompanyId, created.GetProperty("companyId").GetInt64());
            Assert.Equal(155.5m, created.GetProperty("radiusMeters").GetDecimal());
            var originalVersion = createResponse.Headers.ETag?.ToString() ?? throw new InvalidOperationException("Create response omitted ETag.");
            Assert.Equal(masterSchema, await ReadCurrentSchemaAsync(connection, transaction));

            using var updateRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new WriteBranchAttendanceLocationDto(0m, 0m, 200m, true))
            };
            updateRequest.Headers.Add("X-Company-Id", branch.CompanyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            updateRequest.Headers.TryAddWithoutValidation("If-Match", originalVersion);
            using var updateResponse = await client.SendAsync(updateRequest);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = (await updateResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            Assert.Equal(200m, updated.GetProperty("radiusMeters").GetDecimal());
            Assert.Equal("HR_HTTP_VERIFY", updated.GetProperty("creationUser").GetString());
            Assert.Equal("HR_HTTP_VERIFY", updated.GetProperty("updateUser").GetString());
            var currentVersion = updateResponse.Headers.ETag?.ToString() ?? throw new InvalidOperationException("Update response omitted ETag.");
            Assert.NotEqual(originalVersion, currentVersion);
            Assert.Equal(masterSchema, await ReadCurrentSchemaAsync(connection, transaction));

            using var staleRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new WriteBranchAttendanceLocationDto(1m, 1m, 20m, true))
            };
            staleRequest.Headers.Add("X-Company-Id", branch.CompanyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            staleRequest.Headers.TryAddWithoutValidation("If-Match", originalVersion);
            using var staleResponse = await client.SendAsync(staleRequest);
            Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
            Assert.Equal(masterSchema, await ReadCurrentSchemaAsync(connection, transaction));
        }
        finally
        {
            try { await app.StopAsync(); }
            finally { await transaction.RollbackAsync(); }
        }

        Assert.Contains(observedSchemas, schema => string.Equals(schema, "DEV_TEMPLATE", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(masterSchema, await ReadCurrentSchemaAsync(connection));
        Assert.Equal(0, await CountDevTemplateBranchLocationAsync(connection, branch.BranchId));
        output.WriteLine(resolvedFromRegistry
            ? "Kestrel attendance GET/POST/PUT/stale PUT resolved through SYS_COMPANY and ran with Oracle CURRENT_SCHEMA=DEV_TEMPLATE; every request restored the master schema and outer rollback left no location row."
            : "No active SYS_COMPANY registry mapping exists for DEV_TEMPLATE; Kestrel attendance GET/POST/PUT/stale PUT used the fixed DEV_TEMPLATE test resolver, with CURRENT_SCHEMA restored after each request and all writes rolled back.");
    }

    private static async Task<(long CompanyId, long BranchId)> ReadDevTemplateBranchAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT b.COMPANY_ID, b.\"Id\" FROM DEV_TEMPLATE.SYS_BRANCH b WHERE b.IS_ACTIVE=1 AND b.COMPANY_ID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DEV_TEMPLATE.HR_BRANCH_ATTENDANCE_LOCATION l WHERE l.BRANCH_ID=b.\"Id\") FETCH FIRST 1 ROWS ONLY";
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
        if (!await reader.ReadAsync()) throw new InvalidOperationException("DEV_TEMPLATE has no active unconfigured company branch for the rollback-only attendance HTTP check.");
        return (Convert.ToInt64(reader.GetValue(0)), Convert.ToInt64(reader.GetValue(1)));
    }

    private static async Task<int> CountDevTemplateBranchLocationAsync(OracleConnection connection, long branchId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM DEV_TEMPLATE.HR_BRANCH_ATTENDANCE_LOCATION WHERE BRANCH_ID=:branchId";
        command.Parameters.Add("branchId", OracleDbType.Int64).Value = branchId;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ReadCurrentSchemaAsync(OracleConnection connection, DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction as OracleTransaction;
        command.CommandText = "SELECT SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL";
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("Oracle did not return CURRENT_SCHEMA.");
    }

    [Fact]
    public async Task RealHttpCrudRoutesUseOracleRepositoryAndKeepCompanyPrecisionIsolated()
    {
        var configured = Environment.GetEnvironmentVariable("HR_ORACLE_VERIFY_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured))
        {
            output.WriteLine("Oracle HTTP integration was not run. Set HR_ORACLE_VERIFY_CONNECTION_STRING to enable this DEV_TEMPLATE-only check.");
            return;
        }

        var connectionString = new OracleConnectionStringBuilder(configured) { ConnectionTimeout = 10, Pooling = false }.ConnectionString;
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        var company1 = DateTime.UtcNow.Ticks + 3_000_000_000_000_000_000L;
        var company2 = company1 + 1;

        try
        {
            await EnsureUnusedAsync(connection, company1, company2);
            await RunHttpWorkflowAsync(connection, transaction, connectionString, company1, company2);
        }
        finally
        {
            try { await transaction.RollbackAsync(); } catch { }
        }

        Assert.Equal(0, await CountRowsAsync(connection, company1, company2));
        output.WriteLine("Oracle Kestrel/MVC GET/PUT/DELETE passed for two isolated company contexts; transaction rolled back and no verification rows remain.");
    }

    [Fact]
    public async Task RealHttpPayrollPolicyWriterRejectsApprovedPeriodAndLeavesNoPolicy()
    {
        var configured = Environment.GetEnvironmentVariable("HR_ORACLE_VERIFY_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured))
        {
            output.WriteLine("Oracle policy-writer HTTP integration was not run. Set HR_ORACLE_VERIFY_CONNECTION_STRING to enable this DEV_TEMPLATE-only check.");
            return;
        }

        var connectionString = new OracleConnectionStringBuilder(configured) { ConnectionTimeout = 10, Pooling = false }.ConnectionString;
        var periodCode = "P" + Guid.NewGuid().ToString("N")[..18];
        var policyCode = "HTTP_" + Guid.NewGuid().ToString("N")[..20];
        var allowedPolicyCode = "HTTP_OK_" + Guid.NewGuid().ToString("N")[..18];
        var overtimeCode = "HTTP_OT_" + Guid.NewGuid().ToString("N")[..18];
        var sscCode = "HTTP_SSC_" + Guid.NewGuid().ToString("N")[..16];
        var taxCode = "HTTP_TAX_" + Guid.NewGuid().ToString("N")[..16];
        var deductionCode = "HTTP_DED_" + Guid.NewGuid().ToString("N")[..16];
        var precisionCode = "HP" + Guid.NewGuid().ToString("N")[..8];
        const string marker = "HR_POLICY_HTTP_VERIFY";
        long periodId = 0;
        long runId = 0;
        long companyId = 0;
        var periodStart = new DateTime(9997, 1, 1);

        await using (var setupConnection = new OracleConnection(connectionString))
        {
            await setupConnection.OpenAsync();
            await SetTemplateSchemaAsync(setupConnection);
            var options = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(setupConnection).Options;
            using var setupDb = new OracleDbContext(options);
            var employee = await (from e in setupDb.Employees.AsNoTracking()
                join branch in setupDb.SysBranches.AsNoTracking() on e.BranchId equals (long?)branch.Id
                where e.IsActive && e.EmploymentStatus == "ACTIVE" && branch.IsActive && branch.CompanyId.HasValue
                select new { e.EmployeeCode, e.BranchId, CompanyId = branch.CompanyId!.Value }).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Oracle policy HTTP fixture requires an active employee and company branch.");
            companyId = employee.CompanyId;
            var nextVersion = (await setupDb.PayrollRuns.IgnoreQueryFilters().AsNoTracking()
                .Where(r => r.CorrectionRootRunId == null).Select(r => (int?)r.CorrectionVersion).MaxAsync() ?? 0) + 1;
            var period = new PayrollPeriod
            {
                CompanyId = companyId, PeriodCode = periodCode, NameAr = "فحص قفل السياسة", NameEn = "Policy lock check",
                FiscalYear = periodStart.Year, Month = periodStart.Month, StartDate = periodStart,
                EndDate = periodStart.AddDays(30), PayDate = periodStart.AddDays(30), PayrollType = "SPECIAL", Status = "OPEN",
                CreationUser = marker, CreationDate = DateTime.UtcNow
            };
            var run = new PayrollRun
            {
                PayPeriod = periodCode, RunDate = period.EndDate, BranchId = employee.BranchId, Status = "APPROVED",
                CorrectionVersion = nextVersion, ApprovedBy = marker, ApprovalDate = DateTime.UtcNow,
                CreationUser = marker, CreationDate = DateTime.UtcNow
            };
            run.Lines.Add(new PayrollRunLine
            {
                EmployeeCode = employee.EmployeeCode, BranchId = employee.BranchId, CurrencyCode = "JOD",
                PaymentMethod = "BANK_TRANSFER", Status = "APPROVED", CreationUser = marker, CreationDate = DateTime.UtcNow
            });
            await using var seed = await setupDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            setupDb.PayrollPeriods.Add(period);
            setupDb.PayrollRuns.Add(run);
            setupDb.PayrollCurrencyPrecisions.Add(new PayrollCurrencyPrecision
            {
                CompanyId = companyId, CurrencyCode = precisionCode, DecimalPlaces = 2,
                CreationUser = marker, CreationDate = DateTime.UtcNow
            });
            await setupDb.SaveChangesAsync();
            periodId = period.Id;
            runId = run.Id;
            await seed.CommitAsync();
        }

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Configuration["ConnectionStrings:OracleDb"] = connectionString;
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<ITenantSchemaResolver, VerificationTenantResolver>();
            builder.Services.AddSingleton<ITenantSchemaSession, OracleTenantSchemaSession>();
            builder.Services.AddAuthentication(AuthScheme).AddScheme<AuthenticationSchemeOptions, VerificationAuthenticationHandler>(AuthScheme, _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddScoped<EmployeeFieldAccess>();
            builder.Services.AddDbContext<OracleDbContext>(options => options.UseOracle(connectionString));
            builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
            builder.Services.AddScoped<IHrUnitOfWork, HrUnitOfWork>();
            builder.Services.AddControllers(options => options.Filters.Add<HrAccessFilter>())
                .AddApplicationPart(typeof(PayrollPoliciesController).Assembly);

            await using var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseMiddleware<SchemaRoutingMiddleware>(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            try
            {
                var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                using var client = new HttpClient { BaseAddress = new Uri(address) };
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/hr/payroll-policies/proration");
                request.Headers.Add("X-Company-Id", companyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                request.Content = System.Net.Http.Json.JsonContent.Create(new ProrationPolicyDto(companyId, policyCode,
                    "تحقق", "Verification", periodStart, null, "CALENDAR_DAYS", false));
                using var response = await client.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();
                Assert.False(response.IsSuccessStatusCode, $"Policy write unexpectedly succeeded: {responseText}");
                Assert.Contains("Payroll policy changes affect approved payroll", responseText, StringComparison.OrdinalIgnoreCase);

                await AssertPolicyWriteRejectedAsync(client, companyId, "api/hr/payroll-policies/currency-precision",
                    new PayrollCurrencyPrecisionDto { CompanyId = companyId, CurrencyCode = precisionCode, DecimalPlaces = 4 }, HttpMethod.Put);
                await AssertPolicyWriteRejectedAsync(client, companyId,
                    $"api/hr/payroll-policies/currency-precision/{companyId}/{precisionCode}", null, HttpMethod.Delete);

                await AssertPolicyWriteRejectedAsync(client, companyId, "api/hr/payroll-policies/overtime",
                    new OvertimeRuleDto(companyId, overtimeCode, "تحقق", "Verification", periodStart, null,
                        "REGULAR", 1.25m, 0, 600, "MONTHLY_SALARY / 30", false));
                await AssertPolicyWriteRejectedAsync(client, companyId, "api/hr/payroll-policies/ssc",
                    new SSCPolicyDto(companyId, sscCode, "تحقق", "Verification", periodStart, null,
                        0.05m, 0.10m, 0m, 0m, 0m));
                await AssertPolicyWriteRejectedAsync(client, companyId, "api/hr/payroll-policies/tax",
                    new TaxPolicyDto(companyId, taxCode, "تحقق", "Verification", periodStart, null,
                        0m, 0m, 0m, 0m, false, "MONTHLY", new List<TaxBracketDto> { new(1, 0m, null, 0m, "verification") }));
                await AssertPolicyWriteRejectedAsync(client, companyId, "api/hr/payroll-policies/deductions",
                    new DeductionPolicyDto(companyId, deductionCode, "تحقق", "Verification", periodStart, null,
                        50m, 0m, false, false, false));

                var allowedStart = new DateTime(9998, 1, 1);
                using var allowedRequest = new HttpRequestMessage(HttpMethod.Post, "api/hr/payroll-policies/proration");
                allowedRequest.Headers.Add("X-Company-Id", companyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                allowedRequest.Content = System.Net.Http.Json.JsonContent.Create(new ProrationPolicyDto(companyId, allowedPolicyCode,
                    "مستقبل", "Future policy", allowedStart, null, "CALENDAR_DAYS", false));
                using var allowedResponse = await client.SendAsync(allowedRequest);
                var allowedResponseText = await allowedResponse.Content.ReadAsStringAsync();
                Assert.True(allowedResponse.IsSuccessStatusCode, $"Policy write outside approved periods unexpectedly failed: {allowedResponseText}");
            }
            finally { await app.StopAsync(); }

            await using var verifyConnection = new OracleConnection(connectionString);
            await verifyConnection.OpenAsync();
            await SetTemplateSchemaAsync(verifyConnection);
            using var verifyDb = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(verifyConnection).Options);
            Assert.False(await verifyDb.ProrationPolicies.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(p => p.CompanyId == companyId && p.Code == policyCode));
            Assert.False(await verifyDb.OvertimeRules.IgnoreQueryFilters().AsNoTracking().AnyAsync(p => p.CompanyId == companyId && p.Code == overtimeCode));
            Assert.False(await verifyDb.SSCPolicies.IgnoreQueryFilters().AsNoTracking().AnyAsync(p => p.CompanyId == companyId && p.Code == sscCode));
            Assert.False(await verifyDb.TaxPolicies.IgnoreQueryFilters().AsNoTracking().AnyAsync(p => p.CompanyId == companyId && p.Code == taxCode));
            Assert.False(await verifyDb.DeductionPolicies.IgnoreQueryFilters().AsNoTracking().AnyAsync(p => p.CompanyId == companyId && p.Code == deductionCode));
            var persistedPrecision = await verifyDb.PayrollCurrencyPrecisions.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(p => p.CompanyId == companyId && p.CurrencyCode == precisionCode);
            Assert.Equal(2, persistedPrecision.DecimalPlaces);
            Assert.True(await verifyDb.ProrationPolicies.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(p => p.CompanyId == companyId && p.Code == allowedPolicyCode));
            output.WriteLine("Oracle Kestrel policy POST was rejected by the real company/period guard; the unique policy was not persisted.");
        }
        finally
        {
            await using var cleanupConnection = new OracleConnection(connectionString);
            await cleanupConnection.OpenAsync();
            await SetTemplateSchemaAsync(cleanupConnection);
            using var cleanupDb = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(cleanupConnection).Options);
            await using var cleanup = await cleanupDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            var leakedPolicy = await cleanupDb.ProrationPolicies.IgnoreQueryFilters()
                .Where(p => p.CompanyId == companyId && (p.Code == policyCode || p.Code == allowedPolicyCode)).ToListAsync();
            if (leakedPolicy.Count > 0) cleanupDb.ProrationPolicies.RemoveRange(leakedPolicy);
            var leakedOvertime = await cleanupDb.OvertimeRules.IgnoreQueryFilters().Where(p => p.CompanyId == companyId && p.Code == overtimeCode).ToListAsync();
            if (leakedOvertime.Count > 0) cleanupDb.OvertimeRules.RemoveRange(leakedOvertime);
            var leakedSsc = await cleanupDb.SSCPolicies.IgnoreQueryFilters().Where(p => p.CompanyId == companyId && p.Code == sscCode).ToListAsync();
            if (leakedSsc.Count > 0) cleanupDb.SSCPolicies.RemoveRange(leakedSsc);
            var leakedTax = await cleanupDb.TaxPolicies.IgnoreQueryFilters().Where(p => p.CompanyId == companyId && p.Code == taxCode).ToListAsync();
            if (leakedTax.Count > 0) cleanupDb.TaxPolicies.RemoveRange(leakedTax);
            var leakedDeductions = await cleanupDb.DeductionPolicies.IgnoreQueryFilters().Where(p => p.CompanyId == companyId && p.Code == deductionCode).ToListAsync();
            if (leakedDeductions.Count > 0) cleanupDb.DeductionPolicies.RemoveRange(leakedDeductions);
            var leakedPrecision = await cleanupDb.PayrollCurrencyPrecisions.IgnoreQueryFilters()
                .Where(p => p.CompanyId == companyId && p.CurrencyCode == precisionCode).ToListAsync();
            if (leakedPrecision.Count > 0) cleanupDb.PayrollCurrencyPrecisions.RemoveRange(leakedPrecision);
            if (runId > 0)
            {
                var run = await cleanupDb.PayrollRuns.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.Id == runId && r.CreationUser == marker);
                if (run != null) cleanupDb.PayrollRuns.Remove(run);
            }
            if (periodId > 0)
            {
                var period = await cleanupDb.PayrollPeriods.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.Id == periodId && p.CreationUser == marker);
                if (period != null) cleanupDb.PayrollPeriods.Remove(period);
            }
            await cleanupDb.SaveChangesAsync();
            await cleanup.CommitAsync();
            Assert.False(await cleanupDb.PayrollPeriods.IgnoreQueryFilters().AnyAsync(p => p.Id == periodId && p.PeriodCode == periodCode));
            Assert.False(await cleanupDb.PayrollRuns.IgnoreQueryFilters().AnyAsync(r => r.Id == runId && r.PayPeriod == periodCode));
            Assert.False(await cleanupDb.ProrationPolicies.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyId && p.Code == policyCode));
            Assert.False(await cleanupDb.ProrationPolicies.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyId && p.Code == allowedPolicyCode));
            Assert.False(await cleanupDb.OvertimeRules.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyId && p.Code == overtimeCode));
            Assert.False(await cleanupDb.SSCPolicies.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyId && p.Code == sscCode));
            Assert.False(await cleanupDb.TaxPolicies.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyId && p.Code == taxCode));
            Assert.False(await cleanupDb.DeductionPolicies.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyId && p.Code == deductionCode));
            Assert.False(await cleanupDb.PayrollCurrencyPrecisions.IgnoreQueryFilters()
                .AnyAsync(p => p.CompanyId == companyId && p.CurrencyCode == precisionCode));
        }
    }

    [Fact]
    public async Task RealHttpRawAttendanceProcessingUsesOracleResolverAndApprovedPayrollGuard()
    {
        var configured = Environment.GetEnvironmentVariable("HR_ORACLE_VERIFY_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured))
        {
            output.WriteLine("Oracle raw-attendance HTTP integration was not run. Set HR_ORACLE_VERIFY_CONNECTION_STRING to enable this DEV_TEMPLATE-only check.");
            return;
        }

        const string marker = "HR_RAW_HTTP_VERIFY";
        var connectionString = new OracleConnectionStringBuilder(configured) { ConnectionTimeout = 10, Pooling = false }.ConnectionString;
        var periodCode = "RH" + Guid.NewGuid().ToString("N")[..18];
        var shiftCode = "RH_" + Guid.NewGuid().ToString("N")[..18];
        var eventId = "RH_" + Guid.NewGuid().ToString("N");
        var workDate = new DateTime(1900, 1, 2);
        long companyId = 0;
        long periodId = 0;
        long runId = 0;
        long rawId = 0;
        long workShiftDayId = 0;
        long restShiftDayId = 0;
        string? employeeCode = null;
        var timeSettingsCreated = false;
        WebApplication? app = null;

        try
        {
            await using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                await SetTemplateSchemaAsync(connection);
                using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection).Options);
                var employee = await (from e in db.Employees.AsNoTracking()
                    join branch in db.SysBranches.AsNoTracking() on e.BranchId equals (long?)branch.Id
                    where e.IsActive && e.EmploymentStatus == "ACTIVE" && branch.IsActive && branch.CompanyId.HasValue
                    select new { e.EmployeeCode, e.BranchId, CompanyId = branch.CompanyId!.Value }).FirstOrDefaultAsync()
                    ?? throw new InvalidOperationException("Raw HTTP verification requires an active employee and company branch.");
                employeeCode = employee.EmployeeCode;
                companyId = employee.CompanyId;
                Assert.False(await db.AttendanceDays.AsNoTracking().AnyAsync(d => d.EmployeeCode == employee.EmployeeCode && d.AttendanceDate == workDate),
                    "Raw HTTP verification requires an unused historical attendance date.");
                var version = (await db.PayrollRuns.IgnoreQueryFilters().AsNoTracking()
                    .Where(r => r.CorrectionRootRunId == null).Select(r => (int?)r.CorrectionVersion).MaxAsync() ?? 0) + 1;
                var period = new PayrollPeriod
                {
                    CompanyId = companyId, PeriodCode = periodCode, NameAr = "فحص HTTP للبصمة", NameEn = "Raw HTTP lock check",
                    FiscalYear = workDate.Year, Month = workDate.Month, StartDate = workDate,
                    EndDate = workDate.AddDays(29), PayDate = workDate.AddDays(29), PayrollType = "SPECIAL", Status = "OPEN",
                    CreationUser = marker, CreationDate = DateTime.UtcNow
                };
                var run = new PayrollRun
                {
                    PayPeriod = periodCode, RunDate = period.EndDate, BranchId = employee.BranchId,
                    Status = "APPROVED", CorrectionVersion = version, ApprovedBy = marker, ApprovalDate = DateTime.UtcNow,
                    CreationUser = marker, CreationDate = DateTime.UtcNow
                };
                run.Lines.Add(new PayrollRunLine
                {
                    EmployeeCode = employee.EmployeeCode, BranchId = employee.BranchId, CurrencyCode = "JOD",
                    PaymentMethod = "BANK_TRANSFER", Status = "APPROVED", CreationUser = marker, CreationDate = DateTime.UtcNow
                });
                var shift = new ShiftSchedule
                {
                    ShiftCode = shiftCode, NameLocal = "فحص HTTP", NameEn = "Raw HTTP verification",
                    StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(17, 0, 0), BreakMinutes = 0,
                    WorkingDaysJson = "[0,1,2,3,4,5,6]", IsActive = true, CreationUser = marker, CreationDate = DateTime.UtcNow
                };
                var workDay = new EmployeeShiftDay
                {
                    EmployeeCode = employee.EmployeeCode, WorkDate = workDate, ShiftCode = shiftCode,
                    IsRestDay = false, IsActive = true, Source = "MANUAL", CreationUser = marker, CreationDate = DateTime.UtcNow
                };
                var previousRest = new EmployeeShiftDay
                {
                    EmployeeCode = employee.EmployeeCode, WorkDate = workDate.AddDays(-1), ShiftCode = null,
                    IsRestDay = true, IsActive = true, Source = "MANUAL", CreationUser = marker, CreationDate = DateTime.UtcNow
                };
                var settings = await db.Set<AttendanceTimeSettings>().AsNoTracking()
                    .SingleOrDefaultAsync(s => s.CompanyId == companyId);
                var zoneId = settings?.TimeZoneId ?? "UTC";
                if (settings == null)
                {
                    db.Set<AttendanceTimeSettings>().Add(new AttendanceTimeSettings
                    {
                        CompanyId = companyId, TimeZoneId = zoneId, CreationUser = marker, CreationDate = DateTime.UtcNow
                    });
                    timeSettingsCreated = true;
                }
                var localPunch = workDate.Add(shift.StartTime).AddMinutes(15);
                var punchUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localPunch, DateTimeKind.Unspecified), ShiftTimeRules.Zone(zoneId));
                var raw = new RawAttendance
                {
                    EmployeeCode = employee.EmployeeCode, PunchTime = punchUtc, PunchType = "IN", DeviceId = "RAW_HTTP_VERIFY",
                    EventId = eventId, EventPayloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(eventId))),
                    Source = "BIOMETRIC", IsProcessed = false, CreationUser = marker, CreationDate = DateTime.UtcNow
                };
                db.PayrollPeriods.Add(period);
                db.PayrollRuns.Add(run);
                db.ShiftSchedules.Add(shift);
                db.Set<EmployeeShiftDay>().AddRange(workDay, previousRest);
                db.RawAttendances.Add(raw);
                await using var seed = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                await db.SaveChangesAsync();
                periodId = period.Id; runId = run.Id; rawId = raw.Id;
                workShiftDayId = workDay.Id; restShiftDayId = previousRest.Id;
                await seed.CommitAsync();
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Configuration["ConnectionStrings:OracleDb"] = connectionString;
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<ITenantSchemaResolver, VerificationTenantResolver>();
            builder.Services.AddSingleton<ITenantSchemaSession, OracleTenantSchemaSession>();
            builder.Services.AddAuthentication(AuthScheme).AddScheme<AuthenticationSchemeOptions, VerificationAuthenticationHandler>(AuthScheme, _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddScoped<EmployeeFieldAccess>();
            builder.Services.AddDbContext<OracleDbContext>(options => options.UseOracle(connectionString));
            builder.Services.AddScoped<IAttendanceCorrectionRepository, AttendanceCorrectionRepository>();
            builder.Services.AddScoped<IAttendanceTimeSettingsRepository, AttendanceTimeSettingsRepository>();
            builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            builder.Services.AddScoped<IShiftResolutionRepository, ShiftResolutionRepository>();
            builder.Services.AddScoped<IEffectiveShiftService, EffectiveShiftService>();
            builder.Services.AddScoped<IShiftTimingService, ShiftTimingService>();
            builder.Services.AddScoped<IAttendanceDayContextRepository, AttendanceDayContextRepository>();
            builder.Services.AddScoped<IAttendanceDayClassifier, AttendanceDayClassifier>();
            builder.Services.AddScoped<IRawAttendanceProcessingService, RawAttendanceProcessingService>();
            builder.Services.AddScoped<IAttendanceCalculationEngine, AttendanceCalculationEngine>();
            builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
            builder.Services.AddControllers(options => options.Filters.Add<HrAccessFilter>())
                .AddApplicationPart(typeof(RawAttendanceController).Assembly);
            app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseMiddleware<SchemaRoutingMiddleware>(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using (var client = new HttpClient { BaseAddress = new Uri(address) })
            using (var request = new HttpRequestMessage(HttpMethod.Post, $"api/hr/attendance/raw-events/{rawId}/process"))
            {
                request.Headers.Add("X-Company-Id", companyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                using var response = await client.SendAsync(request);
                var detail = await response.Content.ReadAsStringAsync();
                Assert.False(response.IsSuccessStatusCode, $"Raw processing HTTP unexpectedly succeeded: {detail}");
            }
            await app.StopAsync();
            app = null;

            await using (var verifyConnection = new OracleConnection(connectionString))
            {
                await verifyConnection.OpenAsync();
                await SetTemplateSchemaAsync(verifyConnection);
                using var verify = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(verifyConnection).Options);
                var raw = await verify.RawAttendances.AsNoTracking().SingleAsync(r => r.Id == rawId);
                Assert.False(raw.IsProcessed);
                Assert.Null(raw.AttendanceDayId);
                Assert.Equal(workDate, raw.ResolvedWorkDate?.Date);
                Assert.Equal("HR_HTTP_VERIFY", raw.LastAttemptedBy);
                Assert.Contains("Payroll", raw.ProcessingReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                Assert.False(await verify.AttendanceDays.AsNoTracking().AnyAsync(d => d.EmployeeCode == raw.EmployeeCode && d.AttendanceDate == workDate));
            }
        }
        finally
        {
            if (app != null) { try { await app.StopAsync(); } catch { } await app.DisposeAsync(); }
            if (periodId > 0 || rawId > 0 || timeSettingsCreated)
            {
                await using var cleanupConnection = new OracleConnection(connectionString);
                await cleanupConnection.OpenAsync();
                await SetTemplateSchemaAsync(cleanupConnection);
                using var cleanupDb = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(cleanupConnection).Options);
                await using var cleanup = await cleanupDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                if (rawId > 0)
                {
                    await using var rawCleanup = cleanupConnection.CreateCommand();
                    rawCleanup.Transaction = (OracleTransaction)cleanup.GetDbTransaction();
                    rawCleanup.CommandText = "UPDATE HR_RAW_ATTENDANCE SET IS_PROCESSED=0,PROCESSING_OUTCOME=NULL,PROCESSING_REASON=NULL,PROCESSED_BY=NULL,PROCESSED_DATE=NULL,ATTENDANCE_DAY_ID=NULL,RESOLVED_WORK_DATE=NULL WHERE ID=:p_id AND CREATION_USER=:p_marker";
                    rawCleanup.Parameters.Add("p_id", OracleDbType.Int64, rawId, System.Data.ParameterDirection.Input);
                    rawCleanup.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
                    _ = await rawCleanup.ExecuteNonQueryAsync();
                    rawCleanup.CommandText = "DELETE FROM HR_RAW_ATTENDANCE WHERE ID=:p_id AND CREATION_USER=:p_marker";
                    if (await rawCleanup.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Temporary raw HTTP event was not removed exactly once.");
                    await using var appliedDayCleanup = cleanupConnection.CreateCommand();
                    appliedDayCleanup.Transaction = (OracleTransaction)cleanup.GetDbTransaction();
                    appliedDayCleanup.CommandText = "DELETE FROM HR_ATTENDANCE_DAY WHERE EMPLOYEE_CODE=:p_employee AND ATTENDANCE_DATE=:p_date AND CREATION_USER='HR_HTTP_VERIFY'";
                    appliedDayCleanup.Parameters.Add("p_employee", OracleDbType.NVarchar2, employeeCode, System.Data.ParameterDirection.Input);
                    appliedDayCleanup.Parameters.Add("p_date", OracleDbType.TimeStamp, workDate, System.Data.ParameterDirection.Input);
                    _ = await appliedDayCleanup.ExecuteNonQueryAsync();
                }
                if (workShiftDayId > 0)
                {
                    await using var shiftDayCleanup = cleanupConnection.CreateCommand();
                    shiftDayCleanup.Transaction = (OracleTransaction)cleanup.GetDbTransaction();
                    shiftDayCleanup.CommandText = "DELETE FROM HR_EMPLOYEE_SHIFT_DAY WHERE ID IN (:p_work,:p_rest) AND CREATION_USER=:p_marker";
                    shiftDayCleanup.Parameters.Add("p_work", OracleDbType.Int64, workShiftDayId, System.Data.ParameterDirection.Input);
                    shiftDayCleanup.Parameters.Add("p_rest", OracleDbType.Int64, restShiftDayId, System.Data.ParameterDirection.Input);
                    shiftDayCleanup.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
                    if (await shiftDayCleanup.ExecuteNonQueryAsync() != 2) throw new InvalidOperationException("Temporary raw HTTP shift-day fixtures were not removed exactly twice.");
                    await using var shiftCleanup = cleanupConnection.CreateCommand();
                    shiftCleanup.Transaction = (OracleTransaction)cleanup.GetDbTransaction();
                    shiftCleanup.CommandText = "DELETE FROM HR_SHIFT_SCHEDULE WHERE SHIFT_CODE=:p_code AND CREATION_USER=:p_marker";
                    shiftCleanup.Parameters.Add("p_code", OracleDbType.NVarchar2, shiftCode, System.Data.ParameterDirection.Input);
                    shiftCleanup.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
                    if (await shiftCleanup.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Temporary raw HTTP shift was not removed exactly once.");
                }
                if (runId > 0)
                {
                    await using var runLines = cleanupConnection.CreateCommand();
                    runLines.Transaction = (OracleTransaction)cleanup.GetDbTransaction();
                    runLines.CommandText = "DELETE FROM HR_PAYROLL_RUN_LINE WHERE PAYROLL_RUN_ID=:p_run";
                    runLines.Parameters.Add("p_run", OracleDbType.Int64, runId, System.Data.ParameterDirection.Input);
                    _ = await runLines.ExecuteNonQueryAsync();
                    runLines.CommandText = "DELETE FROM HR_PAYROLL_RUN WHERE ID=:p_run AND CREATION_USER=:p_marker";
                    runLines.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
                    if (await runLines.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Temporary raw HTTP payroll run was not removed exactly once.");
                }
                if (periodId > 0)
                {
                    await using var periodCleanup = cleanupConnection.CreateCommand();
                    periodCleanup.Transaction = (OracleTransaction)cleanup.GetDbTransaction();
                    periodCleanup.CommandText = "DELETE FROM HR_PAYROLL_PERIOD WHERE ID=:p_id AND CREATION_USER=:p_marker";
                    periodCleanup.Parameters.Add("p_id", OracleDbType.Int64, periodId, System.Data.ParameterDirection.Input);
                    periodCleanup.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
                    if (await periodCleanup.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Temporary raw HTTP payroll period was not removed exactly once.");
                }
                await cleanup.CommitAsync();
                if (timeSettingsCreated)
                {
                    await using var triggerCommand = cleanupConnection.CreateCommand();
                    triggerCommand.CommandText = "ALTER TRIGGER DEV_TEMPLATE.TRG_HR_TIME_IMMUTABLE DISABLE";
                    await triggerCommand.ExecuteNonQueryAsync();
                    try
                    {
                        triggerCommand.CommandText = "DELETE FROM HR_ATTENDANCE_TIME_SETTINGS WHERE COMPANY_ID=:p_company AND CREATION_USER=:p_marker";
                        triggerCommand.Parameters.Add("p_company", OracleDbType.Int64, companyId, System.Data.ParameterDirection.Input);
                        triggerCommand.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
                        if (await triggerCommand.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Temporary raw HTTP time setting was not removed exactly once.");
                    }
                    finally
                    {
                        triggerCommand.Parameters.Clear();
                        triggerCommand.CommandText = "ALTER TRIGGER DEV_TEMPLATE.TRG_HR_TIME_IMMUTABLE ENABLE";
                        await triggerCommand.ExecuteNonQueryAsync();
                    }
                }
                Assert.False(await cleanupDb.PayrollPeriods.IgnoreQueryFilters().AnyAsync(p => p.Id == periodId && p.CreationUser == marker));
                Assert.False(await cleanupDb.PayrollRuns.IgnoreQueryFilters().AnyAsync(r => r.Id == runId && r.CreationUser == marker));
                Assert.False(await cleanupDb.RawAttendances.IgnoreQueryFilters().AnyAsync(r => r.Id == rawId && r.CreationUser == marker));
                Assert.False(await cleanupDb.Set<EmployeeShiftDay>().IgnoreQueryFilters().AnyAsync(d => (d.Id == workShiftDayId || d.Id == restShiftDayId) && d.CreationUser == marker));
                Assert.False(await cleanupDb.ShiftSchedules.IgnoreQueryFilters().AnyAsync(s => s.ShiftCode == shiftCode && s.CreationUser == marker));
                if (timeSettingsCreated)
                {
                    Assert.False(await cleanupDb.Set<AttendanceTimeSettings>().IgnoreQueryFilters().AnyAsync(s => s.CompanyId == companyId && s.CreationUser == marker));
                }
                await using var triggerCheck = cleanupConnection.CreateCommand();
                triggerCheck.CommandText = "SELECT STATUS FROM ALL_TRIGGERS WHERE OWNER='DEV_TEMPLATE' AND TRIGGER_NAME='TRG_HR_TIME_IMMUTABLE'";
                Assert.Equal("ENABLED", Convert.ToString(await triggerCheck.ExecuteScalarAsync()));
            }
        }

        output.WriteLine("Oracle Kestrel raw-attendance process route rejected by approved payroll; real resolver/shift repository used and all fixtures removed.");
    }

    private static async Task AssertPolicyWriteRejectedAsync(HttpClient client, long companyId, string path, object? body, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path);
        request.Headers.Add("X-Company-Id", companyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (body != null) request.Content = System.Net.Http.Json.JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        Assert.False(response.IsSuccessStatusCode, $"Policy write {path} unexpectedly succeeded: {responseText}");
        Assert.Contains("Payroll policy changes affect approved payroll", responseText, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RunHttpWorkflowAsync(OracleConnection connection, OracleTransaction transaction,
        string connectionString, long company1, long company2)
    {
        var dbOptions = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection).Options;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration["ConnectionStrings:OracleDb"] = connectionString;
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ITenantSchemaResolver, VerificationTenantResolver>();
        builder.Services.AddSingleton<ITenantSchemaSession, OracleTenantSchemaSession>();
        builder.Services.AddAuthentication(AuthScheme).AddScheme<AuthenticationSchemeOptions, VerificationAuthenticationHandler>(AuthScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<EmployeeFieldAccess>();
        builder.Services.AddScoped(sp =>
        {
            var context = new OracleDbContext(dbOptions);
            context.Database.UseTransaction(transaction);
            return context;
        });
        builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
        builder.Services.AddControllers(options => options.Filters.Add<HrAccessFilter>())
            .AddApplicationPart(typeof(PayrollPoliciesController).Assembly);

        await using var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<SchemaRoutingMiddleware>();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            await SendAsync(client, HttpMethod.Get, company1, $"api/hr/payroll-policies/currency-precision?companyId={company1}");
            await SendAsync(client, HttpMethod.Get, company2, $"api/hr/payroll-policies/currency-precision?companyId={company2}");
            await SendAsync(client, HttpMethod.Put, company1, "api/hr/payroll-policies/currency-precision",
                new PayrollCurrencyPrecisionDto { CompanyId = company1, CurrencyCode = " hrhttp ", DecimalPlaces = 0 });
            await SendAsync(client, HttpMethod.Put, company2, "api/hr/payroll-policies/currency-precision",
                new PayrollCurrencyPrecisionDto { CompanyId = company2, CurrencyCode = CurrencyCode, DecimalPlaces = 4 });

            Assert.Equal(0, await ReadSinglePrecisionAsync(client, company1));
            Assert.Equal(4, await ReadSinglePrecisionAsync(client, company2));

            await SendAsync(client, HttpMethod.Delete, company1, $"api/hr/payroll-policies/currency-precision/{company1}/hrhttp");
            await SendAsync(client, HttpMethod.Delete, company2, $"api/hr/payroll-policies/currency-precision/{company2}/hrhttp");
            Assert.Equal(0, await ReadPrecisionCountAsync(client, company1));
            Assert.Equal(0, await ReadPrecisionCountAsync(client, company2));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task<int> ReadSinglePrecisionAsync(HttpClient client, long company)
    {
        using var response = await SendAsync(client, HttpMethod.Get, company,
            $"api/hr/payroll-policies/currency-precision?companyId={company}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = json.RootElement.GetProperty("data");
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal(CurrencyCode, rows[0].GetProperty("currencyCode").GetString());
        return rows[0].GetProperty("decimalPlaces").GetInt32();
    }

    private static async Task<int> ReadPrecisionCountAsync(HttpClient client, long company)
    {
        using var response = await SendAsync(client, HttpMethod.Get, company,
            $"api/hr/payroll-policies/currency-precision?companyId={company}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetArrayLength();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, long company, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Company-Id", company.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (body != null) request.Content = System.Net.Http.Json.JsonContent.Create(body);
        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            var status = response.StatusCode;
            response.Dispose();
            throw new InvalidOperationException($"HTTP {method} {path} returned {(int)status}: {detail}");
        }
        return response;
    }

    private static async Task EnsureUnusedAsync(OracleConnection connection, long company1, long company2)
    {
        if (await CountRowsAsync(connection, company1, company2) != 0)
            throw new InvalidOperationException("Reserved payroll precision HTTP verification keys are already in use.");
    }

    private static async Task SetTemplateSchemaAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA = DEV_TEMPLATE";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountRowsAsync(OracleConnection connection, long company1, long company2)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "SELECT COUNT(*) FROM DEV_TEMPLATE.HR_PAYROLL_CURRENCY_PRECISION WHERE COMPANY_ID IN (:company1,:company2) AND CURRENCY_CODE=:code";
        command.Parameters.Add("company1", OracleDbType.Int64).Value = company1;
        command.Parameters.Add("company2", OracleDbType.Int64).Value = company2;
        command.Parameters.Add("code", OracleDbType.Varchar2, 10).Value = CurrencyCode;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed class VerificationTenantResolver : ITenantSchemaResolver
    {
        public Task<TenantRequestContext?> ResolveAsync(long? companyId, string? companyCode, string? expectedSchema, CancellationToken cancellationToken)
        {
            if (companyId is not > 0 || expectedSchema != null && !string.Equals(expectedSchema, "DEV_TEMPLATE", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<TenantRequestContext?>(null);
            return Task.FromResult<TenantRequestContext?>(new TenantRequestContext(companyId.Value, "HR_VERIFY", "DEV_TEMPLATE"));
        }
    }

    private sealed class VerificationAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public VerificationAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(new[] { new Claim("isSuperAdmin", "true"), new Claim(ClaimTypes.Name, "HR_HTTP_VERIFY") }, AuthScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), AuthScheme)));
        }
    }

    private sealed class CurrentSchemaCaptureInterceptor(ConcurrentQueue<string> observedSchemas) : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.Connection?.State == System.Data.ConnectionState.Open)
            {
                await using var probe = command.Connection.CreateCommand();
                probe.Transaction = command.Transaction;
                probe.CommandText = "SELECT SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL";
                var schema = Convert.ToString(await probe.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(schema)) observedSchemas.Enqueue(schema);
            }

            return result;
        }
    }
}
