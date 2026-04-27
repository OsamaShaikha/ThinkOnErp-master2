using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ThinkOnErp.Application;
using ThinkOnErp.Infrastructure;
using ThinkOnErp.Infrastructure.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

// Configure Serilog before building the host
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.With<CorrelationIdEnricher>()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] [{MachineName}] [{ThreadId}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting ThinkOnErp API");

    var builder = WebApplication.CreateBuilder(args);

    // Replace default logging with Serilog
    builder.Host.UseSerilog();

    // Override minimum level based on environment
    if (builder.Environment.IsProduction())
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.With<CorrelationIdEnricher>()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] [{MachineName}] [{ThreadId}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    // Add JWT Authentication
    var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"] 
        ?? throw new InvalidOperationException("JWT SecretKey is not configured");
    var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] 
        ?? throw new InvalidOperationException("JWT Issuer is not configured");
    var jwtAudience = builder.Configuration["JwtSettings:Audience"] 
        ?? throw new InvalidOperationException("JWT Audience is not configured");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });

    // Add Authorization with AdminOnly policy
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy =>
            policy.RequireClaim("isAdmin", "true"));
        
        // Add multi-tenant access control policy
        options.AddPolicy("MultiTenantAccess", policy =>
            policy.Requirements.Add(new ThinkOnErp.Infrastructure.Authorization.MultiTenantAccessRequirement()));
        
        // Add audit data access control policies
        options.AddPolicy("AuditDataAccess", policy =>
            policy.Requirements.Add(new ThinkOnErp.Infrastructure.Authorization.AuditDataAccessRequirement(allowSelfAccess: true)));
        
        options.AddPolicy("AdminOnlyAuditDataAccess", policy =>
            policy.Requirements.Add(new ThinkOnErp.Infrastructure.Authorization.AuditDataAccessRequirement(allowSelfAccess: false)));
    });

    // Register authorization handlers
    builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, 
        ThinkOnErp.Infrastructure.Authorization.MultiTenantAuthorizationHandler>();
    
    builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, 
        ThinkOnErp.Infrastructure.Authorization.AuditDataAuthorizationHandler>();

    // Add Memory Cache for configuration and caching services
    builder.Services.AddMemoryCache();

    // Add HttpContextAccessor for middleware access to HttpContext
    builder.Services.AddHttpContextAccessor();

    // Configure OpenTelemetry for Application Performance Monitoring (APM)
    var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "ThinkOnErp.API";
    var serviceVersion = builder.Configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
    var enableConsoleExporter = builder.Configuration.GetValue<bool>("OpenTelemetry:EnableConsoleExporter", false);
    var enablePrometheusExporter = builder.Configuration.GetValue<bool>("OpenTelemetry:EnablePrometheusExporter", true);

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName,
                ["host.name"] = Environment.MachineName
            }))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    // Capture request and response bodies for detailed tracing
                    options.RecordException = true;
                    
                    // Enrich spans with additional information
                    options.EnrichWithHttpRequest = (activity, httpRequest) =>
                    {
                        // Add correlation ID to span
                        var correlationId = ThinkOnErp.Infrastructure.Services.CorrelationContext.Current;
                        if (!string.IsNullOrEmpty(correlationId))
                        {
                            activity.SetTag("correlation.id", correlationId);
                        }
                        
                        // Add user information if available
                        if (httpRequest.HttpContext.User?.Identity?.IsAuthenticated == true)
                        {
                            var userId = httpRequest.HttpContext.User.FindFirst("userId")?.Value;
                            var companyId = httpRequest.HttpContext.User.FindFirst("companyId")?.Value;
                            
                            if (!string.IsNullOrEmpty(userId))
                                activity.SetTag("user.id", userId);
                            if (!string.IsNullOrEmpty(companyId))
                                activity.SetTag("company.id", companyId);
                        }
                    };
                    
                    // Filter out health check and metrics endpoints
                    options.Filter = (httpContext) =>
                    {
                        var path = httpContext.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
                        return !path.StartsWith("/health") && 
                               !path.StartsWith("/metrics") && 
                               !path.StartsWith("/swagger");
                    };
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddSource("ThinkOnErp.*"); // Capture custom traces from our services

            // Add exporters based on configuration
            if (enableConsoleExporter)
            {
                tracing.AddConsoleExporter();
            }

            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                });
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation() // CPU, memory, GC metrics
                .AddMeter("ThinkOnErp.*"); // Capture custom metrics from our services

            // Add exporters based on configuration
            if (enableConsoleExporter)
            {
                metrics.AddConsoleExporter();
            }

            if (enablePrometheusExporter)
            {
                metrics.AddPrometheusExporter();
            }

            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                metrics.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                });
            }
        });

    // Register Application and Infrastructure services
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add services to the container.
    builder.Services.AddControllers();
    
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        // API Information
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "ThinkOnErp API - Full Traceability System",
            Version = "v1.0",
            Description = @"
# ThinkOnErp Enterprise Resource Planning API

