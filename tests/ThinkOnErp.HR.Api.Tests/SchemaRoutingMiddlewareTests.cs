using System.Security.Claims;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Middleware;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class SchemaRoutingMiddlewareTests
{
    [Fact]
    public async Task AuthenticatedCompanyRequestUsesRegistryResolvedSchema()
    {
        var tenant = new TenantRequestContext(1, "CO1", "HR_CO1");
        var resolver = new FakeTenantSchemaResolver((id, code, schema, _) =>
        {
            Assert.Equal(1, id);
            Assert.Equal("CO1", code);
            Assert.Equal("HR_CO1", schema);
            return tenant;
        });
        var session = new FakeTenantSchemaSession();
        var wasInvoked = false;
        var middleware = CreateMiddleware(resolver, session, _ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        var context = TenantRequest(claims: new Dictionary<string, string>
        {
            ["companyId"] = "1",
            ["companyCode"] = "CO1",
            ["companySchema"] = "HR_CO1"
        });

        await InvokeAsync(middleware, context);

        Assert.True(wasInvoked);
        Assert.Equal("HR_CO1", Assert.Single(session.Schemas));
        Assert.Same(tenant, context.Items[TenantRequestContext.HttpContextItemKey]);
        Assert.Equal("HR_CO1", context.Items[SchemaRoutingMiddleware.TenantSchemaContextItem]);
    }

    [Fact]
    public async Task AuthenticatedCompanyRequestRejectsResolverResultForAnotherCompany()
    {
        var resolver = new FakeTenantSchemaResolver((_, _, _, _) => new TenantRequestContext(2, "CO2", "HR_CO2"));
        var session = new FakeTenantSchemaSession();
        var middleware = CreateMiddleware(resolver, session, _ => Task.CompletedTask);
        var context = TenantRequest(claims: new Dictionary<string, string>
        {
            ["companyId"] = "1",
            ["companyCode"] = "CO1",
            ["companySchema"] = "HR_CO1"
        });

        await InvokeAsync(middleware, context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Empty(session.Schemas);
    }

    [Fact]
    public async Task AuthenticatedCompanyRequestPassesRequestCancellationToResolverAndSession()
    {
        var resolver = new FakeTenantSchemaResolver((_, _, _, _) => new TenantRequestContext(1, "CO1", "HR_CO1"));
        var session = new FakeTenantSchemaSession();
        var middleware = CreateMiddleware(resolver, session, _ => Task.CompletedTask);
        using var cancellation = new CancellationTokenSource();
        var context = TenantRequest(claims: new Dictionary<string, string>
        {
            ["companyId"] = "1",
            ["companyCode"] = "CO1",
            ["companySchema"] = "HR_CO1"
        });
        context.RequestAborted = cancellation.Token;

        await InvokeAsync(middleware, context);

        Assert.Equal(cancellation.Token, Assert.Single(resolver.Calls).Token);
        Assert.Equal(cancellation.Token, Assert.Single(session.CancellationTokens));
    }

    [Fact]
    public async Task CompanyTokenSchemaMustStillMatchTheRegistry()
    {
        var resolver = new FakeTenantSchemaResolver((id, code, schema, _) =>
        {
            Assert.Equal(1, id);
            Assert.Equal("CO1", code);
            Assert.Equal("HR_FORGED", schema);
            return null;
        });
        var session = new FakeTenantSchemaSession();
        var wasInvoked = false;
        var middleware = CreateMiddleware(resolver, session, _ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        var context = TenantRequest(claims: new Dictionary<string, string>
        {
            ["companyId"] = "1",
            ["companyCode"] = "CO1",
            ["companySchema"] = "HR_FORGED"
        });

        await InvokeAsync(middleware, context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(wasInvoked);
        Assert.Empty(session.Schemas);
    }

    [Fact]
    public async Task CompanyUserCannotOverrideTokenTenantWithRequestSelector()
    {
        var resolver = new FakeTenantSchemaResolver((_, _, _, _) => new TenantRequestContext(1, "CO1", "HR_CO1"));
        var session = new FakeTenantSchemaSession();
        var wasInvoked = false;
        var middleware = CreateMiddleware(resolver, session, _ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        var context = TenantRequest(claims: new Dictionary<string, string>
        {
            ["companyId"] = "1",
            ["companyCode"] = "CO1",
            ["companySchema"] = "HR_CO1"
        });
        context.Request.Headers["X-Company-Id"] = "2";

        await InvokeAsync(middleware, context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(wasInvoked);
        Assert.Empty(session.Schemas);
        Assert.Single(resolver.Calls);
    }

    [Fact]
    public async Task SuperAdminUsesSchemaReturnedForExplicitCompanySelection()
    {
        var tenant = new TenantRequestContext(2, "CO2", "HR_CO2");
        var resolver = new FakeTenantSchemaResolver((id, code, schema, _) =>
        {
            Assert.Equal(2, id);
            Assert.Null(code);
            Assert.Null(schema);
            return tenant;
        });
        var session = new FakeTenantSchemaSession();
        var middleware = CreateMiddleware(resolver, session, _ => Task.CompletedTask);
        var context = TenantRequest(claims: new Dictionary<string, string> { ["isSuperAdmin"] = "true" });
        context.Request.Headers["X-Company-Id"] = "2";

        await InvokeAsync(middleware, context);

        Assert.Equal("HR_CO2", Assert.Single(session.Schemas));
        Assert.Same(tenant, context.Items[TenantRequestContext.HttpContextItemKey]);
    }

    [Fact]
    public async Task SuperAdminRejectsResolverResultThatDoesNotMatchExplicitSelection()
    {
        var resolver = new FakeTenantSchemaResolver((_, _, _, _) => new TenantRequestContext(1, "CO1", "HR_CO1"));
        var session = new FakeTenantSchemaSession();
        var middleware = CreateMiddleware(resolver, session, _ => Task.CompletedTask);
        var context = TenantRequest(claims: new Dictionary<string, string> { ["isSuperAdmin"] = "true" });
        context.Request.Headers["X-Company-Id"] = "2";

        await InvokeAsync(middleware, context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Empty(session.Schemas);
    }

    [Fact]
    public async Task SuperAdminRejectsResolverResultWithInvalidSchemaBeforeOpeningSession()
    {
        var resolver = new FakeTenantSchemaResolver((_, _, _, _) => new TenantRequestContext(2, "CO2", "HR_CO2\"; DROP TABLE SYS_COMPANY"));
        var session = new FakeTenantSchemaSession();
        var middleware = CreateMiddleware(resolver, session, _ => Task.CompletedTask);
        var context = TenantRequest(claims: new Dictionary<string, string> { ["isSuperAdmin"] = "true" });
        context.Request.Headers["X-Company-Id"] = "2";

        await InvokeAsync(middleware, context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Empty(session.Schemas);
    }

    [Fact]
    public async Task SuperAdminMustSelectACompanyBeforeTenantResolution()
    {
        var resolver = new FakeTenantSchemaResolver((_, _, _, _) => throw new Xunit.Sdk.XunitException("Resolver must not be called."));
        var session = new FakeTenantSchemaSession();
        var wasInvoked = false;
        var middleware = CreateMiddleware(resolver, session, _ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        var context = TenantRequest(claims: new Dictionary<string, string> { ["isSuperAdmin"] = "true" });

        await InvokeAsync(middleware, context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(wasInvoked);
        Assert.Empty(resolver.Calls);
        Assert.Empty(session.Schemas);
    }

    [Fact]
    public async Task MalformedCompanySelectorIsRejectedBeforeRegistryLookup()
    {
        var resolver = new FakeTenantSchemaResolver((_, _, _, _) => throw new Xunit.Sdk.XunitException("Resolver must not be called."));
        var session = new FakeTenantSchemaSession();
        var wasInvoked = false;
        var middleware = CreateMiddleware(resolver, session, _ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        var context = TenantRequest(claims: new Dictionary<string, string> { ["isSuperAdmin"] = "true" });
        context.Request.Headers.Append("X-Company-Id", "2");
        context.Request.Headers.Append("X-Company-Id", "3");

        await InvokeAsync(middleware, context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(wasInvoked);
        Assert.Empty(resolver.Calls);
        Assert.Empty(session.Schemas);
    }

    [Fact]
    public async Task SuperAdminMustUseOnlyOneCompanySelector()
    {
        var resolver = new FakeTenantSchemaResolver((_, _, _, _) => throw new Xunit.Sdk.XunitException("Resolver must not be called."));
        var session = new FakeTenantSchemaSession();
        var middleware = CreateMiddleware(resolver, session, _ => Task.CompletedTask);
        var context = TenantRequest(claims: new Dictionary<string, string> { ["isSuperAdmin"] = "true" });
        context.Request.Headers["X-Company-Id"] = "2";
        context.Request.Headers["X-Company-Code"] = "CO2";

        await InvokeAsync(middleware, context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Empty(resolver.Calls);
        Assert.Empty(session.Schemas);
    }

    [Fact]
    public async Task UnauthenticatedTenantRequestContinuesToAuthorizationWithoutResolvingSchema()
    {
        var resolver = new FakeTenantSchemaResolver((_, _, _, _) => throw new Xunit.Sdk.XunitException("Resolver must not be called."));
        var session = new FakeTenantSchemaSession();
        var wasInvoked = false;
        var middleware = CreateMiddleware(resolver, session, _ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        var context = TenantRequest(claims: null);

        await InvokeAsync(middleware, context);

        Assert.True(wasInvoked);
        Assert.Empty(resolver.Calls);
        Assert.Empty(session.Schemas);
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("HR_CO1", true)]
    [InlineData("HR$CO1", true)]
    [InlineData("1HR", false)]
    [InlineData("HR-CO1", false)]
    [InlineData("HR\"; DROP TABLE SYS_COMPANY", false)]
    [InlineData("", false)]
    public void OracleSchemaIdentifierAcceptsOnlySimpleOracleIdentifiers(string? value, bool expected)
    {
        Assert.Equal(expected, OracleSchemaIdentifier.IsValid(value));
    }

    [Fact]
    public void OracleSchemaIdentifierEnforcesOracleIdentifierLengthLimit()
    {
        Assert.True(OracleSchemaIdentifier.IsValid("A" + new string('X', 127)));
        Assert.False(OracleSchemaIdentifier.IsValid("A" + new string('X', 128)));
    }

    [Fact]
    public void OracleTenantSchemaSessionBuildsQuotedUppercaseCurrentSchemaStatement()
    {
        Assert.Equal(
            "ALTER SESSION SET CURRENT_SCHEMA = \"HR_CO$#2\"",
            OracleTenantSchemaSession.BuildSetCurrentSchemaStatement("hr_co$#2"));
    }

    [Fact]
    public void OracleTenantSchemaSessionDoesNotBuildStatementForInjectedSchema()
    {
        Assert.Throws<InvalidOperationException>(() => OracleTenantSchemaSession.BuildSetCurrentSchemaStatement("HR_CO2\"; DROP TABLE SYS_COMPANY"));
    }

    [Theory]
    [InlineData("APP_MASTER", true)]
    [InlineData("APP-MASTER", false)]
    [InlineData("1APP_MASTER", false)]
    public void OracleTenantSchemaResolverValidatesConfiguredRegistryOwner(string owner, bool expectedValid)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OracleDb"] = $"User Id={owner};Password=unused;Data Source=unused"
        }).Build();

        if (expectedValid)
        {
            _ = new OracleTenantSchemaResolver(configuration, NullLogger<OracleTenantSchemaResolver>.Instance);
            return;
        }

        Assert.Throws<InvalidOperationException>(() => new OracleTenantSchemaResolver(
            configuration,
            NullLogger<OracleTenantSchemaResolver>.Instance));
    }

    [Fact]
    public async Task OracleTenantSchemaSessionSetsTenantAndRestoresMasterBeforeClosingOwnedConnection()
    {
        var harness = CreateOracleSessionHarness();
        var invoked = false;

        await harness.Session.RunAsync(harness.DbContext, "hr_co2", CancellationToken.None, () =>
        {
            invoked = true;
            Assert.True(harness.IsOpen());
            return Task.CompletedTask;
        });

        Assert.True(invoked);
        Assert.Equal(new[] { "HR_CO2", "APP_MASTER" }, harness.Schemas);
        Assert.False(harness.IsOpen());
        harness.Connection.Verify(c => c.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.Connection.Verify(c => c.CloseAsync(), Times.Once);
    }

    [Fact]
    public async Task OracleTenantSchemaSessionRestoresMasterAndClosesConnectionWhenRequestFails()
    {
        var harness = CreateOracleSessionHarness();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.RunAsync(
            harness.DbContext,
            "HR_CO2",
            CancellationToken.None,
            () => throw new InvalidOperationException("Request failed.")));

        Assert.Equal(new[] { "HR_CO2", "APP_MASTER" }, harness.Schemas);
        Assert.False(harness.IsOpen());
        harness.Connection.Verify(c => c.CloseAsync(), Times.Once);
    }

    [Fact]
    public async Task OracleTenantSchemaSessionRejectsInvalidSchemaAndOnlySetsMasterSchema()
    {
        var harness = CreateOracleSessionHarness();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.RunAsync(
            harness.DbContext,
            "HR_CO2\"; DROP TABLE SYS_COMPANY",
            CancellationToken.None,
            () => throw new Xunit.Sdk.XunitException("Request must not execute for an invalid schema.")));

        Assert.Equal(new[] { "APP_MASTER" }, harness.Schemas);
        Assert.False(harness.IsOpen());
        harness.Connection.Verify(c => c.CloseAsync(), Times.Once);
    }

    [Fact]
    public async Task OracleTenantSchemaSessionRestoresButDoesNotCloseConnectionOpenedByCaller()
    {
        var harness = CreateOracleSessionHarness();
        harness.SetOpen();

        await harness.Session.RunAsync(harness.DbContext, "HR_CO2", CancellationToken.None, () => Task.CompletedTask);

        Assert.Equal(new[] { "HR_CO2", "APP_MASTER" }, harness.Schemas);
        Assert.True(harness.IsOpen());
        harness.Connection.Verify(c => c.OpenAsync(It.IsAny<CancellationToken>()), Times.Never);
        harness.Connection.Verify(c => c.CloseAsync(), Times.Never);
    }

    [Fact]
    public async Task OracleTenantSchemaSessionRestoresMasterWithUncancelledTokenAfterRequestCancellation()
    {
        var harness = CreateOracleSessionHarness();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Session.RunAsync(
            harness.DbContext,
            "HR_CO2",
            cancellation.Token,
            () => Task.FromCanceled(cancellation.Token)));

        Assert.Equal(new[] { "HR_CO2", "APP_MASTER" }, harness.Schemas);
        Assert.True(harness.SchemaTokens[0].IsCancellationRequested);
        Assert.False(harness.SchemaTokens[1].CanBeCanceled);
        Assert.False(harness.IsOpen());
    }

    [Fact]
    public async Task OracleTenantSchemaSessionClosesConnectionAndPreservesRequestFailureWhenResetFails()
    {
        var harness = CreateOracleSessionHarness(failMasterReset: true);
        var requestFailure = new InvalidOperationException("Request failed before reset.");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.RunAsync(
            harness.DbContext,
            "HR_CO2",
            CancellationToken.None,
            () => throw requestFailure));

        Assert.Same(requestFailure, thrown);
        Assert.Equal(new[] { "HR_CO2", "APP_MASTER" }, harness.Schemas);
        Assert.False(harness.IsOpen());
        harness.Connection.Verify(c => c.CloseAsync(), Times.Once);
    }

    [Fact]
    public async Task OracleTenantSchemaSessionAttemptsMasterResetAndClosesConnectionWhenTenantSwitchFails()
    {
        var harness = CreateOracleSessionHarness(failTenantSwitch: true);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.RunAsync(
            harness.DbContext,
            "HR_CO2",
            CancellationToken.None,
            () => throw new Xunit.Sdk.XunitException("Request must not run when tenant schema switch fails.")));

        Assert.Equal("Tenant schema switch failed.", thrown.Message);
        Assert.Equal(new[] { "HR_CO2", "APP_MASTER" }, harness.Schemas);
        Assert.False(harness.IsOpen());
        harness.Connection.Verify(c => c.CloseAsync(), Times.Once);
    }

    private static OracleSessionHarness CreateOracleSessionHarness(
        bool failMasterReset = false,
        bool failTenantSwitch = false)
    {
        var isOpen = false;
        var connection = new Mock<OracleConnection>();
        connection.SetupGet(c => c.State).Returns(() => isOpen ? ConnectionState.Open : ConnectionState.Closed);
        connection.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>()))
            .Callback(() => isOpen = true)
            .Returns(Task.CompletedTask);
        connection.Setup(c => c.CloseAsync())
            .Callback(() => isOpen = false)
            .Returns(Task.CompletedTask);

        var schemas = new List<string>();
        var schemaTokens = new List<CancellationToken>();

        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle(connection.Object, oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options;
        var dbContext = new OracleDbContext(options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OracleDb"] = "User Id=APP_MASTER;Password=unused;Data Source=unused"
        }).Build();
        var session = new OracleTenantSchemaSession(
            configuration,
            NullLogger<OracleTenantSchemaSession>.Instance,
            (_, schema, cancellationToken) =>
            {
                if (!OracleSchemaIdentifier.IsValid(schema))
                    throw new InvalidOperationException("Invalid Oracle schema identifier.");
                schemas.Add(schema.ToUpperInvariant());
                schemaTokens.Add(cancellationToken);
                if (failTenantSwitch && string.Equals(schema, "HR_CO2", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Tenant schema switch failed.");
                if (failMasterReset && string.Equals(schema, "APP_MASTER", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Master schema reset failed.");
                return Task.CompletedTask;
            });
        return new OracleSessionHarness(dbContext, connection, session, schemas, schemaTokens, () => isOpen, () => isOpen = true);
    }

    private static SchemaRoutingMiddleware CreateMiddleware(
        ITenantSchemaResolver resolver,
        ITenantSchemaSession session,
        RequestDelegate next) => new(next, resolver, session);

    private static DefaultHttpContext TenantRequest(Dictionary<string, string>? claims)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging()
                .AddSingleton(new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options))
                .BuildServiceProvider()
        };
        context.Request.Path = "/api/hr/employees";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new TenantScopedAttribute()), "tenant-scoped-test"));
        if (claims != null)
        {
            var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.Key, c.Value)), "test");
            context.User = new ClaimsPrincipal(identity);
        }
        return context;
    }

    private static async Task InvokeAsync(SchemaRoutingMiddleware middleware, HttpContext context)
    {
        using var requestServices = (IDisposable)context.RequestServices;
        await middleware.InvokeAsync(context);
    }

    private sealed class FakeTenantSchemaResolver(
        Func<long?, string?, string?, CancellationToken, TenantRequestContext?> resolve) : ITenantSchemaResolver
    {
        public List<(long? CompanyId, string? CompanyCode, string? ExpectedSchema, CancellationToken Token)> Calls { get; } = [];

        public Task<TenantRequestContext?> ResolveAsync(
            long? companyId,
            string? companyCode,
            string? expectedSchema,
            CancellationToken cancellationToken)
        {
            Calls.Add((companyId, companyCode, expectedSchema, cancellationToken));
            return Task.FromResult(resolve(companyId, companyCode, expectedSchema, cancellationToken));
        }
    }

    private sealed class FakeTenantSchemaSession : ITenantSchemaSession
    {
        public List<string> Schemas { get; } = [];
        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task RunAsync(OracleDbContext dbContext, string schema, CancellationToken cancellationToken, Func<Task> next)
        {
            Schemas.Add(schema);
            CancellationTokens.Add(cancellationToken);
            return next();
        }
    }

    private sealed record OracleSessionHarness(
        OracleDbContext DbContext,
        Mock<OracleConnection> Connection,
        OracleTenantSchemaSession Session,
        List<string> Schemas,
        List<CancellationToken> SchemaTokens,
        Func<bool> IsOpen,
        Action SetOpen);
}
