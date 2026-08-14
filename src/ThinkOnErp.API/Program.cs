using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ThinkOnErp.Application;
using ThinkOnErp.Infrastructure;
using ThinkOnErp.Infrastructure.Logging;
using ThinkOnErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Oracle.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Domain.Interfaces;

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
        // Read log path from SYS_SETTINGS (SETTING_CODE=4), fallback to default
        var logPath = "logs/log-.txt";
        try
        {
            var connString = builder.Configuration.GetConnectionString("OracleDb");
            if (!string.IsNullOrEmpty(connString))
            {
                var optionsBuilder = new DbContextOptionsBuilder<OracleDbContext>();
                optionsBuilder.UseOracle(connString);
                using var tempContext = new OracleDbContext(optionsBuilder.Options);
                var setting = tempContext.SysSettings
                    .AsNoTracking()
                    .FirstOrDefault(s => s.SettingCode == 4);
                if (setting != null && !string.IsNullOrEmpty(setting.SettingValue))
                    logPath = Path.Combine(setting.SettingValue.TrimEnd('/'), "log-.txt");
            }
        }
        catch
        {
            // Fallback to default path if DB read fails
        }

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
                path: logPath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 104857600,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 90,
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

        // Tenant configuration must run only after SchemaRoutingMiddleware
        // has resolved an active company from the central registry.
        options.AddPolicy("TenantAdminOnly", policy =>
        {
            policy.RequireClaim("isAdmin", "true");
            policy.RequireAssertion(context =>
                context.Resource is HttpContext httpContext &&
                httpContext.Items.ContainsKey(
                    ThinkOnErp.Domain.Models.TenantRequestContext.HttpContextItemKey));
        });
        
        // Add multi-tenant access control policy
        options.AddPolicy("MultiTenantAccess", policy =>
            policy.Requirements.Add(new ThinkOnErp.Infrastructure.Authorization.MultiTenantAccessRequirement()));
        
        // Add SuperAdminOnly policy for super admin endpoints
        options.AddPolicy("SuperAdminOnly", policy =>
            policy.RequireClaim("isSuperAdmin", "true"));
        
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

    // Add CORS policy for frontend applications
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

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

            if (!string.IsNullOrWhiteSpace(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = otlpUri;
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

            if (!string.IsNullOrWhiteSpace(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri))
            {
                metrics.AddOtlpExporter(options =>
                {
                    options.Endpoint = otlpUri;
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
    builder.Services.AddSingleton<ThinkOnErp.API.Swagger.SwaggerCategoryLoader>();
    builder.Services.ConfigureOptions<ThinkOnErp.API.Swagger.ConfigureSwaggerOptions>();

    builder.Services.AddSwaggerGen(options =>
    {
        options.OperationFilter<ThinkOnErp.API.Swagger.TenantCompanyHeaderOperationFilter>();

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

    // Add global exception handling middleware early so it wraps the request pipeline.
    app.UseMiddleware<ThinkOnErp.API.Middleware.ExceptionHandlingMiddleware>();

    // Configure the HTTP request pipeline.
    //if (app.Environment.IsDevelopment())
    //{
        app.UseSwagger();
        app.UseSwaggerUI();
    //}

    // Disable HTTPS redirection for IP-based access
    // app.UseHttpsRedirection();

    // Add Prometheus metrics endpoint (if enabled)
    if (builder.Configuration.GetValue<bool>("OpenTelemetry:EnablePrometheusExporter", true))
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
    }

    // Add CORS middleware (must be before authentication)
    app.UseCors("AllowFrontend");

    // Add authentication and authorization middleware
    app.UseAuthentication();

    // Resolve the tenant before middleware reads tenant repositories.
    app.UseMiddleware<ThinkOnErp.API.Middleware.SchemaRoutingMiddleware>();

    // Trace the immutable actor and the authoritative effective company.
    app.UseMiddleware<ThinkOnErp.API.Middleware.RequestTracingMiddleware>();

    // Force logout is tenant-user state, so it runs after schema selection.
    app.UseMiddleware<ThinkOnErp.API.Middleware.ForceLogoutMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

    // Auto-provision developer template schema on startup
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Auto-provisioning developer template schema on startup");
            var schemaService = scope.ServiceProvider.GetRequiredService<IOracleSchemaService>();
            await schemaService.ProvisionDeveloperSchemaAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to auto-provision developer template schema on startup");
        }

        // Auto-sync discovered API Endpoints to Oracle DB SYS_API_ENDPOINTS table
        try
        {
            var apiExplorer = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Mvc.ApiExplorer.IApiDescriptionGroupCollectionProvider>();
            var swaggerLoader = scope.ServiceProvider.GetRequiredService<ThinkOnErp.API.Swagger.SwaggerCategoryLoader>();
            await swaggerLoader.SyncDiscoveredEndpointsAsync(apiExplorer);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to auto-sync discovered API Endpoints to SYS_API_ENDPOINTS table");
        }
    }

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