## Overview
Enterprise Resource Planning API with comprehensive audit logging, request tracing, and compliance monitoring capabilities.

## Features
- **Full Audit Trail**: Complete tracking of all data modifications, authentication events, and API requests
- **Compliance Reporting**: GDPR, SOX, and ISO 27001 compliance reports
- **Performance Monitoring**: Real-time system health, performance metrics, and slow query detection
- **Security Monitoring**: Threat detection, failed login tracking, and anomaly detection
- **Alert Management**: Configurable alert rules with multiple notification channels (email, webhook, SMS)

## Authentication
All endpoints (except health checks) require JWT Bearer authentication. Use the `/api/auth/login` endpoint to obtain a token.

## Authorization
- **Admin-Only Endpoints**: Audit logs, compliance reports, monitoring, and alerts require admin privileges
- **Multi-Tenant Access**: All data access is automatically filtered by user's company and branch permissions

## Audit Trail API
The audit trail system provides comprehensive logging and monitoring:
- **AuditLogs**: Query audit logs, view entity history, trace requests by correlation ID
- **Compliance**: Generate GDPR, SOX, and ISO 27001 compliance reports
- **Monitoring**: System health, performance metrics, memory usage, security threats
- **Alerts**: Configure alert rules, view alert history, acknowledge and resolve alerts

## Rate Limiting
API requests are subject to rate limiting to prevent abuse. Failed login attempts are tracked and blocked after 5 attempts within 5 minutes.

## Support
For API support, contact the development team or refer to the comprehensive documentation at /swagger.",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "ThinkOnErp Development Team",
                Email = "support@thinkonerp.com"
            },
            License = new Microsoft.OpenApi.Models.OpenApiLicense
            {
                Name = "Proprietary License",
                Url = new Uri("https://thinkonerp.com/license")
            }
        });

        // Add JWT Bearer authentication to Swagger
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = @"JWT Authorization header using the Bearer scheme.

Enter your JWT token in the text input below.

Example: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'

**Note:** Do NOT include the 'Bearer ' prefix - it will be added automatically."
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Include XML documentation comments from API project
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }

        // Include XML documentation from Application layer (DTOs)
        var applicationXmlFile = "ThinkOnErp.Application.xml";
        var applicationXmlPath = Path.Combine(AppContext.BaseDirectory, applicationXmlFile);
        if (File.Exists(applicationXmlPath))
        {
            options.IncludeXmlComments(applicationXmlPath);
        }

        // Include XML documentation from Domain layer (models)
        var domainXmlFile = "ThinkOnErp.Domain.xml";
        var domainXmlPath = Path.Combine(AppContext.BaseDirectory, domainXmlFile);
        if (File.Exists(domainXmlPath))
        {
            options.IncludeXmlComments(domainXmlPath);
        }

        // Group endpoints by tags for better organization
        options.TagActionsBy(api =>
        {
            if (api.GroupName != null)
            {
                return new[] { api.GroupName };
            }

            var controllerName = api.ActionDescriptor.RouteValues["controller"];
            return new[] { controllerName ?? "Default" };
        });

        // Add custom operation filters for enhanced documentation
        options.EnableAnnotations();
        
        // Order actions by HTTP method and then by path
        options.OrderActionsBy(apiDesc => 
            $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.HttpMethod}_{apiDesc.RelativePath}");

        // Use full schema names to avoid conflicts
        options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

        // Add example values for common types
        options.MapType<DateTime>(() => new Microsoft.OpenApi.Models.OpenApiSchema
        {
            Type = "string",
            Format = "date-time",
            Example = new Microsoft.OpenApi.Any.OpenApiString("2024-01-15T10:30:00Z")
        });

        options.MapType<TimeSpan>(() => new Microsoft.OpenApi.Models.OpenApiSchema
        {
            Type = "string",
            Format = "duration",
            Example = new Microsoft.OpenApi.Any.OpenApiString("01:30:00")
        });
    });

    var app = builder.Build();

    // Add request tracing middleware (must be early in pipeline to capture all requests and generate correlation IDs)
    app.UseMiddleware<ThinkOnErp.API.Middleware.RequestTracingMiddleware>();

    // Add global exception handling middleware (must be after request tracing to capture exceptions with correlation ID)
    app.UseMiddleware<ThinkOnErp.API.Middleware.ExceptionHandlingMiddleware>();

    // Configure the HTTP request pipeline.
    //if (app.Environment.IsDevelopment())
    //{
        app.UseSwagger();
        app.UseSwaggerUI();
    //}

    app.UseHttpsRedirection();

    // Add Prometheus metrics endpoint (if enabled)
    if (builder.Configuration.GetValue<bool>("OpenTelemetry:EnablePrometheusExporter", true))
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
    }

    // Add authentication and authorization middleware
    app.UseAuthentication();
    
    // Add force logout check middleware (after authentication, before authorization)
    app.UseMiddleware<ThinkOnErp.API.Middleware.ForceLogoutMiddleware>();
    
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible to integration tests
public partial class Program { }
