using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.API.Middleware;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Repositories.Accounting;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

// Real loopback HTTP/JWT/MVC/filter/service/middleware over independent InMemory stores.
// Registry resolution and Oracle session switching are adapters, not Oracle schema-routing proof.
public sealed partial class HrAttendanceHttpTests : IAsyncLifetime
{
    private readonly string store = Guid.NewGuid().ToString("N");
    private readonly byte[] key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    private readonly SemaphoreSlim[] captureLocks = { new(1), new(1) };
    private WebApplication app = null!;
    private HttpClient client = null!;
    private ILeaveService? leaveServiceOverride;
    private IHrAccountingHandoffService? accountingHandoffServiceOverride;
    private Func<IServiceProvider, IHrAccountingHandoffService>? accountingHandoffServiceFactory;
    private Func<IServiceProvider, IAttendanceTimeSettingsRepository>? attendanceTimeSettingsRepositoryFactory;
    private Func<IServiceProvider, IPayrollService>? payrollServiceFactory;
    private IPayrollService? payrollServiceOverride;
    private bool useActualAttendancePipeline;
    private DbContextOptions<OracleDbContext> Options(int company) => new DbContextOptionsBuilder<OracleDbContext>()
        .UseInMemoryDatabase(store + company)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options;

    public async Task InitializeAsync()
    {
        for (var company = 1; company <= 2; company++)
        {
            using var db = new OracleDbContext(Options(company));
            db.AddRange(new SysUser { Id = 12, CompanyId = company, IsActive = true },
                new BranchAttendanceLocation { BranchId = 7, CompanyId = company, Latitude = 0m, Longitude = 0m, RadiusMeters = 100m, EnforceGeofence = true, CreationUser = "FIXTURE", CreationDate = DateTime.UtcNow },
                new SysBranch { Id = 7, CompanyId = company, IsActive = true, Latitude = 0m, Longitude = 0m, EnforceGeofence = true, BranchNameEn = "Tenant" + company },
                new SysBranch { Id = 8, CompanyId = company, IsActive = true },
                new SysUserBranch { Id = 1, UserId = 12, BranchId = 7 },
                new Employee { EmployeeCode = "SELF", BranchId = 7, UserId = 12 }, new Employee { EmployeeCode = "OTHER", BranchId = 8 },
                new SysScreen { Id = 53, SystemId = 50, ScreenCode = "hr-leaves", IsActive = true },
                new SysScreenFeature { ScreenId = 53, FeatureId = 61 }, new SysScreenFeature { ScreenId = 53, FeatureId = 62 },
                new SysScreenFeature { ScreenId = 53, FeatureId = 63 }, new SysScreenFeature { ScreenId = 53, FeatureId = 71 },
                new SysScreenFeature { ScreenId = 53, FeatureId = 72 }, new SysScreenFeature { ScreenId = 53, FeatureId = 73 },
                new SysScreen { Id = 80, SystemId = 50, ScreenCode = "accounting-handoff", IsActive = true },
                new SysScreen { Id = 90, SystemId = 50, ScreenCode = "hr-end-of-service", IsActive = true },
                new SysScreenFeature { ScreenId = 90, FeatureId = 61 },
                new SysScreenFeature { ScreenId = 80, FeatureId = 62 },
                new SysScreenFeature { ScreenId = 80, FeatureId = 81 },
                new SysScreenFeature { ScreenId = 80, FeatureId = 82 },
                new SysScreenFeature { ScreenId = 80, FeatureId = 65 },
                new SysUserScreenPermission { Id = 93, UserId = 12, ScreenId = 53, FeatureId = 61, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 91, UserId = 12, ScreenId = 53, FeatureId = 62, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 92, UserId = 12, ScreenId = 53, FeatureId = 63, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 94, UserId = 12, ScreenId = 53, FeatureId = 71, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 95, UserId = 12, ScreenId = 53, FeatureId = 72, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 96, UserId = 12, ScreenId = 53, FeatureId = 73, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 97, UserId = 12, ScreenId = 90, FeatureId = 61, BranchId = 7, IsGranted = true },
                new SysSystem { Id = 50, SystemCode = "hr", IsActive = true },
                new SysScreen { Id = 51, SystemId = 50, ScreenCode = "hr-attendance", IsActive = true },
                new SysFeature { Id = 61, FeatureCode = "create", IsActive = true }, new SysFeature { Id = 62, FeatureCode = "view", IsActive = true },
                new SysFeature { Id = 63, FeatureCode = "configure", IsActive = true }, new SysFeature { Id = 71, FeatureCode = "edit", IsActive = true },
                new SysFeature { Id = 72, FeatureCode = "approve", IsActive = true }, new SysFeature { Id = 73, FeatureCode = "reject", IsActive = true },
                new SysFeature { Id = 81, FeatureCode = "review", IsActive = true },
                new SysFeature { Id = 82, FeatureCode = "pay", IsActive = true },
                new SysFeature { Id = 65, FeatureCode = "reverse", IsActive = true },
                new SysScreenFeature { ScreenId = 51, FeatureId = 61 }, new SysScreenFeature { ScreenId = 51, FeatureId = 62 },
                new SysScreenFeature { ScreenId = 51, FeatureId = 63 }, new SysBranchSystem { Id = 70, BranchId = 7, SystemId = 50 });
            await db.SaveChangesAsync();
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ITenantSchemaResolver>(new FixtureTenantSchemaResolver());
        builder.Services.AddSingleton<ITenantSchemaSession>(new FixtureTenantSchemaSession());
        builder.Services.AddScoped<EmployeeFieldAccess>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "hr-fixture", ValidateAudience = true,
                ValidAudience = "hr-fixture", ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero, NameClaimType = "name" };
        });
        builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", p => p.RequireClaim("isAdmin", "true")));
        builder.Services.AddControllers(o => o.Filters.Add<HrAccessFilter>())
            .AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles)
            .AddApplicationPart(typeof(AttendanceController).Assembly);
        builder.Services.AddSingleton(Mock.Of<IAuditLogger>());
        builder.Services.AddScoped(sp =>
        {
            var tenant = (TenantRequestContext)sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().HttpContext!.Items[TenantRequestContext.HttpContextItemKey]!;
            return new OracleDbContext(Options((int)tenant.CompanyId));
        });
        builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        builder.Services.AddScoped<ILeaveRepository, LeaveRepository>();
        builder.Services.AddScoped<IShortLeaveRepository, ShortLeaveRepository>();
        builder.Services.AddScoped<IShortLeaveService, ShortLeaveService>();
        builder.Services.AddScoped<IEndOfServiceRepository, EndOfServiceRepository>();
        builder.Services.AddScoped<IEndOfServiceCalculationEngine, EndOfServiceCalculationEngine>();
        builder.Services.AddScoped<IEndOfServiceService, EndOfServiceService>();
        builder.Services.AddScoped(_ => Mock.Of<ILoanDeductionService>());
        builder.Services.AddScoped<ILeaveService>(sp => leaveServiceOverride ??
            new LeaveService(sp.GetRequiredService<ILeaveRepository>(), sp.GetRequiredService<IEmployeeRepository>(),
                sp.GetRequiredService<IHrUnitOfWork>()));
        builder.Services.AddScoped<IHrUnitOfWork, InMemoryHttpHrUnitOfWork>();
        builder.Services.AddScoped<IHrAccountingHandoffService>(sp => accountingHandoffServiceFactory?.Invoke(sp)
            ?? accountingHandoffServiceOverride ?? Mock.Of<IHrAccountingHandoffService>());
        // Employees endpoints dispatch through MediatR (Features/Hr/Employees).
        builder.Services.AddScoped(sp => new ThinkOnErp.Application.Features.Hr.Employees.Common.EmployeeFieldGuard(sp.GetRequiredService<EmployeeFieldAccess>()));
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ThinkOnErp.Application.Features.Hr.Employees.Queries.GetEmployees.GetEmployeesQuery>());
        builder.Services.AddSingleton<IXlsxEmployeeWorkbookReader,XlsxEmployeeWorkbookReader>();
        builder.Services.AddSingleton<IXlsxEmployeeWorkbookWriter,XlsxEmployeeWorkbookWriter>();
        builder.Services.AddScoped<IEmployeeExcelService,EmployeeExcelService>();
        builder.Services.AddScoped<IBranchRepository, BranchRepository>();
        builder.Services.AddScoped<IAttendanceBranchRepository, AttendanceBranchRepository>();
        builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        builder.Services.AddScoped<IBranchAttendanceLocationRepository, AttendanceBranchRepository>();
        builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
        builder.Services.AddScoped<IPayrollPeriodRepository, PayrollPeriodRepository>();
        builder.Services.AddScoped<IPayrollAdjustmentRepository, PayrollAdjustmentRepository>();
        builder.Services.AddScoped<IPayrollAdjustmentService>(sp => new PayrollAdjustmentService(
            sp.GetRequiredService<IPayrollAdjustmentRepository>(),
            attendanceRepository: sp.GetRequiredService<IAttendanceCorrectionRepository>(),
            employeeRepository: sp.GetRequiredService<IEmployeeRepository>(),
            payrollPeriodRepository: sp.GetRequiredService<IPayrollPeriodRepository>(),
            overtimeCalculationService: new OvertimeCalculationService(sp.GetRequiredService<IPolicyRepository>())));
        builder.Services.AddScoped<IGlVoucherRepository, GlVoucherRepository>();
        builder.Services.AddScoped<IHrPayrollPostingService, HrPayrollPostingService>();
        // Financial transactions/Oracle locks have separate native verification. This
        // loopback adapter runs the real repositories/services on isolated InMemory stores.
        builder.Services.AddScoped<IPayrollService>(sp => payrollServiceFactory?.Invoke(sp) ?? payrollServiceOverride ?? new PayrollService(sp.GetRequiredService<IPayrollPeriodRepository>(),
            Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>()));
        builder.Services.AddSingleton(Mock.Of<IPayrollExplanationService>());
        builder.Services.AddScoped<IGeoLocationService, GeoLocationService>();
        builder.Services.AddScoped<IAttendanceCorrectionRepository>(sp => useActualAttendancePipeline
            ? new AttendanceCorrectionRepository(sp.GetRequiredService<OracleDbContext>())
            : RawAdapter(sp.GetRequiredService<OracleDbContext>()));
        builder.Services.AddScoped<IAttendanceTimeSettingsRepository>(sp => attendanceTimeSettingsRepositoryFactory?.Invoke(sp)
            ?? new AttendanceTimeSettingsRepository(sp.GetRequiredService<OracleDbContext>()));
        builder.Services.AddScoped<IShiftResolutionRepository, ShiftResolutionRepository>();
        builder.Services.AddScoped<IEffectiveShiftService, EffectiveShiftService>();
        builder.Services.AddScoped<IShiftTimingService>(sp => new ShiftTimingService(
            sp.GetRequiredService<IEffectiveShiftService>(), sp.GetRequiredService<IAttendanceTimeSettingsRepository>()));
        builder.Services.AddScoped<IAttendanceDayContextRepository>(sp => new AttendanceDayContextRepository(
            sp.GetRequiredService<OracleDbContext>(), sp.GetRequiredService<IShiftTimingService>(),
            sp.GetRequiredService<IAttendanceTimeSettingsRepository>(), sp.GetRequiredService<IAttendanceCorrectionRepository>()));
        builder.Services.AddScoped<IAttendanceDayClassifier, AttendanceDayClassifier>();
        builder.Services.AddScoped<IAttendanceDayStatusService, AttendanceDayStatusService>();
        builder.Services.AddScoped<IAttendanceCorrectionService>(sp => useActualAttendancePipeline
            ? new AttendanceCorrectionService(sp.GetRequiredService<IAttendanceCorrectionRepository>(),
                sp.GetRequiredService<IAttendanceCalculationEngine>(), sp.GetRequiredService<IHrUnitOfWork>(),
                sp.GetRequiredService<IPolicyRepository>(), sp.GetRequiredService<IAttendanceTimeSettingsRepository>())
            : Mock.Of<IAttendanceCorrectionService>());
        builder.Services.AddScoped<IRawAttendanceProcessingService>(sp =>
        {
            if (useActualAttendancePipeline)
                return new RawAttendanceProcessingService(sp.GetRequiredService<IAttendanceCorrectionRepository>(),
                    sp.GetRequiredService<IShiftTimingService>(), sp.GetRequiredService<IAttendanceTimeSettingsRepository>(),
                    sp.GetRequiredService<IPolicyRepository>(), sp.GetRequiredService<IAttendanceDayClassifier>());
            var db = sp.GetRequiredService<OracleDbContext>(); var mock = new Mock<IRawAttendanceProcessingService>();
            mock.Setup(p => p.ProcessAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Synthetic closed payroll period: financial processing deferred."));
            mock.Setup(p => p.GetAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(async (long id, CancellationToken ct) =>
                RawAttendanceProcessingService.Map(await db.RawAttendances.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct)
                    ?? throw new KeyNotFoundException("Raw event not found.")));
            return mock.Object;
        });
        builder.Services.AddScoped<IAttendanceCalculationEngine>(sp => useActualAttendancePipeline
            ? new AttendanceCalculationEngine(sp.GetRequiredService<IAttendanceCorrectionRepository>(), sp.GetRequiredService<IShiftTimingService>())
            : Mock.Of<IAttendanceCalculationEngine>());
        builder.Services.AddScoped<IHrAttendanceService, HrAttendanceService>();
        app = builder.Build();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseRouting(); app.UseAuthentication();
        app.UseMiddleware<SchemaRoutingMiddleware>();
        app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    private IAttendanceCorrectionRepository RawAdapter(OracleDbContext db)
    {
        var mock = new Mock<IAttendanceCorrectionRepository>();
        mock.Setup(r => r.GetAttendanceDayAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((string employee, DateTime date, CancellationToken ct) => db.AttendanceDays.AsNoTracking()
                .SingleOrDefaultAsync(day => day.EmployeeCode == employee && day.AttendanceDate.Date == date.Date, ct));
        mock.Setup(r => r.GetCorrectionRequestsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async (string? employee, string? status, CancellationToken ct) =>
                (IReadOnlyList<AttendanceCorrectionRequest>)await db.AttendanceCorrectionRequests.AsNoTracking()
                    .Where(request => (employee == null || request.EmployeeCode == employee) && (status == null || request.Status == status))
                    .ToListAsync(ct));
        mock.Setup(r => r.GetMobileEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string employee, string id, CancellationToken ct) => db.RawAttendances.AsNoTracking().SingleOrDefaultAsync(r => r.EmployeeCode == employee && r.EventId == id, ct));
        mock.Setup(r => r.AcceptMobileEvidenceAsync(It.IsAny<RawAttendance>(), It.IsAny<Func<long,string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (RawAttendance raw, Func<long,string> receipt, CancellationToken ct) =>
            {
                var gate = captureLocks[(int)db.HrScopeCompanyId - 1]; await gate.WaitAsync(ct);
                try
                {
                    var existing = await db.RawAttendances.AsNoTracking().SingleOrDefaultAsync(r => r.EmployeeCode == raw.EmployeeCode && r.EventId == raw.EventId, ct);
                    if (existing != null)
                    {
                        if (existing.EventPayloadHash != raw.EventPayloadHash) throw new AttendanceEventConflictException();
                        return existing;
                    }
                    db.Add(raw); await db.SaveChangesAsync(ct); raw.EventResponseJson = receipt(raw.Id); await db.SaveChangesAsync(ct); return raw;
                }
                finally { gate.Release(); }
            });
        mock.Setup(r => r.RecordRawAttemptAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async (long id, string user, string? reason, CancellationToken ct) =>
            {
                var raw = await db.RawAttendances.SingleAsync(r => r.Id == id, ct);
                raw.ProcessingReason = reason; raw.LastAttemptedBy = user; raw.LastAttemptedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct);
            });
        return mock.Object;
    }

    private string Token(int company, bool adminClaim = false) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("hr-fixture", "hr-fixture",
        new[] { new Claim("userId", "12"), new Claim("companyId", company.ToString()), new Claim("companyCode", "FIXTURE" + company),
            new Claim("companySchema", "FIXTURE_" + company), new Claim("name", "MOBILE_USER"), new Claim("isAdmin", adminClaim ? "true" : "false") },
        expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)));
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null, int? company = 1, string? selector = null, string? version = null, bool adminClaim = false)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (company.HasValue) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(company.Value,adminClaim));
        if (selector != null) request.Headers.Add("X-Company-Id", selector);
        if (version != null) request.Headers.Add("If-Match", '"' + version + '"');
        if (body != null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    private static CheckInRequestDto Punch(string? id = null, decimal latitude = 0m) => new("SELF", latitude, 0m, EventId: id ?? Guid.NewGuid().ToString());

    private sealed class FixtureTenantSchemaResolver : ITenantSchemaResolver
    {
        public Task<TenantRequestContext?> ResolveAsync(long? companyId, string? companyCode, string? expectedSchema, CancellationToken cancellationToken)
        {
            if (companyId is not (1 or 2)) return Task.FromResult<TenantRequestContext?>(null);
            var resolvedCode = "FIXTURE" + companyId.Value;
            var resolvedSchema = "FIXTURE_" + companyId.Value;
            if ((!string.IsNullOrWhiteSpace(companyCode) && !string.Equals(companyCode.Trim(), resolvedCode, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(expectedSchema) && !string.Equals(expectedSchema.Trim(), resolvedSchema, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult<TenantRequestContext?>(null);
            return Task.FromResult<TenantRequestContext?>(new TenantRequestContext(companyId.Value, resolvedCode, resolvedSchema));
        }
    }

    private sealed class FixtureTenantSchemaSession : ITenantSchemaSession
    {
        public Task RunAsync(OracleDbContext dbContext, string schema, CancellationToken cancellationToken, Func<Task> next) => next();
    }

    [Fact] public async Task AnonymousCannotCapture()
    {
        using var response = await Send("POST", "/api/hr/attendance/check-in", Punch(), company: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var db = new OracleDbContext(Options(1)); Assert.Empty(await db.RawAttendances.ToListAsync());
    }
    [Fact] public async Task BoundSelfScopeRejectsOtherEmployeeBranchAndCompanyOverride()
    {
        foreach (var body in new[] { Punch() with { EmployeeCode = "OTHER" }, Punch() with { BranchId = 8 } })
        { using var response = await Send("POST", "/api/hr/attendance/check-in", body); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); }
        using var selected = await Send("POST", "/api/hr/attendance/check-in", Punch(), selector: "2");
        Assert.Equal(HttpStatusCode.Forbidden, selected.StatusCode);
        using var db = new OracleDbContext(Options(1)); Assert.Empty(await db.RawAttendances.ToListAsync());
    }
    [Fact] public async Task BindingRequiresCoordinatesAndEventIdBeforeCapture()
    {
        foreach (var body in new object[] { new { employeeCode = "SELF", eventId = Guid.NewGuid().ToString(), latitude = 0 }, new { employeeCode = "SELF", latitude = 0, longitude = 0 } })
        { using var response = await Send("POST", "/api/hr/attendance/check-in", body); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        using var db = new OracleDbContext(Options(1)); Assert.Empty(await db.RawAttendances.ToListAsync());
    }
    [Fact] public async Task DurableReceiptReplaysAndChangedContentConflictsOverHttp()
    {
        var punch = Punch(); using var first = await Send("POST", "/api/hr/attendance/check-in", punch);
        Assert.True(first.StatusCode == HttpStatusCode.Accepted, await first.Content.ReadAsStringAsync()); var receipt = await Data(first);
        Assert.True(receipt.GetProperty("evidenceOnly").GetBoolean());
        using var retry = await Send("POST", "/api/hr/attendance/check-in", punch);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode); Assert.Equal(receipt.GetRawText(), (await Data(retry)).GetRawText());
        using var conflict = await Send("POST", "/api/hr/attendance/check-in", punch with { Latitude = .1m });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var db = new OracleDbContext(Options(1)); var raw = Assert.Single(await db.RawAttendances.ToListAsync());
        Assert.False(raw.IsProcessed); Assert.Contains("closed payroll period", raw.ProcessingReason); Assert.Equal("MOBILE_USER", raw.LastAttemptedBy);
    }
    [Fact] public async Task IndependentStoresDoNotShareSameEmployeeAndEventId()
    {
        var punch = Punch();
        for (var company = 1; company <= 2; company++)
        {
            using var response = await Send("POST", "/api/hr/attendance/check-in", punch, company);
            Assert.True(response.StatusCode == HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
            Assert.Equal("Tenant" + company, (await Data(response)).GetProperty("branchName").GetString());
            using var db = new OracleDbContext(Options(company)); Assert.Single(await db.RawAttendances.ToListAsync());
        }
    }
    [Fact] public async Task ConcurrentHttpRequestsKeepCompanyContextAndStoresSeparated()
    {
        var punch = Punch();
        var responses = await Task.WhenAll(
            Send("POST", "/api/hr/attendance/check-in", punch, company: 1),
            Send("POST", "/api/hr/attendance/check-in", punch, company: 2));
        using var companyOne = responses[0];
        using var companyTwo = responses[1];

        Assert.True(companyOne.StatusCode == HttpStatusCode.Accepted, await companyOne.Content.ReadAsStringAsync());
        Assert.True(companyTwo.StatusCode == HttpStatusCode.Accepted, await companyTwo.Content.ReadAsStringAsync());
        Assert.Equal("Tenant1", (await Data(companyOne)).GetProperty("branchName").GetString());
        Assert.Equal("Tenant2", (await Data(companyTwo)).GetProperty("branchName").GetString());
        using var dbOne = new OracleDbContext(Options(1));
        using var dbTwo = new OracleDbContext(Options(2));
        Assert.Equal("SELF", Assert.Single(await dbOne.RawAttendances.ToListAsync()).EmployeeCode);
        Assert.Equal("SELF", Assert.Single(await dbTwo.RawAttendances.ToListAsync()).EmployeeCode);
    }
    [Fact] public async Task ManagerTeamHttpReadUsesOnlyTheSelectedTenantStore()
    {
        for (var company = 1; company <= 2; company++)
        {
            using var db = new OracleDbContext(Options(company));
            db.AddRange(
                new Employee { EmployeeCode = "REPORT" + company, NameLocal = "Report", NameEn = "Report", BranchId = 7, ManagerEmployeeCode = "SELF" },
                new SysScreen { Id = 997, SystemId = 50, ScreenCode = "hr-employees", IsActive = true },
                new SysScreenFeature { ScreenId = 997, FeatureId = 62 },
                new SysUserScreenPermission { Id = 997, UserId = 12, ScreenId = 997, FeatureId = 62, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }
        foreach (var company in new[] { 1, 2 })
        {
            using var response = await Send("GET", "/api/hr/employees/me/team", company: company);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var data = await Data(response);
            Assert.Equal(1, data.GetProperty("totalCount").GetInt32());
            Assert.Equal("REPORT" + company, data.GetProperty("items")[0].GetProperty("employeeCode").GetString());
        }

        using (var dbTwo = new OracleDbContext(Options(2)))
        {
            dbTwo.Remove(await dbTwo.SysUserScreenPermissions.SingleAsync(p => p.ScreenId == 997 && p.FeatureId == 62));
            await dbTwo.SaveChangesAsync();
        }
        using (var denied = await Send("GET", "/api/hr/employees/me/team", company: 2))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var stillAuthorized = await Send("GET", "/api/hr/employees/me/team", company: 1))
            Assert.Equal(HttpStatusCode.OK, stillAuthorized.StatusCode);
    }
    [Fact]
    public async Task ShortLeaveReadsWithoutCompanyQueryUseResolvedTenant()
    {
        using var policyResponse = await Send("GET", "/api/hr/short-leaves/policy", company: 2);
        Assert.Equal(HttpStatusCode.OK, policyResponse.StatusCode);
        var policy = await policyResponse.Content.ReadFromJsonAsync<ShortLeavePolicyDto>();
        Assert.NotNull(policy);
        Assert.Equal(2, policy!.CompanyId);

        using var requestsResponse = await Send("GET", "/api/hr/short-leaves?branchId=7", company: 2);
        Assert.Equal(HttpStatusCode.OK, requestsResponse.StatusCode);
        var requests = await requestsResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(requests);
        Assert.Empty(requests!);
    }

    [Fact]
    public async Task ShortLeaveSubmitWithoutCompanyUsesTenantSpecificPolicyAndStore()
    {
        using (var db = new OracleDbContext(Options(2)))
        {
            var employee = await db.Employees.SingleAsync(e => e.EmployeeCode == "SELF");
            employee.IsActive = true;
            employee.EmploymentStatus = "ACTIVE";
            employee.BranchId = 7;
            db.ShortLeavePolicies.Add(new ShortLeavePolicy
            {
                Id = 996,
                CompanyId = 2,
                MonthlyPaidMinutesQuota = 10,
                MaxMinutesPerRequest = 60,
                ExcessAction = ShortLeaveCompensationTypes.DeductLeaveBalance,
                StandardWorkdayHours = 8m,
                IsActive = true,
                CreationUser = "HTTP_TEST"
            });
            await db.SaveChangesAsync();
        }

        using var response = await Send("POST", "/api/hr/short-leaves",
            new CreateShortLeaveDto("SELF", ShortLeaveTypes.LateArrival, new DateTime(2026, 11, 14),
                TimeSpan.FromHours(8), TimeSpan.FromHours(8.5), "Tenant-specific quota", BranchId: 7), company: 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(data.ValueKind == JsonValueKind.Object);
        Assert.Equal(2, data.GetProperty("companyId").GetInt64());
        Assert.Equal(7, data.GetProperty("branchId").GetInt64());
        Assert.Equal(ShortLeaveCompensationTypes.DeductLeaveBalance,
            data.GetProperty("compensationType").GetString());

        using var tenantTwo = new OracleDbContext(Options(2));
        var saved = await tenantTwo.ShortLeaveRequests.SingleAsync(r => r.Reason == "Tenant-specific quota");
        Assert.Equal(2, saved.CompanyId);
        Assert.Equal(ShortLeaveCompensationTypes.DeductLeaveBalance, saved.CompensationType);
        using var tenantOne = new OracleDbContext(Options(1));
        Assert.False(await tenantOne.ShortLeaveRequests.AnyAsync(r => r.Reason == "Tenant-specific quota"));
    }

    [Fact]
    public async Task LeaveDayCalculationWithoutCompanyQueryUsesResolvedTenant()
    {
        var date = new DateTime(2026, 11, 2);
        long? observedCompanyId = null;
        var calculation = new LeaveDaysCalculationDto(
            "SELF", "HTTP_UNPAID", date, date, 1, 1, 0m, true, date, date);
        var service = new Mock<ILeaveService>();
        service.Setup(s => s.CalculateLeaveDaysAsync(
                "SELF", "HTTP_UNPAID", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateTime, DateTime, long, CancellationToken>(
                (_, _, _, _, companyId, _) => observedCompanyId = companyId)
            .ReturnsAsync(calculation);
        leaveServiceOverride = service.Object;

        using var response = await Send("GET",
            "/api/hr/leaves/calculate-days?employeeCode=SELF&leaveTypeCode=HTTP_UNPAID&startDate=2026-11-02&endDate=2026-11-02",
            company: 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, observedCompanyId);
    }

    [Fact]
    public async Task EndOfServiceCalculationUsesTenantPolicyAndPersistsTenantCompany()
    {
        using (var db = new OracleDbContext(Options(2)))
        {
            var employee = await db.Employees.SingleAsync(e => e.EmployeeCode == "SELF");
            employee.HireDate = new DateTime(2020, 1, 1);
            employee.EmploymentStatus = "ACTIVE";
            employee.IsActive = true;
            employee.SalaryStructures.Add(new SalaryStructure
            {
                EmployeeCode = employee.EmployeeCode,
                EffectiveFrom = new DateTime(2020, 1, 1),
                IsActive = true,
                BasicSalary = 2_000m,
                CurrencyCode = "USD"
            });
            db.EndOfServicePolicies.Add(new EndOfServicePolicy
            {
                CompanyId = 2,
                PolicyName = "TENANT_2_EOS",
                CalculationBasis = "BASIC_SALARY",
                FirstFiveYearsRatePerYear = 0.75m,
                SubsequentYearsRatePerYear = 1.25m,
                IsActive = true,
                CreationUser = "HTTP_TEST"
            });
            await db.SaveChangesAsync();
        }

        var request = new CreateEndOfServiceRequest(
            "SELF", EndOfServiceTerminationTypes.TerminationEmployer, new DateTime(2025, 1, 1));
        using var response = await Send("POST", "/api/hr/end-of-service/calculate", request, company: 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await Data(response);
        Assert.Equal(2, data.GetProperty("companyId").GetInt64());
        Assert.Equal(2_000m, data.GetProperty("baseSalarySnapshot").GetDecimal());
        Assert.Equal(7_500m, data.GetProperty("severancePay").GetDecimal());
        using var verify = new OracleDbContext(Options(2));
        var saved = await verify.EndOfServiceSettlements.SingleAsync(s => s.EmployeeCode == "SELF");
        Assert.Equal(2, saved.CompanyId);
        Assert.Equal(7_500m, saved.SeverancePay);
    }

    [Fact] public async Task GeofenceRejectionIsRetainedAndReplayable()
    {
        var punch = Punch(latitude: .1m); using var first = await Send("POST", "/api/hr/attendance/check-in", punch);
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode); var receipt = await Data(first);
        using var retry = await Send("POST", "/api/hr/attendance/check-in", punch);
        Assert.Equal(HttpStatusCode.BadRequest, retry.StatusCode); Assert.Equal(receipt.GetRawText(), (await Data(retry)).GetRawText());
        using var db = new OracleDbContext(Options(1)); Assert.Equal("REJECTED", Assert.Single(await db.RawAttendances.ToListAsync()).ProcessingOutcome);
    }
    [Fact] public async Task DevicePunchesAreAcceptedAndProcessedThroughHttpWithActualAttendanceServices()
    {
        useActualAttendancePipeline = true;
        using (var db = new OracleDbContext(Options(1)))
        {
            var employee = await db.Employees.SingleAsync(e => e.EmployeeCode == "SELF");
            employee.HireDate = new DateTime(2020, 1, 1);
            employee.EmploymentStatus = "ACTIVE";
            employee.IsActive = true;
            employee.SalaryStructures.Add(new SalaryStructure
            {
                EmployeeCode = employee.EmployeeCode,
                EffectiveFrom = new DateTime(2020, 1, 1),
                IsActive = true,
                BasicSalary = 900m,
                CurrencyCode = "USD",
                Lines =
                [
                    new SalaryStructureLine
                    {
                        ComponentCode = "OTHER",
                        Amount = 90m,
                        IsActive = true,
                        Component = new SalaryComponent { ComponentCode = "OTHER", ComponentType = "DEDUCTION" }
                    }
                ]
            });
            var postingBranch = await db.SysBranches.SingleAsync(b => b.Id == 7);
            postingBranch.BaseCurrencyId = 990;
            db.AddRange(
                new SysUserScreenPermission { Id = 997, UserId = 12, ScreenId = 51, FeatureId = 63, BranchId = 7, IsGranted = true },
                new SysScreen { Id = 54, SystemId = 50, ScreenCode = "hr-attendance-corrections", IsActive = true },
                new SysScreenFeature { ScreenId = 54, FeatureId = 61 },
                new SysScreenFeature { ScreenId = 54, FeatureId = 72 },
                new SysUserScreenPermission { Id = 998, UserId = 12, ScreenId = 54, FeatureId = 61, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 999, UserId = 12, ScreenId = 54, FeatureId = 72, BranchId = 7, IsGranted = true },
                new SysScreen { Id = 52, SystemId = 50, ScreenCode = "hr-payroll", IsActive = true },
                new SysScreenFeature { ScreenId = 52, FeatureId = 71 },
                new SysScreenFeature { ScreenId = 52, FeatureId = 72 },
                new SysUserScreenPermission { Id = 1000, UserId = 12, ScreenId = 52, FeatureId = 71, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 1001, UserId = 12, ScreenId = 52, FeatureId = 72, BranchId = 7, IsGranted = true },
                new SysScreen { Id = 998, SystemId = 50, ScreenCode = "hr-payroll-adjustments", IsActive = true },
                new SysScreenFeature { ScreenId = 998, FeatureId = 61 },
                new SysScreenFeature { ScreenId = 998, FeatureId = 72 },
                new SysUserScreenPermission { Id = 1006, UserId = 12, ScreenId = 998, FeatureId = 61, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 1007, UserId = 12, ScreenId = 998, FeatureId = 72, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 1002, UserId = 12, ScreenId = 80, FeatureId = 81, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 1003, UserId = 12, ScreenId = 80, FeatureId = 84, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 1004, UserId = 12, ScreenId = 80, FeatureId = 82, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 1005, UserId = 12, ScreenId = 80, FeatureId = 65, BranchId = 7, IsGranted = true },
                new SysFeature { Id = 84, FeatureCode = "post", IsActive = true },
                new SysScreenFeature { ScreenId = 80, FeatureId = 84 },
                new SysCurrency { Id = 990, CurrencyNameLocal = "US Dollar", CurrencyNameEn = "US Dollar", ShortNameLocal = "USD", ShortNameEn = "USD", CreationUser = "HTTP_TEST" },
                new PayrollPostingConfiguration { BranchId = 7, CurrencyId = 990, VoucherType = 990, SalaryExpense = "HR-SAL-EXP", EmployerSscExpense = "HR-SSC-EXP", SalaryPayable = "HR-SAL-PAY", SscPayable = "HR-SSC-PAY", TaxPayable = "HR-TAX-PAY", DeductionsPayable = "HR-DED-PAY" },
                new GlVoucherType { Id = 990, TypeCode = 990, TypeKey = "HR_HTTP_JV", NameLocal = "HR test journal", NameEn = "HR test journal", Prefix = "HRJ", Category = "JOURNAL", SerialResetPolicy = "MONTHLY", IsActive = true },
                new GlVoucherType { Id = 991, TypeCode = 103, TypeKey = "HR_HTTP_PV", NameLocal = "HR test payment", NameEn = "HR test payment", Prefix = "HRP", Category = "PAYMENT", SerialResetPolicy = "MONTHLY", IsActive = true },
                new SalaryComponent { ComponentCode = "OT_ALLOWANCE", NameLocal = "Overtime allowance", NameEn = "Overtime allowance", ComponentType = "ALLOWANCE", CalculationType = "FIXED", IsActive = true },
                new OvertimeRule { Id = 993, CompanyId = 1, Code = "HTTP_REGULAR_OT", NameAr = "اختبار إضافي", NameEn = "HTTP overtime test", EffectiveFrom = new DateTime(2026, 1, 1), DayType = "REGULAR", Multiplier = 1.25m, MinimumMinutes = 15, MaximumMinutes = 240, HourlyDivisorFormula = "MONTHLY_SALARY / 240", RequiresApproval = true, IsActive = true },
                new SysFiscalYear { Id = 990, BranchId = 7, FiscalYearCode = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), IsClosed = false, IsActive = true, CreationUser = "HTTP_TEST" },
                new GlFiscalPeriod { Id = 990, FiscalYearId = 990, PeriodNumber = 11, PeriodNameLocal = "November 2026", PeriodNameEn = "November 2026", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30), Status = "OPEN", CreationUser = "HTTP_TEST" },
                new GlAccount { AccountCode = "HR-SAL-EXP", AccountNameLocal = "Salary expense", AccountNameEn = "Salary expense", AccountLevel = 1, AccountType = "DETAIL", NormalBalance = "DEBIT", IsActive = true },
                new GlAccount { AccountCode = "HR-SSC-EXP", AccountNameLocal = "Employer SSC expense", AccountNameEn = "Employer SSC expense", AccountLevel = 1, AccountType = "DETAIL", NormalBalance = "DEBIT", IsActive = true },
                new GlAccount { AccountCode = "HR-SAL-PAY", AccountNameLocal = "Salary payable", AccountNameEn = "Salary payable", AccountLevel = 1, AccountType = "DETAIL", NormalBalance = "CREDIT", IsActive = true },
                new GlAccount { AccountCode = "HR-SSC-PAY", AccountNameLocal = "SSC payable", AccountNameEn = "SSC payable", AccountLevel = 1, AccountType = "DETAIL", NormalBalance = "CREDIT", IsActive = true },
                new GlAccount { AccountCode = "HR-TAX-PAY", AccountNameLocal = "Tax payable", AccountNameEn = "Tax payable", AccountLevel = 1, AccountType = "DETAIL", NormalBalance = "CREDIT", IsActive = true },
                new GlAccount { AccountCode = "HR-DED-PAY", AccountNameLocal = "Deductions payable", AccountNameEn = "Deductions payable", AccountLevel = 1, AccountType = "DETAIL", NormalBalance = "CREDIT", IsActive = true },
                new GlAccount { AccountCode = "HR-CASH", AccountNameLocal = "Payroll cash", AccountNameEn = "Payroll cash", AccountLevel = 1, AccountType = "DETAIL", NormalBalance = "CREDIT", IsActive = true },
                new PaymentMethod { Id = 990, BranchId = 7, Code = "HTTP-CASH", NameLocal = "Test cash", NameEn = "Test cash", MethodType = "CASH", GlAccountCode = "HR-CASH", IsActive = true, ShowInVouchers = true, CreationUser = "HTTP_TEST" },
                new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC", CreationUser = "HTTP_TEST" },
                new ShortLeavePolicy { Id = 997, CompanyId = 1, MonthlyPaidMinutesQuota = 45, MaxMinutesPerRequest = 45,
                    AllowMiddayExit = true, ExcessAction = "DEDUCT_SALARY", IsActive = true, CreationUser = "HTTP_TEST" },
                new AttendancePolicy { Id = 997, CompanyId = 1, Code = "HTTP_POLICY", NameAr = "HTTP test", NameEn = "HTTP test",
                    EffectiveFrom = new DateTime(2026, 1, 1), GracePeriodMinutes = 10, EarlyLeaveToleranceMinutes = 0,
                    MinMinutesForOvertime = 15, AutoDeductLateArrival = true, IsDefault = true, IsActive = true },
                new SSCPolicy { Id = 991, CompanyId = 1, Code = "HTTP_ZERO_SSC", NameAr = "HTTP test", NameEn = "HTTP test",
                    EffectiveFrom = new DateTime(2026, 1, 1), EmployeeContribRate = 0m, EmployerContribRate = 0m,
                    HighRiskSurchargeRate = 0m, MonthlyCeilingCap = 10000m, IsActive = true },
                new TaxPolicy { Id = 992, CompanyId = 1, Code = "HTTP_ZERO_TAX", NameAr = "HTTP test", NameEn = "HTTP test",
                    EffectiveFrom = new DateTime(2026, 1, 1), PersonalExemptionSelf = 1000000m,
                    NationalContribThreshold = 1000000m, NationalContribRate = 0m, IsActive = true },
                new LeaveType { LeaveTypeCode = "HTTP_UNPAID", NameLocal = "HTTP unpaid", IsPaid = false, IsActive = true },
                new PayrollPeriod { Id = 910, CompanyId = 1, PeriodCode = "2026-11", StartDate = new DateTime(2026, 11, 1),
                    EndDate = new DateTime(2026, 11, 30), Status = "OPEN" },
                new ShiftSchedule { ShiftCode = "HTTP_DAY", NameLocal = "HTTP day", NameEn = "HTTP day", StartTime = TimeSpan.FromHours(9),
                    EndTime = TimeSpan.FromHours(17), BreakMinutes = 30, WorkingDaysJson = "[0,1,2,3,4,5,6]", IsActive = true },
                new EmployeeShiftAssignment { EmployeeCode = "SELF", ShiftCode = "HTTP_DAY", AssignmentKind = "BASE",
                    EffectiveFrom = new DateTime(2026, 11, 1), IsActive = true });
            await db.SaveChangesAsync();
        }

        async Task<(long Id, long DayId)> CaptureAndProcess(string punchType, string eventId, string occurredAt)
        {
            using var captured = await Send("POST", "/api/hr/attendance/raw-events",
                new DeviceAttendanceDto("SELF", occurredAt, punchType, "HTTP_DEVICE", eventId));
            Assert.Equal(HttpStatusCode.OK, captured.StatusCode);
            var rawId = (await Data(captured)).GetProperty("id").GetInt64();
            using var processed = await Send("POST", $"/api/hr/attendance/raw-events/{rawId}/process");
            Assert.Equal(HttpStatusCode.OK, processed.StatusCode);
            var result = await Data(processed);
            Assert.True(result.GetProperty("applied").GetBoolean());
            var dayId = result.GetProperty("attendanceDayId").GetInt64();
            using var db = new OracleDbContext(Options(1));
            var raw = await db.RawAttendances.SingleAsync(r => r.Id == rawId);
            Assert.True(raw.IsProcessed);
            Assert.Equal("APPLIED", raw.ProcessingOutcome);
            Assert.Equal(dayId, raw.AttendanceDayId);
            return (rawId, dayId);
        }

        var first = await CaptureAndProcess("IN", "HTTP-IN", "2026-11-05T09:20:00Z");
        var last = await CaptureAndProcess("OUT", "HTTP-OUT", "2026-11-05T17:00:00Z");
        Assert.Equal(first.DayId, last.DayId);
        using var finalDb = new OracleDbContext(Options(1));
        var day = await finalDb.AttendanceDays.SingleAsync(d => d.Id == first.DayId);
        Assert.Equal("HTTP_DAY", day.ShiftCode);
        Assert.Equal(7.5m, day.ScheduledHours);
        Assert.Equal(7.17m, day.ActualWorkedHours);
        Assert.Equal(10, day.LateArrivalMinutes);
        Assert.Equal(new DateTime(2026, 11, 5, 9, 20, 0, DateTimeKind.Utc), day.FirstCheckIn);
        Assert.Equal(new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc), day.LastCheckOut);
        Assert.Equal("PRESENT", day.Status);

        using var submitted = await Send("POST", "/api/hr/attendance-corrections",
            new AttendanceCorrectionRequestDto("SELF", new DateTime(2026, 11, 5),
                new DateTime(2026, 11, 5, 9, 30, 0, DateTimeKind.Utc), new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc),
                "Correct device check-in"));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var correctionId = (await Data(submitted)).GetProperty("id").GetInt64();
        using var approved = await Send("POST", $"/api/hr/attendance-corrections/{correctionId}/process",
            new ProcessAttendanceCorrectionDto(true, null));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("APPROVED", (await Data(approved)).GetProperty("status").GetString());
        using var correctedDb = new OracleDbContext(Options(1));
        var corrected = await correctedDb.AttendanceDays.SingleAsync(d => d.Id == first.DayId);
        Assert.Equal(new DateTime(2026, 11, 5, 9, 30, 0, DateTimeKind.Utc), corrected.FirstCheckIn);
        Assert.Equal(20, corrected.LateArrivalMinutes);
        Assert.Equal(7m, corrected.ActualWorkedHours);

        using (var overtimeSourceDb = new OracleDbContext(Options(1)))
        {
            var overtimeSource = await overtimeSourceDb.AttendanceDays.SingleAsync(d => d.EmployeeCode == "SELF" && d.AttendanceDate == new DateTime(2026, 11, 5));
            overtimeSource.OvertimeHours = 2m;
            await overtimeSourceDb.SaveChangesAsync();
        }
        var overtimeDto = new CreateOvertimePayrollAdjustmentDto(1, "SELF", new DateTime(2026, 11, 5), "2026-11", "OT_ALLOWANCE", 7);
        using var overtimeSubmitted = await Send("POST", "/api/hr/adjustments/overtime", overtimeDto);
        Assert.True(overtimeSubmitted.StatusCode == HttpStatusCode.OK, await overtimeSubmitted.Content.ReadAsStringAsync());
        var overtimeSubmission = await Data(overtimeSubmitted);
        var overtimeAdjustmentId = overtimeSubmission.GetProperty("id").GetInt64();
        Assert.Equal("PENDING", overtimeSubmission.GetProperty("status").GetString());
        Assert.Equal(9.38m, overtimeSubmission.GetProperty("amount").GetDecimal());
        using var overtimeReplay = await Send("POST", "/api/hr/adjustments/overtime", overtimeDto);
        Assert.Equal(HttpStatusCode.OK, overtimeReplay.StatusCode);
        Assert.Equal(overtimeAdjustmentId, (await Data(overtimeReplay)).GetProperty("id").GetInt64());
        using var overtimeApproved = await Send("POST", $"/api/hr/adjustments/{overtimeAdjustmentId}/approve",
            new PayrollAdjustmentDecisionDto("Approved after attendance review", 7));
        Assert.True(overtimeApproved.StatusCode == HttpStatusCode.OK, await overtimeApproved.Content.ReadAsStringAsync());
        var approvedOvertime = await Data(overtimeApproved);
        Assert.Equal("APPROVED", approvedOvertime.GetProperty("status").GetString());
        Assert.Equal("Approved after attendance review", approvedOvertime.GetProperty("decisionReason").GetString());
        using (var verifyOvertimeDb = new OracleDbContext(Options(1)))
        {
            var savedOvertime = await verifyOvertimeDb.PayrollAdjustments.SingleAsync(a => a.Id == overtimeAdjustmentId);
            Assert.Equal("APPROVED", savedOvertime.Status);
            Assert.Equal("ATTENDANCE_OVERTIME", savedOvertime.SourceType);
            Assert.Equal(first.DayId.ToString(System.Globalization.CultureInfo.InvariantCulture), savedOvertime.SourceId);
            Assert.Equal("MOBILE_USER", savedOvertime.UpdateUser);
        }

        using var leaveSubmitted = await Send("POST", "/api/hr/leaves/requests",
            new SubmitLeaveRequestDto("SELF", "HTTP_UNPAID", new DateTime(2026, 11, 10), new DateTime(2026, 11, 12), 3m,
                "Approved unpaid leave", null));
        Assert.Equal(HttpStatusCode.OK, leaveSubmitted.StatusCode);
        var leaveId = (await Data(leaveSubmitted)).GetProperty("id").GetInt64();
        using var leaveApproved = await Send("POST", $"/api/hr/leaves/requests/{leaveId}/process",
            new ProcessLeaveRequestDto(true, null));
        Assert.Equal(HttpStatusCode.OK, leaveApproved.StatusCode);
        var approvedLeave = await Data(leaveApproved);
        Assert.Equal("APPROVED", approvedLeave.GetProperty("status").GetString());
        Assert.Equal("MOBILE_USER", approvedLeave.GetProperty("approvedBy").GetString());

        using var crossTenantShortLeavePolicy = await Send("PUT", "/api/hr/short-leaves/policy",
            new ShortLeavePolicyDto(998, 2, 60, 30, true, ShortLeaveCompensationTypes.DeductSalary, "", 8m, true));
        Assert.Equal(HttpStatusCode.Forbidden, crossTenantShortLeavePolicy.StatusCode);
        using (var verifyShortLeavePolicyScope = new OracleDbContext(Options(1)))
        {
            var scopedPolicy = await verifyShortLeavePolicyScope.ShortLeavePolicies.SingleAsync(p => p.CompanyId == 1);
            Assert.Equal(45, scopedPolicy.MonthlyPaidMinutesQuota);
            Assert.False(await verifyShortLeavePolicyScope.ShortLeavePolicies.AnyAsync(p => p.CompanyId == 2));
        }

        using var shortLeaveSubmitted = await Send("POST", "/api/hr/short-leaves",
            new CreateShortLeaveDto("SELF", ShortLeaveTypes.EarlyDeparture, new DateTime(2026, 11, 11),
                TimeSpan.FromHours(15), TimeSpan.FromHours(15.5), "Company-configured paid short leave", BranchId: 7));
        Assert.Equal(HttpStatusCode.Created, shortLeaveSubmitted.StatusCode);
        var shortLeaveRequest = await shortLeaveSubmitted.Content.ReadFromJsonAsync<JsonElement>();
        var shortLeaveId = shortLeaveRequest.GetProperty("id").GetInt64();
        Assert.Equal("PENDING_APPROVAL", shortLeaveRequest.GetProperty("status").GetString());
        Assert.Equal("PAID", shortLeaveRequest.GetProperty("compensationType").GetString());
        using var shortLeaveApproved = await Send("POST", $"/api/hr/short-leaves/{shortLeaveId}/approve");
        Assert.Equal(HttpStatusCode.OK, shortLeaveApproved.StatusCode);
        Assert.Equal("APPROVED", (await shortLeaveApproved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        using var excessShortLeaveSubmitted = await Send("POST", "/api/hr/short-leaves",
            new CreateShortLeaveDto("SELF", ShortLeaveTypes.EarlyDeparture, new DateTime(2026, 11, 12),
                TimeSpan.FromHours(15), TimeSpan.FromHours(15.5), "Exceeds company paid quota", 1, 7));
        Assert.Equal(HttpStatusCode.Created, excessShortLeaveSubmitted.StatusCode);
        var excessShortLeave = await excessShortLeaveSubmitted.Content.ReadFromJsonAsync<JsonElement>();
        var excessShortLeaveId = excessShortLeave.GetProperty("id").GetInt64();
        Assert.Equal("DEDUCT_SALARY", excessShortLeave.GetProperty("compensationType").GetString());
        using var excessShortLeaveApproved = await Send("POST", $"/api/hr/short-leaves/{excessShortLeaveId}/approve");
        Assert.Equal(HttpStatusCode.OK, excessShortLeaveApproved.StatusCode);
        var approvedExcessShortLeave = await excessShortLeaveApproved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("APPROVED", approvedExcessShortLeave.GetProperty("status").GetString());
        Assert.Equal("DEDUCT_SALARY", approvedExcessShortLeave.GetProperty("compensationType").GetString());
        Assert.Equal(1.88m, approvedExcessShortLeave.GetProperty("deductedSalaryAmount").GetDecimal());
        using var outOfBranchShortLeave = await Send("POST", "/api/hr/short-leaves",
            new CreateShortLeaveDto("OTHER", ShortLeaveTypes.LateArrival, new DateTime(2026, 11, 12),
                TimeSpan.FromHours(9), TimeSpan.FromHours(9.25), "Out of granted branch", 1, 8));
        Assert.Equal(HttpStatusCode.Forbidden, outOfBranchShortLeave.StatusCode);
        using (var verifyShortLeaveScope = new OracleDbContext(Options(1)))
            Assert.False(await verifyShortLeaveScope.ShortLeaveRequests.AnyAsync(r => r.EmployeeCode == "OTHER"));
        using var crossTenantShortLeave = await Send("POST", "/api/hr/short-leaves",
            new CreateShortLeaveDto("SELF", ShortLeaveTypes.LateArrival, new DateTime(2026, 11, 13),
                TimeSpan.FromHours(9), TimeSpan.FromHours(9.25), "Foreign tenant", 2, 7));
        Assert.Equal(HttpStatusCode.Forbidden, crossTenantShortLeave.StatusCode);
        using (var verifyTenantScope = new OracleDbContext(Options(1)))
            Assert.False(await verifyTenantScope.ShortLeaveRequests.AnyAsync(r => r.Reason == "Foreign tenant"));

        accountingHandoffServiceFactory = sp =>
        {
            var db = sp.GetRequiredService<OracleDbContext>();
            var voucherRepository = sp.GetRequiredService<IGlVoucherRepository>();
            var tenantContext = new Mock<ICurrentTenantContext>();
            tenantContext.Setup(t => t.GetRequiredCompanyId()).Returns(1);
            tenantContext.Setup(t => t.GetLanguageCode()).Returns("en");
            var glVoucherService = new GlVoucherService(voucherRepository,
                new GlAccountRepository(db), new GlCostCenterRepository(db), new GlFiscalPeriodRepository(db),
                new GlAccountBalanceRepository(db), Mock.Of<IArSubledgerRepository>(), Mock.Of<IApSubledgerRepository>(),
                tenantContext.Object, NullLogger<GlVoucherService>.Instance);
            var paymentVoucherService = new ReceiptPaymentVoucherService(glVoucherService, voucherRepository,
                Mock.Of<ICustomerRepository>(), Mock.Of<IVendorRepository>(), Mock.Of<IArSubledgerRepository>(),
                Mock.Of<IApSubledgerRepository>(), Mock.Of<ISubledgerService>(), tenantContext.Object,
                NullLogger<ReceiptPaymentVoucherService>.Instance);
            return new HrAccountingHandoffService(db, sp.GetRequiredService<IGlVoucherRepository>(),
                new HrPayrollPostingService(db), paymentVoucherService, glVoucherService,
                sp.GetRequiredService<IHrUnitOfWork>());
        };
        payrollServiceFactory = sp =>
        {
            var db = sp.GetRequiredService<OracleDbContext>();
            var policies = sp.GetRequiredService<IPolicyRepository>();
            var engine = new PayrollCalculationEngine(policies,
                new PayrollProrationService(policies, Mock.Of<IWorkCalendarService>()), new OvertimeCalculationService(policies),
                new SSCCalculationService(policies), new TaxCalculationEngine(policies), new LoanDeductionService(new LoanRepository(db)),
                sp.GetRequiredService<IPayrollAdjustmentRepository>(), sp.GetRequiredService<ILeaveService>(),
                sp.GetRequiredService<IAttendanceCorrectionRepository>());
            var validation = new PayrollValidationService(sp.GetRequiredService<IPayrollPeriodRepository>(), policies,
                sp.GetRequiredService<IAttendanceDayContextRepository>());
            return new PayrollService(sp.GetRequiredService<IPayrollPeriodRepository>(), engine, validation,
                sp.GetRequiredService<IHrUnitOfWork>(),
                new PayrollInputSnapshotService(db), sp.GetRequiredService<IHrAccountingHandoffService>());
        };
        using var payrollCalculated = await Send("POST", "/api/hr/payroll/calculate", new CreatePayrollRunDto(1, "2026-11", 7));
        Assert.Equal(HttpStatusCode.OK, payrollCalculated.StatusCode);
        var payrollResult = await Data(payrollCalculated);
        var payrollRunId = payrollResult.GetProperty("payrollRunId").GetInt64();
        Assert.Equal(819.38m, payrollResult.GetProperty("totalGross").GetDecimal());
        Assert.Equal(729.38m, payrollResult.GetProperty("totalNet").GetDecimal());
        Assert.Equal(1, payrollResult.GetProperty("employeeCount").GetInt32());
        using (var calculatedOvertimeDb = new OracleDbContext(Options(1)))
        {
            var calculatedRun = await calculatedOvertimeDb.PayrollRuns.Include(r => r.Lines).ThenInclude(l => l.Components)
                .SingleAsync(r => r.Id == payrollRunId);
            var calculatedLine = Assert.Single(calculatedRun.Lines);
            var overtimeComponent = Assert.Single(calculatedLine.Components.Where(c => c.ComponentCode == "OT_ALLOWANCE"));
        Assert.Equal(9.38m, overtimeComponent.Amount);
            var linkedOvertime = await calculatedOvertimeDb.PayrollAdjustments.SingleAsync(a => a.Id == overtimeAdjustmentId);
            Assert.Equal("APPROVED", linkedOvertime.Status);
            Assert.Equal(calculatedLine.Id, linkedOvertime.PayrollRunLineId);
        }
        using var payrollApproved = await Send("POST", $"/api/hr/payroll/{payrollRunId}/approve");
        Assert.True(payrollApproved.StatusCode == HttpStatusCode.OK,
            $"Payroll approval failed: {payrollApproved.StatusCode} {await payrollApproved.Content.ReadAsStringAsync()}");
        Assert.Equal("APPROVED", (await Data(payrollApproved)).GetProperty("status").GetString());
        using var handoffDb = new OracleDbContext(Options(1));
        var handoff = await handoffDb.HrAccountingHandoffs.Include(h => h.Lines).Include(h => h.Transitions)
            .Include(h => h.OutboxMessages).SingleAsync(h => h.SourceType == "PAYROLL" && h.SourceId == payrollRunId.ToString());
        Assert.Equal("QUEUED", handoff.DeliveryStatus);
        Assert.NotEmpty(handoff.Lines);
        Assert.Equal(729.38m, handoff.Lines.Single(l => l.LineType == "CONTROL:NET_PAY").Amount);
        Assert.Contains(handoff.Transitions, t => t.FromStatus == "INITIAL" && t.ToStatus == "QUEUED");
        Assert.Single(handoff.OutboxMessages);
        Assert.Equal("PENDING", handoff.OutboxMessages.Single().Status);

        using var relayProvider = new ServiceCollection().AddScoped(_ => new OracleDbContext(Options(1))).BuildServiceProvider();
        var relay = new AccountingHandoffOutboxWorker(relayProvider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<AccountingHandoffOutboxWorker>>());
        await relay.RelayBatchAsync(CancellationToken.None);
        handoffDb.ChangeTracker.Clear();
        handoff = await handoffDb.HrAccountingHandoffs.Include(h => h.Lines).Include(h => h.Transitions).Include(h => h.OutboxMessages)
            .SingleAsync(h => h.Id == handoff.Id);
        Assert.Equal("DELIVERED", handoff.DeliveryStatus);
        Assert.Contains(handoff.Transitions, t => t.FromStatus == "QUEUED" && t.ToStatus == "DELIVERED" && t.Actor == "OUTBOX");
        Assert.Equal("DELIVERED", handoff.OutboxMessages.Single().Status);
        Assert.Single(await handoffDb.AccountingHandoffInboxMessages.Where(m => m.HandoffId == handoff.Id && m.Status == "RECEIVED").ToListAsync());

        using var acceptedHandoff = await Send("POST", $"/api/accounting/hr-handoffs/{handoff.Id}/accept");
        Assert.Equal(HttpStatusCode.OK, acceptedHandoff.StatusCode);
        Assert.Equal("ACCEPTED", (await Data(acceptedHandoff)).GetProperty("deliveryStatus").GetString());

        using var journalVoucherResponse = await Send("POST", $"/api/accounting/hr-handoffs/{handoff.Id}/journal-voucher");
        Assert.Equal(HttpStatusCode.OK, journalVoucherResponse.StatusCode);
        var voucherId = (await Data(journalVoucherResponse)).GetProperty("voucherId").GetInt64();
        using (var glDb = new OracleDbContext(Options(1)))
        {
            var voucherRepository = new GlVoucherRepository(glDb);
            var glVoucherService = new GlVoucherService(voucherRepository,
                new GlAccountRepository(glDb), new GlCostCenterRepository(glDb), new GlFiscalPeriodRepository(glDb),
                new GlAccountBalanceRepository(glDb), Mock.Of<IArSubledgerRepository>(), Mock.Of<IApSubledgerRepository>(),
                Mock.Of<ICurrentTenantContext>(), NullLogger<GlVoucherService>.Instance);
            await glVoucherService.PostVoucherAsync(voucherId, "ACCOUNTING_TEST");

            var postedVoucher = await glDb.GlVoucherHeaders.Include(v => v.Details).SingleAsync(v => v.Id == voucherId);
            Assert.Equal(3, postedVoucher.Status);
            Assert.NotNull(postedVoucher.PostDate);
            Assert.Equal(postedVoucher.Details.Sum(d => d.Debit), postedVoucher.Details.Sum(d => d.Credit));
            Assert.Equal(postedVoucher.Details.Count,
                await glDb.GlAccountBalances.CountAsync(b => b.FiscalYearId == 990 && b.FiscalPeriodId == 990));
        }
        using var postingReceipt = await Send("POST", $"/api/accounting/hr-handoffs/{handoff.Id}/posting-receipt",
            new AccountingHandoffController.PostingReceiptRequest(voucherId, "HR-HTTP-JV-POSTED-01"));
        Assert.Equal(HttpStatusCode.OK, postingReceipt.StatusCode);
        Assert.Equal("POSTED", (await Data(postingReceipt)).GetProperty("status").GetString());
        using (var verifyPostedHandoff = new OracleDbContext(Options(1)))
        {
            var postedHandoff = await verifyPostedHandoff.HrAccountingHandoffs.SingleAsync(h => h.Id == handoff.Id);
            Assert.Equal("POSTED", postedHandoff.DeliveryStatus);
            Assert.Single(await verifyPostedHandoff.AccountingHandoffPostingReceipts.Where(r => r.HandoffId == handoff.Id).ToListAsync());
        }

        using var preparedPayment = await Send("POST", $"/api/accounting/hr-handoffs/{handoff.Id}/payments",
            new AccountingHandoffController.PreparePaymentRequest(720m, "USD", "HTTP-CASH", "HR-PAYMENT-HTTP-01"));
        Assert.True(preparedPayment.StatusCode == HttpStatusCode.OK,
            $"Payment preparation failed: {preparedPayment.StatusCode} {await preparedPayment.Content.ReadAsStringAsync()}");
        var paymentInstruction = await Data(preparedPayment);
        var paymentId = paymentInstruction.GetProperty("id").GetInt64();
        var paymentVoucherId = paymentInstruction.GetProperty("paymentVoucherId").GetInt64();
        Assert.Equal("PREPARED", paymentInstruction.GetProperty("status").GetString());
        using (var paymentDb = new OracleDbContext(Options(1)))
        {
            var paymentVoucher = await paymentDb.GlVoucherHeaders.Include(v => v.Details).SingleAsync(v => v.Id == paymentVoucherId);
            Assert.Equal(103, paymentVoucher.VoucherType);
            Assert.Equal(1, paymentVoucher.Status);
            Assert.Equal("HR_PAYMENT", paymentVoucher.SourceSystemCode);
            Assert.Equal(paymentId, paymentVoucher.SourceRefId);
            Assert.Equal(720m, paymentVoucher.TotalAmount);
            Assert.Equal(paymentVoucher.Details.Sum(d => d.Debit), paymentVoucher.Details.Sum(d => d.Credit));

            var voucherRepository = new GlVoucherRepository(paymentDb);
            var tenantContext = new Mock<ICurrentTenantContext>();
            tenantContext.Setup(t => t.GetRequiredCompanyId()).Returns(1);
            tenantContext.Setup(t => t.GetLanguageCode()).Returns("en");
            var glVoucherService = new GlVoucherService(voucherRepository,
                new GlAccountRepository(paymentDb), new GlCostCenterRepository(paymentDb), new GlFiscalPeriodRepository(paymentDb),
                new GlAccountBalanceRepository(paymentDb), Mock.Of<IArSubledgerRepository>(), Mock.Of<IApSubledgerRepository>(),
                tenantContext.Object, NullLogger<GlVoucherService>.Instance);
            await glVoucherService.PostVoucherAsync(paymentVoucherId, "ACCOUNTING_TEST");
            Assert.Equal(3, paymentVoucher.Status);
            Assert.NotNull(paymentVoucher.PostDate);
        }

        using var confirmedPayment = await Send("POST", $"/api/accounting/hr-handoffs/payments/{paymentId}/confirm",
            new AccountingHandoffController.ConfirmPaymentRequest("BANK-REF-HTTP-01", "PAYMENT-EVIDENCE-HTTP-01"));
        Assert.Equal(HttpStatusCode.OK, confirmedPayment.StatusCode);
        var confirmedInstruction = await Data(confirmedPayment);
        Assert.Equal("CONFIRMED", confirmedInstruction.GetProperty("status").GetString());
        Assert.Equal(paymentVoucherId, confirmedInstruction.GetProperty("paymentVoucherId").GetInt64());
        Assert.Equal("BANK-REF-HTTP-01", confirmedInstruction.GetProperty("externalReference").GetString());

        var remainingPayrollPayable = handoff.Lines.Single(l => l.LineType == "CONTROL:NET_PAY").Amount - 720m;
        Assert.Equal(9.38m, remainingPayrollPayable);
        using var preparedRemainderPayment = await Send("POST", $"/api/accounting/hr-handoffs/{handoff.Id}/payments",
            new AccountingHandoffController.PreparePaymentRequest(remainingPayrollPayable, "USD", "HTTP-CASH", "HR-PAYMENT-HTTP-02"));
        Assert.Equal(HttpStatusCode.OK, preparedRemainderPayment.StatusCode);
        var remainderInstruction = await Data(preparedRemainderPayment);
        var remainderPaymentId = remainderInstruction.GetProperty("id").GetInt64();
        var remainderPaymentVoucherId = remainderInstruction.GetProperty("paymentVoucherId").GetInt64();
        Assert.Equal(2, remainderInstruction.GetProperty("paymentSequence").GetInt32());
        Assert.Equal(remainingPayrollPayable, remainderInstruction.GetProperty("amount").GetDecimal());
        using (var remainderPaymentDb = new OracleDbContext(Options(1)))
        {
            var remainderVoucherRepository = new GlVoucherRepository(remainderPaymentDb);
            var tenantContext = new Mock<ICurrentTenantContext>();
            tenantContext.Setup(t => t.GetRequiredCompanyId()).Returns(1);
            tenantContext.Setup(t => t.GetLanguageCode()).Returns("en");
            var remainderGlService = new GlVoucherService(remainderVoucherRepository,
                new GlAccountRepository(remainderPaymentDb), new GlCostCenterRepository(remainderPaymentDb), new GlFiscalPeriodRepository(remainderPaymentDb),
                new GlAccountBalanceRepository(remainderPaymentDb), Mock.Of<IArSubledgerRepository>(), Mock.Of<IApSubledgerRepository>(),
                tenantContext.Object, NullLogger<GlVoucherService>.Instance);
            await remainderGlService.PostVoucherAsync(remainderPaymentVoucherId, "ACCOUNTING_TEST");
            var remainderVoucher = await remainderPaymentDb.GlVoucherHeaders.Include(v => v.Details)
                .SingleAsync(v => v.Id == remainderPaymentVoucherId);
            Assert.Equal(3, remainderVoucher.Status);
            Assert.Equal("HR_PAYMENT", remainderVoucher.SourceSystemCode);
            Assert.Equal(remainderPaymentId, remainderVoucher.SourceRefId);
            Assert.Equal(remainingPayrollPayable, remainderVoucher.TotalAmount);
            Assert.Equal(remainderVoucher.Details.Sum(d => d.Debit), remainderVoucher.Details.Sum(d => d.Credit));
        }
        using var confirmedRemainderPayment = await Send("POST", $"/api/accounting/hr-handoffs/payments/{remainderPaymentId}/confirm",
            new AccountingHandoffController.ConfirmPaymentRequest("BANK-REF-HTTP-02", "PAYMENT-EVIDENCE-HTTP-02"));
        Assert.Equal(HttpStatusCode.OK, confirmedRemainderPayment.StatusCode);
        Assert.Equal("CONFIRMED", (await Data(confirmedRemainderPayment)).GetProperty("status").GetString());
        using (var verifyFullyPaid = new OracleDbContext(Options(1)))
        {
            var confirmed = await verifyFullyPaid.AccountingHandoffPayments.Where(p => p.HandoffId == handoff.Id).ToListAsync();
            Assert.Equal(2, confirmed.Count);
            Assert.All(confirmed, payment => Assert.Equal("CONFIRMED", payment.Status));
            Assert.Equal(handoff.Lines.Single(l => l.LineType == "CONTROL:NET_PAY").Amount, confirmed.Sum(p => p.Amount));
        }
        using (var prematureJournalReverse = await Send("POST", $"/api/accounting/hr-handoffs/{handoff.Id}/journal-voucher/reverse",
            new AccountingHandoffController.ReverseJournalVoucherRequest(DateOnly.FromDateTime(DateTime.UtcNow), "Cannot reverse while payroll payments remain confirmed")))
        {
            Assert.Equal(HttpStatusCode.BadRequest, prematureJournalReverse.StatusCode);
            using var verifyStillPosted = new OracleDbContext(Options(1));
            Assert.Equal("POSTED", (await verifyStillPosted.HrAccountingHandoffs.SingleAsync(h => h.Id == handoff.Id)).DeliveryStatus);
        }

        using var reversedPayment = await Send("POST", $"/api/accounting/hr-handoffs/payments/{paymentId}/reverse",
            new AccountingHandoffController.ReversePaymentRequest("Duplicate payroll transfer", "BANK-REV-HTTP-01", "REVERSAL-EVIDENCE-HTTP-01"));
        Assert.Equal(HttpStatusCode.OK, reversedPayment.StatusCode);
        var reversedInstruction = await Data(reversedPayment);
        Assert.Equal("REVERSED", reversedInstruction.GetProperty("status").GetString());
        var reversalVoucherId = reversedInstruction.GetProperty("reversalVoucherId").GetInt64();
        using (var reversalDb = new OracleDbContext(Options(1)))
        {
            var original = await reversalDb.GlVoucherHeaders.Include(v => v.Details).SingleAsync(v => v.Id == paymentVoucherId);
            var reversal = await reversalDb.GlVoucherHeaders.Include(v => v.Details).SingleAsync(v => v.Id == reversalVoucherId);
            Assert.True(original.IsReversed);
            Assert.Equal(3, reversal.Status);
            Assert.Equal("GL_REVERSAL", reversal.SourceSystemCode);
            Assert.Equal(original.Id, reversal.SourceRefId);
            Assert.Equal(original.TotalLocalCredit, reversal.TotalLocalDebit);
            Assert.Equal(original.TotalLocalDebit, reversal.TotalLocalCredit);
            Assert.Equal(original.Details.Count, reversal.Details.Count);
        }

        using (var prematureJournalReverse = await Send("POST", $"/api/accounting/hr-handoffs/{handoff.Id}/journal-voucher/reverse",
            new AccountingHandoffController.ReverseJournalVoucherRequest(DateOnly.FromDateTime(DateTime.UtcNow), "Cannot reverse while the remaining payroll payment is confirmed")))
            Assert.Equal(HttpStatusCode.BadRequest, prematureJournalReverse.StatusCode);
        using (var verifySecondStillConfirmed = new OracleDbContext(Options(1)))
            Assert.Equal("CONFIRMED", (await verifySecondStillConfirmed.AccountingHandoffPayments.SingleAsync(p => p.Id == remainderPaymentId)).Status);

        using var reversedRemainderPayment = await Send("POST", $"/api/accounting/hr-handoffs/payments/{remainderPaymentId}/reverse",
            new AccountingHandoffController.ReversePaymentRequest("Reverse remaining payroll transfer", "BANK-REV-HTTP-02", "REVERSAL-EVIDENCE-HTTP-02"));
        Assert.Equal(HttpStatusCode.OK, reversedRemainderPayment.StatusCode);
        var reversedRemainder = await Data(reversedRemainderPayment);
        Assert.Equal("REVERSED", reversedRemainder.GetProperty("status").GetString());
        var remainderReversalVoucherId = reversedRemainder.GetProperty("reversalVoucherId").GetInt64();
        using (var verifyAllPaymentsReversed = new OracleDbContext(Options(1)))
        {
            var payments = await verifyAllPaymentsReversed.AccountingHandoffPayments.Where(p => p.HandoffId == handoff.Id).ToListAsync();
            Assert.Equal(2, payments.Count);
            Assert.All(payments, payment => Assert.Equal("REVERSED", payment.Status));
            var originalRemainder = await verifyAllPaymentsReversed.GlVoucherHeaders.Include(v => v.Details)
                .SingleAsync(v => v.Id == remainderPaymentVoucherId);
            var reversalRemainder = await verifyAllPaymentsReversed.GlVoucherHeaders.Include(v => v.Details)
                .SingleAsync(v => v.Id == remainderReversalVoucherId);
            Assert.True(originalRemainder.IsReversed);
            Assert.Equal(3, reversalRemainder.Status);
            Assert.Equal("GL_REVERSAL", reversalRemainder.SourceSystemCode);
            Assert.Equal(originalRemainder.Id, reversalRemainder.SourceRefId);
            Assert.Equal(originalRemainder.TotalLocalCredit, reversalRemainder.TotalLocalDebit);
            Assert.Equal(originalRemainder.TotalLocalDebit, reversalRemainder.TotalLocalCredit);
        }

        using var reversedPayrollJournal = await Send("POST", $"/api/accounting/hr-handoffs/{handoff.Id}/journal-voucher/reverse",
            new AccountingHandoffController.ReverseJournalVoucherRequest(DateOnly.FromDateTime(DateTime.UtcNow), "Reverse complete payroll settlement"));
        Assert.Equal(HttpStatusCode.OK, reversedPayrollJournal.StatusCode);
        var journalReversal = await Data(reversedPayrollJournal);
        Assert.Equal("REVERSED", journalReversal.GetProperty("status").GetString());
        var payrollReversalVoucherId = journalReversal.GetProperty("reversalVoucherId").GetInt64();
        using (var finalAccountingDb = new OracleDbContext(Options(1)))
        {
            var originalJournal = await finalAccountingDb.GlVoucherHeaders.Include(v => v.Details).SingleAsync(v => v.Id == voucherId);
            var payrollReversal = await finalAccountingDb.GlVoucherHeaders.Include(v => v.Details).SingleAsync(v => v.Id == payrollReversalVoucherId);
            var finalHandoff = await finalAccountingDb.HrAccountingHandoffs.SingleAsync(h => h.Id == handoff.Id);
            Assert.True(originalJournal.IsReversed);
            Assert.Equal("REVERSED", finalHandoff.DeliveryStatus);
            Assert.Equal(3, payrollReversal.Status);
            Assert.Equal("GL_REVERSAL", payrollReversal.SourceSystemCode);
            Assert.Equal(originalJournal.Id, payrollReversal.SourceRefId);
            Assert.Equal(originalJournal.TotalLocalCredit, payrollReversal.TotalLocalDebit);
            Assert.Equal(originalJournal.TotalLocalDebit, payrollReversal.TotalLocalCredit);
        }
    }

    [Fact] public async Task EmployeeCannotUseRawConfigureEndpoint()
    {
        using var response = await Send("POST", "/api/hr/attendance/raw-events", new DeviceAttendanceDto("SELF", DateTimeOffset.UtcNow.ToString("O"), "IN", "device", "event"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] public async Task RawAttendancePendingQueueIsLimitedToGrantedBranches()
    {
        useActualAttendancePipeline = true;
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysUserScreenPermission { Id = 1201, UserId = 12, ScreenId = 51, FeatureId = 63, BranchId = 7, IsGranted = true },
                new RawAttendance { Id = 1201, EmployeeCode = "SELF", PunchTime = DateTime.UtcNow.AddMinutes(-2), PunchType = "IN", Source = "BIOMETRIC", CreationUser = "HTTP_TEST" },
                new RawAttendance { Id = 1202, EmployeeCode = "OTHER", PunchTime = DateTime.UtcNow.AddMinutes(-1), PunchType = "IN", Source = "BIOMETRIC", CreationUser = "HTTP_TEST" });
            await db.SaveChangesAsync();
        }

        using var response = await Send("GET", "/api/hr/attendance/raw-events/pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await Data(response);
        var onlyRow = Assert.Single(rows.EnumerateArray());
        Assert.Equal("SELF", onlyRow.GetProperty("employeeCode").GetString());

        using var verify = new OracleDbContext(Options(1));
        Assert.Equal(2, await verify.RawAttendances.CountAsync());
        Assert.Contains(await verify.RawAttendances.ToListAsync(), row => row.EmployeeCode == "OTHER" && !row.IsProcessed);
    }

    [Fact] public async Task RawAttendancePendingQueueRequiresConfigureGrant()
    {
        useActualAttendancePipeline = true;
        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new RawAttendance { Id = 1203, EmployeeCode = "SELF", PunchTime = DateTime.UtcNow, PunchType = "IN", Source = "BIOMETRIC", CreationUser = "HTTP_TEST" });
            await db.SaveChangesAsync();
        }

        using var response = await Send("GET", "/api/hr/attendance/raw-events/pending");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var verify = new OracleDbContext(Options(1));
        var onlyRow = await verify.RawAttendances.SingleAsync();
        Assert.Equal("SELF", onlyRow.EmployeeCode);
        Assert.False(onlyRow.IsProcessed);
    }

    [Fact] public async Task RawAttendanceBatchProcessingIsLimitedToGrantedBranches()
    {
        useActualAttendancePipeline = true;
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysUserScreenPermission { Id = 1212, UserId = 12, ScreenId = 51, FeatureId = 63, BranchId = 7, IsGranted = true },
                new RawAttendance { Id = 1212, EmployeeCode = "SELF", PunchTime = DateTime.UtcNow.AddMinutes(-2), PunchType = "IN", Source = "BIOMETRIC", CreationUser = "HTTP_TEST" },
                new RawAttendance { Id = 1213, EmployeeCode = "OTHER", PunchTime = DateTime.UtcNow.AddMinutes(-1), PunchType = "IN", Source = "BIOMETRIC", CreationUser = "HTTP_TEST" });
            await db.SaveChangesAsync();
        }

        using var response = await Send("POST", "/api/hr/attendance/raw-events/process-batch", new ProcessRawBatchDto(10));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await Data(response);
        var result = Assert.Single(results.EnumerateArray());
        Assert.Equal(1212, result.GetProperty("id").GetInt64());
        Assert.True(result.GetProperty("needsReview").GetBoolean());

        using var verify = new OracleDbContext(Options(1));
        var own = await verify.RawAttendances.SingleAsync(r => r.Id == 1212);
        var other = await verify.RawAttendances.SingleAsync(r => r.Id == 1213);
        Assert.False(own.IsProcessed);
        Assert.Equal("MOBILE_USER", own.LastAttemptedBy);
        Assert.False(other.IsProcessed);
        Assert.Null(other.LastAttemptedBy);
        Assert.Null(other.LastAttemptedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttendanceTodayStatusCannotExposeAnotherBranchEmployee(bool branchViewGrant)
    {
        if (branchViewGrant)
        {
            using var db = new OracleDbContext(Options(1));
            db.Add(new SysUserScreenPermission { Id = 1204, UserId = 12, ScreenId = 51, FeatureId = 62, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var response = await Send("GET", "/api/hr/attendance/today-status?employeeCode=OTHER");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] public async Task AttendanceTodayStatusReturnsCurrentEmployeeStateThroughRealServices()
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC", CreationUser = "HTTP_TEST" });
            await db.SaveChangesAsync();
        }

        using var response = await Send("GET", "/api/hr/attendance/today-status?employeeCode=SELF");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await Data(response);
        Assert.Equal("SELF", data.GetProperty("employeeCode").GetString());
        Assert.Equal("NOT_CHECKED_IN", data.GetProperty("status").GetString());
        Assert.False(data.GetProperty("isCurrentlyCheckedIn").GetBoolean());
    }

    [Fact] public async Task AttendanceEffectiveShiftReturnsCurrentEmployeeScheduleThroughRealServices()
    {
        var workDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new ShiftSchedule { ShiftCode = "HTTP_SCOPE_DAY", NameLocal = "HTTP shift", NameEn = "HTTP shift",
                    StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17), BreakMinutes = 30,
                    WorkingDaysJson = "[0,1,2,3,4,5,6]", IsActive = true },
                new EmployeeShiftAssignment { EmployeeCode = "SELF", ShiftCode = "HTTP_SCOPE_DAY", AssignmentKind = "BASE",
                    EffectiveFrom = new DateTime(2020, 1, 1), IsActive = true });
            await db.SaveChangesAsync();
        }

        using var response = await Send("GET", $"/api/hr/attendance/employees/SELF/effective-shift?workDate={workDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await Data(response);
        Assert.Equal("SELF", data.GetProperty("employeeCode").GetString());
        Assert.Equal("WORKING", data.GetProperty("status").GetString());
        Assert.Equal("HTTP_SCOPE_DAY", data.GetProperty("shiftCode").GetString());
        Assert.Equal(7.5m, data.GetProperty("scheduledHours").GetDecimal());
    }

    [Fact] public async Task AttendanceShiftTimingHttpPreviewsUtcAndMatchesAnInShiftPunch()
    {
        var workDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC", CreationUser = "HTTP_TEST" },
                new ShiftSchedule { ShiftCode = "HTTP_TIMING_DAY", NameLocal = "HTTP timing", NameEn = "HTTP timing",
                    StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17), BreakMinutes = 30,
                    WorkingDaysJson = "[0,1,2,3,4,5,6]", IsActive = true },
                new EmployeeShiftAssignment { EmployeeCode = "SELF", ShiftCode = "HTTP_TIMING_DAY", AssignmentKind = "BASE",
                    EffectiveFrom = new DateTime(2020, 1, 1), IsActive = true });
            await db.SaveChangesAsync();
        }

        using var preview = await Send("GET", $"/api/hr/attendance/employees/SELF/effective-shift-utc?workDate={workDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewData = await Data(preview);
        Assert.Equal("UTC", previewData.GetProperty("timeZoneId").GetString());
        Assert.Equal(480m, previewData.GetProperty("elapsedMinutes").GetDecimal());
        Assert.Equal("HTTP_TIMING_DAY", previewData.GetProperty("shift").GetProperty("shiftCode").GetString());
        Assert.Equal(workDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
            DateTimeOffset.Parse(previewData.GetProperty("startUtc").GetString()!).UtcDateTime);

        var instant = new DateTimeOffset(workDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc)).ToString("O");
        using var match = await Send("GET", $"/api/hr/attendance/employees/SELF/match-punch-shift?instant={Uri.EscapeDataString(instant)}&punchType=IN");
        Assert.Equal(HttpStatusCode.OK, match.StatusCode);
        var matchData = await Data(match);
        Assert.Equal("HTTP_TIMING_DAY", matchData.GetProperty("shift").GetProperty("shiftCode").GetString());
        Assert.Equal("WORKING", matchData.GetProperty("shift").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AttendanceShiftTimingCannotExposeAnotherBranchEmployee(bool matchPunch, bool branchViewGrant)
    {
        if (branchViewGrant)
        {
            using var db = new OracleDbContext(Options(1));
            db.Add(new SysUserScreenPermission { Id = 1208, UserId = 12, ScreenId = 51, FeatureId = 62, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        var route = matchPunch
            ? $"match-punch-shift?instant={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}&punchType=IN"
            : $"effective-shift-utc?workDate={DateOnly.FromDateTime(DateTime.UtcNow.Date):yyyy-MM-dd}";
        using var response = await Send("GET", $"/api/hr/attendance/employees/OTHER/{route}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttendanceEffectiveShiftCannotExposeAnotherBranchEmployee(bool branchViewGrant)
    {
        if (branchViewGrant)
        {
            using var db = new OracleDbContext(Options(1));
            db.Add(new SysUserScreenPermission { Id = 1211, UserId = 12, ScreenId = 51, FeatureId = 62, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        var workDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var response = await Send("GET", $"/api/hr/attendance/employees/OTHER/effective-shift?workDate={workDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] public async Task AttendanceDayClassificationHttpPreviewsAndSavesWithAuthenticatedActor()
    {
        useActualAttendancePipeline = true;
        var workDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysUserScreenPermission { Id = 1209, UserId = 12, ScreenId = 51, FeatureId = 63, BranchId = 7, IsGranted = true },
                new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC", CreationUser = "HTTP_TEST" },
                new ShiftSchedule { ShiftCode = "HTTP_CLASSIFY_DAY", NameLocal = "HTTP classify", NameEn = "HTTP classify",
                    StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17), BreakMinutes = 30,
                    WorkingDaysJson = "[0,1,2,3,4,5,6]", IsActive = true },
                new EmployeeShiftAssignment { EmployeeCode = "SELF", ShiftCode = "HTTP_CLASSIFY_DAY", AssignmentKind = "BASE",
                    EffectiveFrom = new DateTime(2020, 1, 1), IsActive = true });
            await db.SaveChangesAsync();
        }

        var path = $"/api/hr/attendance/employees/SELF/day-classification?workDate={workDate:yyyy-MM-dd}";
        using var preview = await Send("GET", path);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewData = await Data(preview);
        Assert.Equal("ABSENT", previewData.GetProperty("day").GetProperty("status").GetString());
        Assert.True(previewData.GetProperty("day").GetProperty("scheduledHours").GetDecimal() > 0m);

        using var saved = await Send("POST", path);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedData = await Data(saved);
        Assert.Equal("ABSENT", savedData.GetProperty("day").GetProperty("status").GetString());
        using var verify = new OracleDbContext(Options(1));
        var day = await verify.AttendanceDays.SingleAsync(d => d.EmployeeCode == "SELF" && d.AttendanceDate == workDate.ToDateTime(TimeOnly.MinValue));
        Assert.Equal("MOBILE_USER", day.CreationUser);
        Assert.Equal("UTC", day.TimeZoneId);
        Assert.Equal("HTTP_CLASSIFY_DAY", day.ShiftCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AttendanceDayClassificationCannotReadOrSaveAnotherBranchEmployee(bool save, bool branchGrant)
    {
        useActualAttendancePipeline = true;
        var workDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        if (branchGrant)
        {
            using var db = new OracleDbContext(Options(1));
            db.Add(new SysUserScreenPermission
            {
                Id = 1210,
                UserId = 12,
                ScreenId = 51,
                FeatureId = save ? 63 : 62,
                BranchId = 7,
                IsGranted = true
            });
            await db.SaveChangesAsync();
        }

        var method = save ? "POST" : "GET";
        var path = $"/api/hr/attendance/employees/OTHER/day-classification?workDate={workDate:yyyy-MM-dd}";
        using var response = await Send(method, path);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var verify = new OracleDbContext(Options(1));
        Assert.Empty(await verify.AttendanceDays.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawAttendanceMutationsCannotTargetAnotherBranch(bool ignore)
    {
        useActualAttendancePipeline = true;
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysUserScreenPermission { Id = 1205, UserId = 12, ScreenId = 51, FeatureId = 63, BranchId = 7, IsGranted = true },
                new RawAttendance { Id = 1206, EmployeeCode = "OTHER", PunchTime = DateTime.UtcNow, PunchType = "IN", Source = "BIOMETRIC", CreationUser = "HTTP_TEST" });
            await db.SaveChangesAsync();
        }

        using var response = ignore
            ? await Send("POST", "/api/hr/attendance/raw-events/1206/ignore", new IgnoreRawAttendanceDto("must remain outside scope"))
            : await Send("POST", "/api/hr/attendance/raw-events/1206/process");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var verify = new OracleDbContext(Options(1));
        var raw = await verify.RawAttendances.SingleAsync();
        Assert.Equal("OTHER", raw.EmployeeCode);
        Assert.False(raw.IsProcessed);
        Assert.Null(raw.ProcessingOutcome);
        Assert.Null(raw.ProcessingReason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttendanceCorrectionCannotTargetAnotherEmployee(bool explicitBranchGrant)
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysScreen { Id = 54, SystemId = 50, ScreenCode = "hr-attendance-corrections", IsActive = true },
                new SysScreenFeature { ScreenId = 54, FeatureId = 61 });
            if (explicitBranchGrant)
                db.Add(new SysUserScreenPermission { Id = 110, UserId = 12, ScreenId = 54, FeatureId = 61, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        useActualAttendancePipeline = true;
        var attendanceDate = DateTime.UtcNow.Date.AddDays(-1);
        AttendanceCorrectionRequestDto RequestFor(string employeeCode) => new(
            employeeCode,
            attendanceDate,
            attendanceDate.AddHours(9),
            attendanceDate.AddHours(17),
            "Correct my attendance record");

        using var otherEmployee = await Send("POST", "/api/hr/attendance-corrections", RequestFor("OTHER"));
        Assert.Equal(HttpStatusCode.Forbidden, otherEmployee.StatusCode);

        using var ownEmployee = await Send("POST", "/api/hr/attendance-corrections", RequestFor("SELF"));
        Assert.Equal(HttpStatusCode.OK, ownEmployee.StatusCode);
        Assert.Equal("SELF", (await Data(ownEmployee)).GetProperty("employeeCode").GetString());

        using var verify = new OracleDbContext(Options(1));
        var saved = await verify.AttendanceCorrectionRequests.ToListAsync();
        var onlyRequest = Assert.Single(saved);
        Assert.Equal("SELF", onlyRequest.EmployeeCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttendanceCorrectionListIsFilteredToTheSelfServiceEmployeeOrGrantedBranch(bool explicitBranchGrant)
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysScreen { Id = 54, SystemId = 50, ScreenCode = "hr-attendance-corrections", IsActive = true },
                new SysScreenFeature { ScreenId = 54, FeatureId = 62 },
                new AttendanceCorrectionRequest
                {
                    Id = 9101, EmployeeCode = "SELF", AttendanceDate = DateTime.UtcNow.Date.AddDays(-2),
                    Reason = "Own request", Status = "PENDING", CreationUser = "FIXTURE", CreationDate = DateTime.UtcNow
                },
                new AttendanceCorrectionRequest
                {
                    Id = 9102, EmployeeCode = "OTHER", AttendanceDate = DateTime.UtcNow.Date.AddDays(-2),
                    Reason = "Other branch request", Status = "PENDING", CreationUser = "FIXTURE", CreationDate = DateTime.UtcNow
                });
            if (explicitBranchGrant)
                db.Add(new SysUserScreenPermission { Id = 111, UserId = 12, ScreenId = 54, FeatureId = 62, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        useActualAttendancePipeline = true;
        using var response = await Send("GET", "/api/hr/attendance-corrections");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = (await Data(response)).EnumerateArray().ToArray();
        var ownRequest = Assert.Single(requests);
        Assert.Equal("SELF", ownRequest.GetProperty("employeeCode").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BranchApproverCannotProcessAnotherBranchesAttendanceCorrection(bool approve)
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysScreen { Id = 54, SystemId = 50, ScreenCode = "hr-attendance-corrections", IsActive = true },
                new SysScreenFeature { ScreenId = 54, FeatureId = 72 },
                new SysScreenFeature { ScreenId = 54, FeatureId = 73 },
                new SysUserScreenPermission { Id = 112, UserId = 12, ScreenId = 54, FeatureId = 72, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 113, UserId = 12, ScreenId = 54, FeatureId = 73, BranchId = 7, IsGranted = true },
                new AttendanceCorrectionRequest
                {
                    Id = 9103, EmployeeCode = "OTHER", AttendanceDate = DateTime.UtcNow.Date.AddDays(-2),
                    Reason = "Request from a different branch", Status = "PENDING", CreationUser = "FIXTURE", CreationDate = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        useActualAttendancePipeline = true;
        using var response = await Send("POST", "/api/hr/attendance-corrections/9103/process",
            new ProcessAttendanceCorrectionDto(approve, approve ? null : "Reviewed outside branch"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var verify = new OracleDbContext(Options(1));
        var request = await verify.AttendanceCorrectionRequests.SingleAsync(x => x.Id == 9103);
        Assert.Equal("PENDING", request.Status);
        Assert.Null(request.ApprovedBy);
        Assert.Null(request.ApprovalDate);
        Assert.Null(request.RejectionReason);
        Assert.Null(request.UpdateUser);
        Assert.Null(request.UpdateDate);
    }

    [Fact]
    public async Task BranchApproverCanRejectOwnBranchesAttendanceCorrectionWithAudit()
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysScreen { Id = 55, SystemId = 50, ScreenCode = "hr-attendance-corrections", IsActive = true },
                new SysScreenFeature { ScreenId = 55, FeatureId = 72 },
                new SysScreenFeature { ScreenId = 55, FeatureId = 73 },
                new SysUserScreenPermission { Id = 114, UserId = 12, ScreenId = 55, FeatureId = 72, BranchId = 7, IsGranted = true },
                new SysUserScreenPermission { Id = 115, UserId = 12, ScreenId = 55, FeatureId = 73, BranchId = 7, IsGranted = true },
                new AttendanceCorrectionRequest
                {
                    Id = 9104, EmployeeCode = "SELF", AttendanceDate = DateTime.UtcNow.Date.AddDays(-2),
                    Reason = "Correction request for my branch", Status = "PENDING", CreationUser = "FIXTURE", CreationDate = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        useActualAttendancePipeline = true;
        using var response = await Send("POST", "/api/hr/attendance-corrections/9104/process",
            new ProcessAttendanceCorrectionDto(false, "Reviewed and rejected"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verify = new OracleDbContext(Options(1));
        var request = await verify.AttendanceCorrectionRequests.SingleAsync(x => x.Id == 9104);
        Assert.Equal("REJECTED", request.Status);
        Assert.Equal("Reviewed and rejected", request.RejectionReason);
        Assert.Equal("MOBILE_USER", request.ApprovedBy);
        Assert.NotNull(request.ApprovalDate);
        Assert.Equal("MOBILE_USER", request.UpdateUser);
        Assert.NotNull(request.UpdateDate);
    }

    [Fact]
    public async Task BranchApproveGrantAloneCannotRejectAttendanceCorrection()
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysScreen { Id = 56, SystemId = 50, ScreenCode = "hr-attendance-corrections", IsActive = true },
                new SysScreenFeature { ScreenId = 56, FeatureId = 72 },
                new SysScreenFeature { ScreenId = 56, FeatureId = 73 },
                new SysUserScreenPermission { Id = 116, UserId = 12, ScreenId = 56, FeatureId = 72, BranchId = 7, IsGranted = true },
                new AttendanceCorrectionRequest
                {
                    Id = 9105, EmployeeCode = "SELF", AttendanceDate = DateTime.UtcNow.Date.AddDays(-2),
                    Reason = "Correction request for my branch", Status = "PENDING", CreationUser = "FIXTURE", CreationDate = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        useActualAttendancePipeline = true;
        using var response = await Send("POST", "/api/hr/attendance-corrections/9105/process",
            new ProcessAttendanceCorrectionDto(false, "Reviewed and rejected"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var verify = new OracleDbContext(Options(1));
        var request = await verify.AttendanceCorrectionRequests.SingleAsync(x => x.Id == 9105);
        Assert.Equal("PENDING", request.Status);
        Assert.Null(request.RejectionReason);
        Assert.Null(request.ApprovedBy);
        Assert.Null(request.ApprovalDate);
        Assert.Null(request.UpdateUser);
        Assert.Null(request.UpdateDate);
    }

    [Fact] public async Task RawReadUsesBoundEmployeeAndIsolatedStore()
    {
        using var captured = await Send("POST", "/api/hr/attendance/check-in", Punch()); var id = (await Data(captured)).GetProperty("rawEventId").GetInt64();
        using var own = await Send("GET", $"/api/hr/attendance/raw-events/{id}?employeeCode=SELF"); Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var other = await Send("GET", $"/api/hr/attendance/raw-events/{id}?employeeCode=OTHER"); Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        using var tenant = await Send("GET", $"/api/hr/attendance/raw-events/{id}?employeeCode=SELF", company: 2); Assert.Equal(HttpStatusCode.NotFound, tenant.StatusCode);
    }
    [Theory]
    [InlineData(true, HttpStatusCode.Accepted)]
    [InlineData(false, HttpStatusCode.BadRequest)]
    public async Task CheckoutUsesMvcConstructorValidation(bool hasEventId, HttpStatusCode expected)
    {
        using var response = await Send("POST", "/api/hr/attendance/check-out", new CheckOutRequestDto("SELF", 0m, 0m, EventId: hasEventId ? Guid.NewGuid().ToString() : null));
        Assert.Equal(expected, response.StatusCode);
        if (hasEventId) Assert.Equal("OUT", (await Data(response)).GetProperty("punchType").GetString());
    }
    [Fact] public async Task InvalidSignatureCannotReachEmployeeCapture()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/hr/attendance/check-in") { Content = JsonContent.Create(Punch()) };
        var wrongKey = new SymmetricSecurityKey(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var forged = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("hr-fixture", "hr-fixture", new[] { new Claim("userId", "12"), new Claim("companyId", "1") },
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", forged);
        using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var db = new OracleDbContext(Options(1)); Assert.Empty(await db.RawAttendances.ToListAsync());
    }
    [Fact] public async Task EmployeeCannotReadOrChangeBranchConfiguration()
    {
        using var read = await Send("GET", "/api/hr/attendance/branches/7/location"); Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        using var write = await Send("POST", "/api/hr/attendance/branches/7/location", new WriteBranchAttendanceLocationDto(1m, 1m, 10m, false));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        using var db = new OracleDbContext(Options(1)); Assert.Equal(100m, (await db.Set<BranchAttendanceLocation>().SingleAsync()).RadiusMeters);
    }
    [Fact] public async Task AttendanceConfigurationReadsRequireConfigureGrantAndResolveTheCorrectTenant()
    {
        foreach (var company in new[] { 1, 2 })
        {
            using var db = new OracleDbContext(Options(company));
            db.Add(new SysUserScreenPermission
            {
                Id = 101,
                UserId = 12,
                ScreenId = 51,
                FeatureId = 63,
                BranchId = 7,
                IsGranted = true
            });
            db.Add(new AttendanceTimeSettings
            {
                CompanyId = company,
                TimeZoneId = company == 1 ? "Asia/Amman" : "Pacific/Auckland",
                CreationUser = "FIXTURE",
                CreationDate = DateTime.UtcNow
            });
            var location = await db.Set<BranchAttendanceLocation>().SingleAsync(x => x.BranchId == 7);
            location.RadiusMeters = company == 1 ? 100m : 250m;
            await db.SaveChangesAsync();
        }

        foreach (var company in new[] { 1, 2 })
        {
            using var timeSettings = await Send("GET", "/api/hr/attendance/time-settings", company: company);
            Assert.Equal(HttpStatusCode.OK, timeSettings.StatusCode);
            var timezone = (await Data(timeSettings)).GetProperty("timeZoneId").GetString();
            Assert.Equal(company == 1 ? "Asia/Amman" : "Pacific/Auckland", timezone);

            using var location = await Send("GET", "/api/hr/attendance/branches/7/location", company: company);
            Assert.Equal(HttpStatusCode.OK, location.StatusCode);
            var data = await Data(location);
            Assert.Equal(company, data.GetProperty("companyId").GetInt64());
            Assert.Equal(company == 1 ? 100m : 250m, data.GetProperty("radiusMeters").GetDecimal());
            Assert.True(location.Headers.ETag?.Tag.Length > 0);
        }

        using var deniedTimeSettings = await Send("GET", "/api/hr/attendance/time-settings", company: 1, selector: "2");
        Assert.Equal(HttpStatusCode.Forbidden, deniedTimeSettings.StatusCode);
        using var deniedLocation = await Send("GET", "/api/hr/attendance/branches/8/location", company: 1);
        Assert.Equal(HttpStatusCode.Forbidden, deniedLocation.StatusCode);
    }

    [Fact] public async Task AttendanceNearbyBranchesHonorsEmployeeBranchScopeAndTenantSelector()
    {
        foreach (var company in new[] { 1, 2 })
        {
            using var response = await Send("GET", "/api/hr/attendance/nearby-branches?latitude=0&longitude=0", company: company);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var branches = (await Data(response)).EnumerateArray().ToArray();
            Assert.Equal(new long[] { 7 }, branches.Select(x => x.GetProperty("branchId").GetInt64()));
        }

        using var crossCompany = await Send("GET", "/api/hr/attendance/nearby-branches?latitude=0&longitude=0&companyId=2", company: 1);
        Assert.Equal(HttpStatusCode.Forbidden, crossCompany.StatusCode);

        using var mismatchedTenantSelector = await Send("GET", "/api/hr/attendance/nearby-branches?latitude=0&longitude=0", company: 1, selector: "2");
        Assert.Equal(HttpStatusCode.Forbidden, mismatchedTenantSelector.StatusCode);
    }

    [Fact] public async Task AttendanceTimeInitializationRequiresConfigureGrantAndUsesAuthenticatedActorAndTenant()
    {
        attendanceTimeSettingsRepositoryFactory = sp =>
        {
            var db = sp.GetRequiredService<OracleDbContext>();
            var settings = new Mock<IAttendanceTimeSettingsRepository>(MockBehavior.Strict);
            settings.Setup(x => x.InitializeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string timeZoneId, string user, CancellationToken _) => Task.FromResult(new AttendanceTimeSettings
                {
                    CompanyId = db.HrScopeCompanyId,
                    TimeZoneId = timeZoneId,
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                }));
            return settings.Object;
        };

        using var denied = await Send("POST", "/api/hr/attendance/time-settings", new InitializeAttendanceTimeDto("Asia/Amman"), company: 1);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysUserScreenPermission { Id = 102, UserId = 12, ScreenId = 51, FeatureId = 63, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var allowed = await Send("POST", "/api/hr/attendance/time-settings", new InitializeAttendanceTimeDto("Asia/Amman"), company: 1);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var data = await Data(allowed);
        Assert.Equal(1, data.GetProperty("companyId").GetInt64());
        Assert.Equal("Asia/Amman", data.GetProperty("timeZoneId").GetString());
        Assert.Equal("MOBILE_USER", data.GetProperty("creationUser").GetString());

        using var deniedOtherTenant = await Send("POST", "/api/hr/attendance/time-settings", new InitializeAttendanceTimeDto("Europe/London"), company: 2);
        Assert.Equal(HttpStatusCode.Forbidden, deniedOtherTenant.StatusCode);
    }
    [Fact] public async Task AttendanceSelfServiceDoesNotGrantOrganizationCatalogAccess()
    {
        using var response = await Send("GET", "/api/hr/organization/departments");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Fact] public async Task AttendanceSelfServiceCannotConfigureOrganization()
    {
        using var response = await Send("POST", "/api/hr/organization/departments/NEW", new DepartmentWrite("Local", "English", 7, null, true));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var db = new OracleDbContext(Options(1)); Assert.Empty(await db.Set<HrDepartment>().ToListAsync());
    }
    [Fact] public async Task OrganizationDepartmentWriteUsesExplicitConfigureGrantAndRejectsOutOfScopeBranch()
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            db.AddRange(
                new SysScreen { Id = 52, SystemId = 50, ScreenCode = "hr-employees", IsActive = true },
                new SysScreenFeature { ScreenId = 52, FeatureId = 63 },
                new SysUserScreenPermission { Id = 101, UserId = 12, ScreenId = 52, FeatureId = 63, BranchId = 7, IsGranted = true });
            await db.SaveChangesAsync();
        }

        using var allowed = await Send("POST", "/api/hr/organization/departments/SCOPED", new DepartmentWrite("محاسبة", "Accounting", 7, null, true));
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        using var denied = await Send("POST", "/api/hr/organization/departments/OUTSIDE", new DepartmentWrite("خارج النطاق", "Out of scope", 8, null, true));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var verify = new OracleDbContext(Options(1));
        Assert.Equal("SCOPED", Assert.Single(await verify.Set<HrDepartment>().Select(d => d.DepartmentCode).ToListAsync()));
        using var otherCompany = new OracleDbContext(Options(2));
        Assert.Empty(await otherCompany.Set<HrDepartment>().ToListAsync());
    }
    [Fact] public async Task OrganizationHttpCreatesUpdatesRejectsStaleAndSeparatesTenantStores()
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            (await db.SysUsers.SingleAsync()).IsAdmin=true; await db.SaveChangesAsync();
        }
        var body = new DepartmentWrite("Local", "English", 7, null, true);
        using var created = await Send("POST", "/api/hr/organization/departments/NEW", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var version=(await Data(created)).GetProperty("version").GetString()!;
        Assert.Equal(version, created.Headers.ETag!.Tag.Trim('"'));
        using var updated=await Send("PUT", "/api/hr/organization/departments/NEW", body with { NameEn="Updated" }, version:version);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var stale=await Send("PUT", "/api/hr/organization/departments/NEW", body, version:version);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var missing=await Send("PUT", "/api/hr/organization/departments/NEW", body);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var incomplete=await Send("POST", "/api/hr/organization/departments/MISSING", new { NameLocal="Local", NameEn="English", BranchId=7 });
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        using var db1=new OracleDbContext(Options(1)); using var db2=new OracleDbContext(Options(2));
        Assert.Equal("Updated", (await db1.Set<HrDepartment>().SingleAsync()).NameEn);
        Assert.Empty(await db2.Set<HrDepartment>().ToListAsync());
    }
    [Fact] public async Task OrganizationPositionHttpLifecycleFiltersByDepartmentAndSeparatesTenantStores()
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            (await db.SysUsers.SingleAsync()).IsAdmin = true;
            await db.SaveChangesAsync();
        }

        using var department = await Send("POST", "/api/hr/organization/departments/ORG", new DepartmentWrite("تنظيم", "Organization", 7, null, true));
        Assert.Equal(HttpStatusCode.Created, department.StatusCode);

        var body = new PositionWrite("ORG", "محلل", "Analyst", null, 2, true);
        using var created = await Send("POST", "/api/hr/organization/positions/ANALYST", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var version = (await Data(created)).GetProperty("version").GetString()!;
        Assert.Equal(version, created.Headers.ETag!.Tag.Trim('"'));

        using var get = await Send("GET", "/api/hr/organization/positions/ANALYST");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        using var filtered = await Send("GET", "/api/hr/organization/positions?departmentCode=ORG");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        var filteredRows = await Data(filtered);
        Assert.Equal("ANALYST", filteredRows[0].GetProperty("positionCode").GetString());

        using var updated = await Send("PUT", "/api/hr/organization/positions/ANALYST", body with { TitleEn = "Senior Analyst" }, version: version);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedVersion = (await Data(updated)).GetProperty("version").GetString()!;
        using var stale = await Send("PUT", "/api/hr/organization/positions/ANALYST", body, version: version);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);

        using var db1 = new OracleDbContext(Options(1));
        using var db2 = new OracleDbContext(Options(2));
        Assert.Equal("Senior Analyst", (await db1.Set<HrPosition>().SingleAsync()).TitleEn);
        Assert.Equal(updatedVersion, (await db1.Set<HrPosition>().SingleAsync()).Version);
        Assert.Empty(await db2.Set<HrDepartment>().ToListAsync());
        Assert.Empty(await db2.Set<HrPosition>().ToListAsync());
    }
    [Fact] public async Task OrganizationDepartmentHttpRejectsCyclesAndReferencedChangesWithoutChangingTree()
    {
        using (var db = new OracleDbContext(Options(1)))
        {
            (await db.SysUsers.SingleAsync()).IsAdmin = true;
            await db.SaveChangesAsync();
        }

        using var parentCreated = await Send("POST", "/api/hr/organization/departments/CYCLE_A",
            new DepartmentWrite("أ", "A", 7, null, true));
        Assert.Equal(HttpStatusCode.Created, parentCreated.StatusCode);
        var parentVersion = (await Data(parentCreated)).GetProperty("version").GetString()!;

        using var childCreated = await Send("POST", "/api/hr/organization/departments/CYCLE_B",
            new DepartmentWrite("ب", "B", 7, "CYCLE_A", true));
        Assert.Equal(HttpStatusCode.Created, childCreated.StatusCode);

        using var otherBranch = await Send("POST", "/api/hr/organization/departments/BRANCH_8_PARENT",
            new DepartmentWrite("فرع 8", "Branch 8", 8, null, true));
        Assert.Equal(HttpStatusCode.Created, otherBranch.StatusCode);
        using var crossBranchParent = await Send("POST", "/api/hr/organization/departments/INVALID_CHILD",
            new DepartmentWrite("طفل غير صالح", "Invalid child", 7, "BRANCH_8_PARENT", true));
        Assert.Equal(HttpStatusCode.BadRequest, crossBranchParent.StatusCode);

        using var cycle = await Send("PUT", "/api/hr/organization/departments/CYCLE_A",
            new DepartmentWrite("أ", "A", 7, "CYCLE_B", true), version: parentVersion);
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
        using var moveReferenced = await Send("PUT", "/api/hr/organization/departments/CYCLE_A",
            new DepartmentWrite("أ", "A", 8, null, true), version: parentVersion);
        Assert.Equal(HttpStatusCode.BadRequest, moveReferenced.StatusCode);
        using var deactivateReferenced = await Send("PUT", "/api/hr/organization/departments/CYCLE_A",
            new DepartmentWrite("أ", "A", 7, null, false), version: parentVersion);
        Assert.Equal(HttpStatusCode.BadRequest, deactivateReferenced.StatusCode);

        using var verify = new OracleDbContext(Options(1));
        var parent = await verify.Set<HrDepartment>().SingleAsync(d => d.DepartmentCode == "CYCLE_A");
        var child = await verify.Set<HrDepartment>().SingleAsync(d => d.DepartmentCode == "CYCLE_B");
        Assert.Null(parent.ParentDepartmentCode);
        Assert.Equal(7, parent.BranchId);
        Assert.True(parent.IsActive);
        Assert.Equal("CYCLE_A", child.ParentDepartmentCode);
        Assert.False(await verify.Set<HrDepartment>().AnyAsync(d => d.DepartmentCode == "INVALID_CHILD"));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task EmployeeCannotChangeAccountLinkEvenWithAdministratorClaim(bool claim)
    {
        using var response=await Send("PUT", "/api/hr/employees/SELF/user-link", new LinkEmployeeUserDto(null), adminClaim:claim);
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        using var db=new OracleDbContext(Options(1)); Assert.Equal(12,(await db.Employees.SingleAsync(e=>e.EmployeeCode=="SELF")).UserId);
    }
    [Fact] public async Task AdministratorCanUnlinkAndRelinkWithoutChangingLegacyProfileOrAnotherTenant()
    {
        using (var db=new OracleDbContext(Options(1))) { (await db.SysUsers.SingleAsync()).IsAdmin=true; await db.SaveChangesAsync(); }
        using var unlinked=await Send("PUT", "/api/hr/employees/SELF/user-link",new LinkEmployeeUserDto(null),adminClaim:true);
        Assert.Equal(HttpStatusCode.OK,unlinked.StatusCode);
        using var linked=await Send("PUT", "/api/hr/employees/SELF/user-link",new LinkEmployeeUserDto(12),adminClaim:true);
        Assert.Equal(HttpStatusCode.OK,linked.StatusCode);
        using var db1=new OracleDbContext(Options(1)); using var db2=new OracleDbContext(Options(2));
        var employee=await db1.Employees.SingleAsync(e=>e.EmployeeCode=="SELF");
        Assert.Equal(12,employee.UserId); Assert.Equal("",employee.NameEn);
        Assert.Null((await db2.Employees.SingleAsync(e=>e.EmployeeCode=="SELF")).UpdateDate);
    }
    private async Task SeedTeam(string grant)
    {
        using var db=new OracleDbContext(Options(1));
        db.AddRange(new SysScreen { Id=52,SystemId=50,ScreenCode="hr-employees",IsActive=true },new SysScreenFeature { ScreenId=52,FeatureId=62 },
            new SysScreen { Id=54,SystemId=50,ScreenCode="hr-loans",IsActive=true },new SysScreenFeature { ScreenId=54,FeatureId=72 },
            new SysScreen { Id=55,SystemId=50,ScreenCode="hr-payroll",IsActive=true },new SysScreenFeature { ScreenId=55,FeatureId=72 },
            new Employee { EmployeeCode="DIRECT",NameEn="Direct",BranchId=7,ManagerEmployeeCode="SELF",NationalId="PRIVATE_ID",BankIban="PRIVATE_IBAN",Phone="PRIVATE_PHONE" },
            new Employee { EmployeeCode="PEER",BranchId=7 },new Employee { EmployeeCode="GRANDCHILD",BranchId=7,ManagerEmployeeCode="DIRECT" });
        (await db.Employees.SingleAsync(e=>e.EmployeeCode=="OTHER")).ManagerEmployeeCode="SELF";
        if(grant is "user" or "otherbranch")
            db.Add(new SysUserScreenPermission { Id=80,UserId=12,ScreenId=52,FeatureId=62,BranchId=grant=="otherbranch"?8:7,IsGranted=true });
        if(grant is "role" or "deny")
        {
            db.Add(new SysRole { Id=90,IsActive=true }); (await db.SysUsers.SingleAsync()).RoleId=90;
            db.Add(new SysRoleScreenPermission { Id=81,RoleId=90,ScreenId=52,FeatureId=62,BranchId=7,IsGranted=true });
        }
        if(grant=="deny") db.Add(new SysUserScreenPermission { Id=80,UserId=12,ScreenId=52,FeatureId=62,BranchId=7,IsGranted=false });
        if(grant=="otherbranch") db.AddRange(new SysUserBranch { Id=2,UserId=12,BranchId=8 },new SysBranchSystem { Id=71,BranchId=8,SystemId=50 });
        await db.SaveChangesAsync();
    }
    [Theory] [InlineData("user")] [InlineData("role")]
    public async Task ExplicitUserOrRoleGrantReadsOnlyDirectTeamAndOrganizationalFields(string grant)
    {
        await SeedTeam(grant);
        using var response=await Send("GET","/api/hr/employees/me/team?userId=999&managerEmployeeCode=OTHER");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data=await Data(response); Assert.Equal(1,data.GetProperty("totalCount").GetInt32());
        var item=Assert.Single(data.GetProperty("items").EnumerateArray()); Assert.Equal("DIRECT",item.GetProperty("employeeCode").GetString());
        Assert.Equal(new[] { "branchId","departmentCode","employeeCode","employmentStatus","isActive","nameEn","nameLocal","positionCode" },item.EnumerateObject().Select(p=>p.Name).OrderBy(n=>n));
        Assert.DoesNotContain("PRIVATE",item.GetRawText());
        using var other=await Send("GET","/api/hr/employees/me/team",company:2); Assert.Equal(HttpStatusCode.Forbidden,other.StatusCode);
    }
    [Theory] [InlineData("none")] [InlineData("deny")]
    public async Task ManagerRelationshipCannotReplacePermissionOrOverrideExplicitDenial(string grant)
    {
        await SeedTeam(grant); using var response=await Send("GET","/api/hr/employees/me/team");
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }
    [Fact] public async Task DirectTeamHttpFiltersInactiveMembersAndBindsSearchAndPaging()
    {
        await SeedTeam("user");
        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new Employee { EmployeeCode = "INACTIVE", NameLocal = "متوقف", NameEn = "Inactive", BranchId = 7,
                ManagerEmployeeCode = "SELF", EmploymentStatus = "TERMINATED", IsActive = false });
            await db.SaveChangesAsync();
        }

        using var defaultActiveOnly = await Send("GET", "/api/hr/employees/me/team?search=INACTIVE&pageIndex=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, defaultActiveOnly.StatusCode);
        var activeData = await Data(defaultActiveOnly);
        Assert.Equal(0, activeData.GetProperty("totalCount").GetInt32());
        Assert.Empty(activeData.GetProperty("items").EnumerateArray());

        using var includeInactive = await Send("GET", "/api/hr/employees/me/team?activeOnly=false&search=Inactive&pageIndex=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, includeInactive.StatusCode);
        var allData = await Data(includeInactive);
        Assert.Equal(1, allData.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(allData.GetProperty("items").EnumerateArray());
        Assert.Equal("INACTIVE", item.GetProperty("employeeCode").GetString());
        Assert.False(item.GetProperty("isActive").GetBoolean());
    }
    [Fact] public async Task DirectTeamHttpRejectsUnboundedPageAndSearchQueries()
    {
        await SeedTeam("user");
        var invalidQueries = new[]
        {
            "?pageSize=0",
            "?pageSize=201",
            "?pageIndex=2147483647&pageSize=200",
            "?search=" + new string('x', 201)
        };

        foreach (var query in invalidQueries)
        {
            using var response = await Send("GET", "/api/hr/employees/me/team" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
    [Theory]
    [InlineData("account", HttpStatusCode.Forbidden)]
    [InlineData("inactive-employee", HttpStatusCode.NotFound)]
    [InlineData("suspended-employee", HttpStatusCode.NotFound)]
    public async Task DirectTeamHttpRequiresAnActiveManagerAccountAndEmployee(string disabledPart, HttpStatusCode expected)
    {
        await SeedTeam("user");
        using (var db = new OracleDbContext(Options(1)))
        {
            if (disabledPart == "account")
                (await db.SysUsers.SingleAsync()).IsActive = false;
            else
            {
                var manager = await db.Employees.SingleAsync(e => e.EmployeeCode == "SELF");
                if (disabledPart == "inactive-employee") manager.IsActive = false;
                else manager.EmploymentStatus = "SUSPENDED";
            }
            await db.SaveChangesAsync();
        }

        using var response = await Send("GET", "/api/hr/employees/me/team");
        Assert.Equal(expected, response.StatusCode);
    }
    [Theory] [InlineData("user")] [InlineData("role")]
    public async Task ManagerViewGrantDoesNotGrantEmployeeWritesOrLeaveApproval(string grantSource)
    {
        await SeedTeam(grantSource);
        using var team = await Send("GET", "/api/hr/employees/me/team");
        Assert.Equal(HttpStatusCode.OK, team.StatusCode);
        using var employeeList = await Send("GET", "/api/hr/employees?pageIndex=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, employeeList.StatusCode);
        var visibleEmployees = (await Data(employeeList)).GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(visibleEmployees, item => item.GetProperty("employeeCode").GetString() == "DIRECT");
        Assert.DoesNotContain(visibleEmployees, item => item.GetProperty("employeeCode").GetString() == "OTHER");
        Assert.All(visibleEmployees, item => Assert.Equal(7, item.GetProperty("branchId").GetInt64()));
        var directSummary = Assert.Single(visibleEmployees, item => item.GetProperty("employeeCode").GetString() == "DIRECT");
        Assert.Null(directSummary.GetProperty("nationalId").GetString());
        Assert.Null(directSummary.GetProperty("phone").GetString());
        Assert.Null(directSummary.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, directSummary.GetProperty("userId").ValueKind);
        Assert.Equal(JsonValueKind.Null, directSummary.GetProperty("basicSalary").ValueKind);
        using var profile = await Send("GET", "/api/hr/employees/DIRECT");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var visibleProfile = await Data(profile);
        Assert.Null(visibleProfile.GetProperty("nationalId").GetString());
        Assert.Null(visibleProfile.GetProperty("bankIban").GetString());
        Assert.Equal(JsonValueKind.Null, visibleProfile.GetProperty("activeSalaryStructure").ValueKind);
        using var ownLink = await Send("GET", "/api/hr/employees/me/user-link");
        Assert.Equal(HttpStatusCode.OK, ownLink.StatusCode);
        Assert.Equal("SELF", (await Data(ownLink)).GetProperty("employeeCode").GetString());
        using var template = await Send("GET", "/api/hr/employees/export-template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        using var salaryRead = await Send("GET", "/api/hr/employees/DIRECT/salary-structure");
        Assert.Equal(HttpStatusCode.Forbidden, salaryRead.StatusCode);

        var attemptedEdit = new UpdateEmployeeDto("Tampered", "Tampered", "PRIVATE_ID", "X", null,
            new(1990, 1, 1), "M", "SINGLE", null, null, new(2020, 1, 1), null, null, null,
            "FULL_TIME", "ACTIVE", null, null, 7, "SELF", null, 0, false, null, null, "HACK", true);
        using var editDenied = await Send("PUT", "/api/hr/employees/DIRECT", attemptedEdit);
        Assert.Equal(HttpStatusCode.Forbidden, editDenied.StatusCode);
        using var workDenied = await Send("PUT", "/api/hr/employees/DIRECT/work-profile",
            new UpdateEmployeeWorkDto("Tampered", "Tampered", new(2021, 1, 1), null, null, null,
                "FULL_TIME", "ACTIVE", null, null, 7, "SELF", true));
        Assert.Equal(HttpStatusCode.Forbidden, workDenied.StatusCode);
        using var bankDenied = await Send("PUT", "/api/hr/employees/DIRECT/bank", new UpdateEmployeeBankDto("Bank", "Account", "HACK"));
        Assert.Equal(HttpStatusCode.Forbidden, bankDenied.StatusCode);
        using var identityDenied = await Send("PUT", "/api/hr/employees/DIRECT/identity",
            new UpdateEmployeeIdentityDto("HACK", "X", null, new(1990, 1, 1), "M", "SINGLE", null, null, null, 0, false));
        Assert.Equal(HttpStatusCode.Forbidden, identityDenied.StatusCode);
        using var dependentDenied = await Send("POST", "/api/hr/employees/DIRECT/dependents",
            new CreateDependentDto("تابع", "Dependent", "CHILD", new(2010, 1, 1), "M", null, false, false));
        Assert.Equal(HttpStatusCode.Forbidden, dependentDenied.StatusCode);
        using var salaryDenied = await Send("POST", "/api/hr/employees/DIRECT/salary-structure",
            new AssignSalaryStructureDto(new(2025, 1, 1), null, 100m, "USD", "CASH", null));
        Assert.Equal(HttpStatusCode.Forbidden, salaryDenied.StatusCode);
        using var deleteDenied = await Send("DELETE", "/api/hr/employees/DIRECT");
        Assert.Equal(HttpStatusCode.Forbidden, deleteDenied.StatusCode);
        using var dependentDeleteDenied = await Send("DELETE", "/api/hr/employees/dependents/999");
        Assert.Equal(HttpStatusCode.Forbidden, dependentDeleteDenied.StatusCode);
        using var exportDenied = await Send("GET", "/api/hr/employees/export");
        Assert.Equal(HttpStatusCode.Forbidden, exportDenied.StatusCode);
        var attemptedCreate = new CreateEmployeeDto("UNAUTHORIZED", "New", "New", "NEW_ID", "X", null,
            new(1990, 1, 1), "M", "SINGLE", null, null, new(2024, 1, 1), null,
            "FULL_TIME", "ACTIVE", null, null, 7, "SELF", null, 0, false, null, null, null);
        using var createDenied = await Send("POST", "/api/hr/employees", attemptedCreate);
        Assert.Equal(HttpStatusCode.Forbidden, createDenied.StatusCode);
        using var linkDenied = await Send("PUT", "/api/hr/employees/DIRECT/user-link", new LinkEmployeeUserDto(12));
        Assert.Equal(HttpStatusCode.Forbidden, linkDenied.StatusCode);
        using var importValidation = new HttpRequestMessage(HttpMethod.Post, "/api/hr/employees/import/validate");
        importValidation.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(1));
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new ByteArrayContent(new byte[] { 1 }), "file", "manager.xlsx");
            importValidation.Content = form;
            using var importDenied = await client.SendAsync(importValidation);
            Assert.Equal(HttpStatusCode.Forbidden, importDenied.StatusCode);
        }
        using var importRun = new HttpRequestMessage(HttpMethod.Post, "/api/hr/employees/import");
        importRun.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(1));
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new ByteArrayContent(new byte[] { 1 }), "file", "manager.xlsx");
            importRun.Content = form;
            using var importRunDenied = await client.SendAsync(importRun);
            Assert.Equal(HttpStatusCode.Forbidden, importRunDenied.StatusCode);
        }

        using (var db = new OracleDbContext(Options(1)))
        {
            db.SysUserScreenPermissions.RemoveRange(await db.SysUserScreenPermissions
                .Where(p => p.UserId == 12 && p.ScreenId == 53 && p.FeatureId == 72)
                .ToListAsync());
            await db.SaveChangesAsync();
        }

        using var denied = await Send("POST", "/api/hr/leaves/requests/999/process", new ProcessLeaveRequestDto(true, null));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var payrollApproveDenied = await Send("POST", "/api/hr/payroll/999/approve");
        Assert.Equal(HttpStatusCode.Forbidden, payrollApproveDenied.StatusCode);
        using var loanApproveDenied = await Send("POST", "/api/hr/loans/999/approve");
        Assert.True(loanApproveDenied.StatusCode == HttpStatusCode.Forbidden,
            $"Expected forbidden, received {(int)loanApproveDenied.StatusCode}: {await loanApproveDenied.Content.ReadAsStringAsync()}");

        using var verify = new OracleDbContext(Options(1));
        Assert.Equal(12, (await verify.Employees.SingleAsync(e => e.EmployeeCode == "SELF")).UserId);
        var direct = await verify.Employees.SingleAsync(e => e.EmployeeCode == "DIRECT");
        Assert.Equal("SELF", direct.ManagerEmployeeCode);
        Assert.Equal(string.Empty, direct.NameLocal);
        Assert.True(direct.IsActive);
        Assert.Equal("PRIVATE_IBAN", direct.BankIban);
        Assert.Empty(await verify.EmployeeDependents.ToListAsync());
        Assert.False(await verify.Employees.AnyAsync(e => e.EmployeeCode == "UNAUTHORIZED"));
        Assert.Empty(await verify.SalaryStructures.Where(s => s.EmployeeCode == "DIRECT").ToListAsync());
        if (grantSource == "user")
            Assert.Contains(await verify.SysUserScreenPermissions.ToListAsync(), p => p.UserId == 12 && p.ScreenId == 52 && p.FeatureId == 62 && p.IsGranted);
        else
            Assert.Contains(await verify.SysRoleScreenPermissions.ToListAsync(), p => p.RoleId == 90 && p.ScreenId == 52 && p.FeatureId == 62 && p.IsGranted);
        Assert.Empty(await verify.SysUserScreenPermissions.Where(p => p.UserId == 12 && p.ScreenId == 53 && p.FeatureId == 72).ToListAsync());
    }
    [Fact] public async Task TeamGrantInReportBranchDoesNotRequireHrViewInManagersOwnBranch()
    {
        await SeedTeam("otherbranch"); using var response=await Send("GET","/api/hr/employees/me/team");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal("OTHER",Assert.Single((await Data(response)).GetProperty("items").EnumerateArray()).GetProperty("employeeCode").GetString());
    }
    [Fact] public async Task TeamAccessStopsAfterAccountUnlinkEvenWithExistingViewGrant()
    {
        await SeedTeam("user");
        using (var db=new OracleDbContext(Options(1))) { (await db.Employees.SingleAsync(e=>e.EmployeeCode=="SELF")).UserId=null; await db.SaveChangesAsync(); }
        using var response=await Send("GET","/api/hr/employees/me/team"); Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }
    [Theory] [InlineData(7,true)] [InlineData(8,false)]
    public async Task WorkbookPreviewUsesRealXlsxReaderAndImportBranchGrant(long branch,bool permitted)
    {
        await SeedTeam("user");
        using(var db=new OracleDbContext(Options(1)))
        {
            db.AddRange(new SysFeature { Id=64,FeatureCode="import",IsActive=true },new SysScreenFeature { ScreenId=52,FeatureId=64 },
                new SysUserScreenPermission { Id=82,UserId=12,BranchId=7,ScreenId=52,FeatureId=64,IsGranted=true });
            await db.SaveChangesAsync();
            await EmployeeFieldPermissionCatalog.EnsureAsync(db,"FIXTURE");
            db.Add(new SysCurrency { ShortNameEn="USD" });
            foreach(var feature in await db.SysFeatures.Where(f=>EmployeeFieldAccess.Features.Contains(f.FeatureCode)).ToListAsync())
                db.Add(new SysUserScreenPermission { UserId=12,BranchId=7,ScreenId=52,FeatureId=feature.Id,IsGranted=true });
            await db.SaveChangesAsync();
        }
        var bytes=new XlsxEmployeeWorkbookWriter().WriteExportWorkbook(new[] { new EmployeeExportRowDto { EmployeeCode="PREVIEW_NEW",NameLocal="Local",NameEn="English",NationalId="PREVIEW_ID",Nationality="X",DateOfBirth=new(1990,1,1),HireDate=new(2024,1,1),BranchId=branch,BasicSalary=750.123m,CurrencyCode="USD",PaymentMethod="CASH" } });
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/hr/employees/import/validate");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token(1));
        using var form=new MultipartFormDataContent(); form.Add(new ByteArrayContent(bytes),"file","preview.xlsx"); request.Content=form;
        using var response=await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data=await Data(response); Assert.Equal(permitted,data.GetProperty("isValid").GetBoolean());
        if(permitted)
        {
            var preview=Assert.Single(data.GetProperty("previewRows").EnumerateArray()); Assert.Equal("INSERT",preview.GetProperty("action").GetString());
            Assert.Equal("USD",preview.GetProperty("currencyCode").GetString()); Assert.Equal("CASH",preview.GetProperty("paymentMethod").GetString());
        }
        else { Assert.Empty(data.GetProperty("previewRows").EnumerateArray()); Assert.Contains(data.GetProperty("errors").EnumerateArray(),e=>e.GetProperty("code").GetString() is "IMPORT_SCOPE_UNAVAILABLE" or "EMPLOYEE_FIELD_PERMISSION_REQUIRED"); }
        using var read=new OracleDbContext(Options(1)); Assert.False(await read.Employees.AnyAsync(e=>e.EmployeeCode=="PREVIEW_NEW"));
    }
    private async Task PrivateFixture(string mode)
    {
        await SeedTeam(mode is "salary" or "deny" ? "role" : "user");
        using var db=new OracleDbContext(Options(1));
        db.Add(new SalaryStructure { EmployeeCode="DIRECT",EffectiveFrom=new(2020,1,1),BasicSalary=4567 });
        await db.SaveChangesAsync(); await EmployeeFieldPermissionCatalog.EnsureAsync(db,"FIXTURE");
        var code=mode=="identity"?EmployeeFieldAccess.ViewIdentity:mode=="bank"?EmployeeFieldAccess.ViewBank:EmployeeFieldAccess.ViewSalary;
        var id=await db.SysFeatures.Where(f=>f.FeatureCode==code).Select(f=>f.Id).SingleAsync();
        if(mode is "salary" or "deny") db.Add(new SysRoleScreenPermission { RoleId=90,BranchId=7,ScreenId=52,FeatureId=id,IsGranted=true });
        if(mode is "identity" or "bank" or "deny") db.Add(new SysUserScreenPermission { UserId=12,BranchId=7,ScreenId=52,FeatureId=id,IsGranted=mode!="deny" });
        await db.SaveChangesAsync();
    }
    private async Task CoarseFeature(string code)
    {
        using var db=new OracleDbContext(Options(1));
        var feature=await db.SysFeatures.SingleOrDefaultAsync(f=>f.FeatureCode==code);
        if(feature==null) { feature=new SysFeature { FeatureCode=code,IsActive=true }; db.Add(feature); await db.SaveChangesAsync(); }
        if(!await db.Set<SysScreenFeature>().AnyAsync(f=>f.ScreenId==52 && f.FeatureId==feature.Id)) db.Add(new SysScreenFeature { ScreenId=52,FeatureId=feature.Id });
        db.Add(new SysUserScreenPermission { UserId=12,BranchId=7,ScreenId=52,FeatureId=feature.Id,IsGranted=true }); await db.SaveChangesAsync();
    }
    [Theory] [InlineData("none")] [InlineData("identity")] [InlineData("bank")] [InlineData("salary")] [InlineData("deny")]
    public async Task EmployeeHttpFieldVisibilityUsesIndependentDatabaseGrantsRatherThanAdminClaim(string mode)
    {
        await PrivateFixture(mode);
        using var response=await Send("GET","/api/hr/employees/DIRECT",adminClaim:true); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data=await Data(response); var flags=data.GetProperty("fieldVisibility");
        Assert.Equal(mode=="identity",flags.GetProperty("identity").GetBoolean()); Assert.Equal(mode=="bank",flags.GetProperty("bank").GetBoolean()); Assert.Equal(mode=="salary",flags.GetProperty("salary").GetBoolean());
        Assert.Equal(mode=="identity"?"PRIVATE_ID":null,data.GetProperty("nationalId").GetString()); Assert.Equal(mode=="bank"?"PRIVATE_IBAN":null,data.GetProperty("bankIban").GetString());
        Assert.Equal(mode=="salary",data.GetProperty("activeSalaryStructure").ValueKind!=JsonValueKind.Null);
        using var list=await Send("GET","/api/hr/employees?searchKeyword=PRIVATE_ID");
        Assert.Equal(mode=="identity"?1:0,(await Data(list)).GetProperty("totalCount").GetInt32());
    }
    [Fact] public async Task GeneralEditCannotReplacePrivateFieldsAndWorkDtoRejectsExtraPrivateProperties()
    {
        await PrivateFixture("none"); await CoarseFeature("edit");
        var full=new UpdateEmployeeDto("Direct","Direct","PRIVATE_ID","X",null,new(1990,1,1),"M","SINGLE",null,null,new(2020,1,1),null,null,null,"FULL_TIME","ACTIVE",null,null,7,"SELF",null,0,false,null,null,"HACK",true);
        using var rejected=await Send("PUT","/api/hr/employees/DIRECT",full); Assert.Equal(HttpStatusCode.Forbidden,rejected.StatusCode);
        using var bank=await Send("PUT","/api/hr/employees/DIRECT/bank",new UpdateEmployeeBankDto(null,null,"HACK")); Assert.Equal(HttpStatusCode.Forbidden,bank.StatusCode);
        using var identity=await Send("PUT","/api/hr/employees/DIRECT/identity",new UpdateEmployeeIdentityDto("HACK","X",null,new(1990,1,1),"M","SINGLE",null,null,null,0,false)); Assert.Equal(HttpStatusCode.Forbidden,identity.StatusCode);
        using var partialBank=await Send("PUT","/api/hr/employees/DIRECT/bank",new { BankIban="HACK" }); Assert.Equal(HttpStatusCode.BadRequest,partialBank.StatusCode);
        using var extra=await Send("PUT","/api/hr/employees/DIRECT/work-profile",new { NameLocal="Direct",NameEn="Direct",HireDate="2020-01-01",EmploymentType="FULL_TIME",EmploymentStatus="ACTIVE",BranchId=7,IsActive=true,NationalId="HACK" });
        Assert.Equal(HttpStatusCode.BadRequest,extra.StatusCode);
        using var db=new OracleDbContext(Options(1)); var e=await db.Employees.SingleAsync(x=>x.EmployeeCode=="DIRECT"); Assert.Equal("PRIVATE_ID",e.NationalId); Assert.Equal("PRIVATE_IBAN",e.BankIban);
    }
    [Fact] public async Task GeneralViewAndExportCannotBypassSalaryOrFullWorkbookReadPermissions()
    {
        await PrivateFixture("none"); await CoarseFeature("export");
        using var salary=await Send("GET","/api/hr/employees/DIRECT/salary-structure"); Assert.Equal(HttpStatusCode.Forbidden,salary.StatusCode);
        using var export=await Send("GET","/api/hr/employees/export"); Assert.Equal(HttpStatusCode.Forbidden,export.StatusCode);
        Assert.DoesNotContain("PRIVATE_ID",await export.Content.ReadAsStringAsync());
    }
    [Fact] public async Task FieldPermissionRevocationAppliesToNextRequestWithoutNewToken()
    {
        await PrivateFixture("identity"); using var first=await Send("GET","/api/hr/employees/DIRECT"); Assert.Equal("PRIVATE_ID",(await Data(first)).GetProperty("nationalId").GetString());
        using(var db=new OracleDbContext(Options(1)))
        {
            var id=await db.SysFeatures.Where(f=>f.FeatureCode==EmployeeFieldAccess.ViewIdentity).Select(f=>f.Id).SingleAsync();
            (await db.SysUserScreenPermissions.SingleAsync(p=>p.FeatureId==id)).IsGranted=false; await db.SaveChangesAsync();
        }
        using var second=await Send("GET","/api/hr/employees/DIRECT"); Assert.Null((await Data(second)).GetProperty("nationalId").GetString());
    }
    [Fact] public async Task ImportGrantAloneCannotValidateOrImportPrivateWorkbook()
    {
        await PrivateFixture("none"); await CoarseFeature("import");
        var bytes=new XlsxEmployeeWorkbookWriter().WriteExportWorkbook(new[] { new EmployeeExportRowDto { EmployeeCode="DIRECT",NameLocal="Direct",NameEn="Direct",NationalId="REQUESTED_ID",Nationality="X",DateOfBirth=new(1990,1,1),HireDate=new(2020,1,1),BranchId=7 } });
        foreach(var path in new[] { "import/validate","import?updateExisting=true" })
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,"/api/hr/employees/"+path); request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token(1));
            using var form=new MultipartFormDataContent(); form.Add(new ByteArrayContent(bytes),"file","private.xlsx"); request.Content=form;
            using var response=await client.SendAsync(request);
            if(path=="import/validate") { Assert.Equal(HttpStatusCode.OK,response.StatusCode); var data=await Data(response); Assert.False(data.GetProperty("isValid").GetBoolean()); Assert.Empty(data.GetProperty("previewRows").EnumerateArray()); }
            else Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        }
        using var read=new OracleDbContext(Options(1)); Assert.Equal("PRIVATE_ID",(await read.Employees.SingleAsync(e=>e.EmployeeCode=="DIRECT")).NationalId);
    }
    public async Task DisposeAsync()
    {
        client?.Dispose(); if (app != null) await app.DisposeAsync(); foreach (var gate in captureLocks) gate.Dispose();
    }
}

// EF InMemory has no transaction or row-lock support. Keep the real unit of work for
// other loopback paths; for payroll, verify the period exists and bypass only the Oracle row-lock wrapper.
internal sealed class InMemoryHttpHrUnitOfWork(OracleDbContext db) : IHrUnitOfWork
{
    private readonly HrUnitOfWork inner = new(db);
    public Task<T> ExecuteEmployeeAccountAsync<T>(string employeeCode, Func<Task<T>> action, CancellationToken cancellationToken = default)
        => inner.ExecuteEmployeeAccountAsync(employeeCode, action, cancellationToken);
    public Task<EmployeeImportResultDto> ExecuteEmployeeImportAsync(IReadOnlyList<EmployeeImportRowDto> rows,
        Func<Task<EmployeeImportResultDto>> action, CancellationToken cancellationToken = default)
        => inner.ExecuteEmployeeImportAsync(rows, action, cancellationToken);
    public Task<T> ExecuteNewEmployeeAsync<T>(EmployeeImportRowDto row, Func<Task<T>> action, CancellationToken cancellationToken = default)
        => inner.ExecuteNewEmployeeAsync(row, action, cancellationToken);
    public Task<T> ExecuteEmployeeProfileAsync<T>(string employeeCode, DateTime earliestDate, Func<Task<T>> action, CancellationToken cancellationToken = default)
        => inner.ExecuteEmployeeProfileAsync(employeeCode, earliestDate, action, cancellationToken);
    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        => inner.ExecuteAsync(action, cancellationToken);
    public async Task<T> ExecuteForPayrollPeriodAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsRelational())
            return await inner.ExecuteForPayrollPeriodAsync(companyId, periodCode, action, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await db.PayrollPeriods.AnyAsync(p => p.CompanyId == companyId && p.PeriodCode == periodCode, cancellationToken))
            throw new InvalidOperationException("The payroll period must exist before calculating payroll or changing adjustment approval.");
        return await action();
    }
    public Task<T> ExecuteForPayrollPeriodsAsync<T>(long companyId, string requiredPeriodCode, IReadOnlyCollection<string> affectedPeriodCodes,
        Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecutePayrollFinancialAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default)
        => inner.ExecutePayrollFinancialAsync(companyId, periodCode, action, cancellationToken);
    public Task<T> ExecuteForEmployeeDatesAsync<T>(string employeeCode, DateTime startDate, DateTime endDate,
        Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecuteForCompanyPayrollDatesAsync<T>(long companyId, DateTime startDate, DateTime endDate,
        Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
}
