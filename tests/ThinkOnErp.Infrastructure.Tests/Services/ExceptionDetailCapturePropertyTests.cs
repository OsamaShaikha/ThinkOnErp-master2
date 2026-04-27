using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Resilience;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for exception detail capture.
/// Validates that all exceptions are logged with complete details including exception type,
/// message, stack trace, inner exceptions, correlation ID, and request context.
/// 
/// **Validates: Requirements 7.1, 7.2, 7.3, 7.6**
/// 
/// Property 16: Exception Detail Capture
/// FOR ALL exceptions that occur, the audit log SHALL contain the exception type, message, 
/// full stack trace, inner exceptions, correlation ID, and request context.
/// </summary>
public class ExceptionDetailCapturePropertyTests : IDisposable
{
    private const int MinIterations = 100;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly Mock<ILogger<AuditLogger>> _mockAuditLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuditLogger _auditLogger;
    private readonly List<SysAuditLog> _capturedAuditLogs;

    public ExceptionDetailCapturePropertyTests()
    {
        _mockRepository = new Mock<IAuditRepository>();
        _mockDataMasker = new Mock<ISensitiveDataMasker>();
        _mockLegacyService = new Mock<ILegacyAuditService>();
        _mockAuditLogger = new Mock<ILogger<AuditLogger>>();
        _capturedAuditLogs = new List<SysAuditLog>();

        // Setup mock repository to capture audit logs
        _mockRepository
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<SysAuditLog>, CancellationToken>((logs, _) =>
            {
                _capturedAuditLogs.AddRange(logs);
            })
            .ReturnsAsync((IEnumerable<SysAuditLog> logs, CancellationToken _) => logs.Count());

        _mockRepository
            .Setup(r => r.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup data masker to pass through data unchanged for testing
        _mockDataMasker
            .Setup(m => m.MaskSensitiveFields(It.IsAny<string?>()))
            .Returns<string?>(s => s);

        _mockDataMasker
            .Setup(m => m.TruncateIfNeeded(It.IsAny<string?>()))
            .Returns<string?>(s => s);

        // Setup legacy service with default implementations
        _mockLegacyService
            .Setup(l => l.DetermineBusinessModuleAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("System");

        _mockLegacyService
            .Setup(l => l.ExtractDeviceIdentifierAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("TestDevice");

        _mockLegacyService
            .Setup(l => l.GenerateErrorCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("ERR_001");

        _mockLegacyService
            .Setup(l => l.GenerateBusinessDescriptionAsync(It.IsAny<ThinkOnErp.Domain.Models.AuditLogEntry>()))
            .ReturnsAsync("Exception occurred");

        // Create service collection for dependency injection
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Object);
        services.AddSingleton(_mockDataMasker.Object);
        services.AddSingleton(_mockLegacyService.Object);
        services.AddSingleton(_mockAuditLogger.Object);

        var serviceProvider = services.BuildServiceProvider();
        _serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Configure audit logging options
        var auditOptions = Options.Create(new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = 50,
            BatchWindowMs = 100,
            MaxQueueSize = 10000,
            EnableCircuitBreaker = false
        });

        var circuitBreakerRegistry = new CircuitBreakerRegistry(
            new LoggerFactory(),
            failureThreshold: 5,
            openDuration: TimeSpan.FromSeconds(60));

        _auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _mockAuditLogger.Object,
            auditOptions,
            circuitBreakerRegistry);

        // Start the audit logger background service
        _auditLogger.StartAsync(CancellationToken.None).Wait();
    }

    /// <summary>
    /// **Validates: Requirements 7.1, 7.2, 7.3, 7.6**
    /// 
    /// Property 16: Exception Detail Capture
    /// 
    /// FOR ALL exceptions that occur, the audit log SHALL contain the exception type, message, 
    /// full stack trace, inner exceptions, correlation ID, and request context.
    /// 
    /// This property verifies that:
    /// 1. All exceptions are captured in the audit log
    /// 2. Exception type is recorded
    /// 3. Exception message is recorded
    /// 4. Full stack trace is recorded
    /// 5. Inner exceptions are captured (when present)
    /// 6. Correlation ID is recorded for request tracing
    /// 7. Request context (user ID, company ID, IP address, user agent) is recorded
    /// 8. Severity classification is recorded
    /// 9. EventCategory is set to "Exception"
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllExceptions_AllDetailsAreCaptured(ExceptionEventData exceptionData)
    {
        // Clear any previously captured audit logs
        _capturedAuditLogs.Clear();

        // Create exception audit event
        var exceptionEvent = new ExceptionAuditEvent
        {
            CorrelationId = exceptionData.CorrelationId,
            ActorType = exceptionData.ActorType,
            ActorId = exceptionData.ActorId,
            CompanyId = exceptionData.CompanyId,
            BranchId = exceptionData.BranchId,
            Action = "EXCEPTION",
            EntityType = exceptionData.EntityType,
            EntityId = exceptionData.EntityId,
            IpAddress = exceptionData.IpAddress,
            UserAgent = exceptionData.UserAgent,
            Timestamp = exceptionData.Timestamp,
            ExceptionType = exceptionData.ExceptionType,
            ExceptionMessage = exceptionData.ExceptionMessage,
            StackTrace = exceptionData.StackTrace,
            InnerException = exceptionData.InnerException,
            Severity = exceptionData.Severity
        };

        // Act: Log the exception event
        var logTask = _auditLogger.LogExceptionAsync(exceptionEvent);
        logTask.Wait();

        // Wait for background processing to complete (with timeout)
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_capturedAuditLogs.Count == 0 && DateTime.UtcNow - startTime < timeout)
        {
            Thread.Sleep(50);
        }

        // Property 1: An audit log entry must exist
        var auditLogExists = _capturedAuditLogs.Count > 0;

        if (!auditLogExists)
        {
            return false
                .Label("Audit log entry exists: false")
                .Label($"Expected at least 1 audit log entry, but found {_capturedAuditLogs.Count}");
        }

        var capturedLog = _capturedAuditLogs.First();

        // Property 2: Exception type must be recorded
        var exceptionTypePresent = !string.IsNullOrEmpty(capturedLog.ExceptionType) &&
                                   capturedLog.ExceptionType == exceptionData.ExceptionType;

        // Property 3: Exception message must be recorded
        var exceptionMessagePresent = !string.IsNullOrEmpty(capturedLog.ExceptionMessage) &&
                                      capturedLog.ExceptionMessage == exceptionData.ExceptionMessage;

        // Property 4: Full stack trace must be recorded
        var stackTracePresent = !string.IsNullOrEmpty(capturedLog.StackTrace) &&
                               capturedLog.StackTrace == exceptionData.StackTrace;

        // Property 5: Inner exceptions must be captured (when present)
        var innerExceptionCorrect = exceptionData.InnerException == null
            ? string.IsNullOrEmpty(capturedLog.Metadata) || !ContainsInnerException(capturedLog.Metadata)
            : ContainsInnerException(capturedLog.Metadata, exceptionData.InnerException);

        // Property 6: Correlation ID must be recorded
        var correlationIdPresent = !string.IsNullOrEmpty(capturedLog.CorrelationId) &&
                                   capturedLog.CorrelationId == exceptionData.CorrelationId;

        // Property 7: Request context must be recorded
        var actorIdPresent = capturedLog.ActorId == exceptionData.ActorId;
        var companyIdPresent = capturedLog.CompanyId == exceptionData.CompanyId;
        var ipAddressPresent = exceptionData.IpAddress == null ||
                              (capturedLog.IpAddress == exceptionData.IpAddress);
        var userAgentPresent = exceptionData.UserAgent == null ||
                              (capturedLog.UserAgent == exceptionData.UserAgent);

        // Property 8: Severity classification must be recorded
        var severityPresent = !string.IsNullOrEmpty(capturedLog.Severity) &&
                             capturedLog.Severity == exceptionData.Severity;

        // Property 9: EventCategory must be set to "Exception"
        var eventCategoryCorrect = capturedLog.EventCategory == "Exception";

        // Property 10: Timestamp must be present
        var timestampPresent = capturedLog.CreationDate != default;

        // Combine all properties
        var result = auditLogExists
            && exceptionTypePresent
            && exceptionMessagePresent
            && stackTracePresent
            && innerExceptionCorrect
            && correlationIdPresent
            && actorIdPresent
            && companyIdPresent
            && ipAddressPresent
            && userAgentPresent
            && severityPresent
            && eventCategoryCorrect
            && timestampPresent;

        return result
            .Label($"Audit log exists: {auditLogExists}")
            .Label($"Exception type present: {exceptionTypePresent} (expected: {exceptionData.ExceptionType}, actual: {capturedLog.ExceptionType})")
            .Label($"Exception message present: {exceptionMessagePresent}")
            .Label($"Stack trace present: {stackTracePresent}")
            .Label($"Inner exception correct: {innerExceptionCorrect}")
            .Label($"Correlation ID present: {correlationIdPresent}")
            .Label($"Actor ID present: {actorIdPresent} (expected: {exceptionData.ActorId}, actual: {capturedLog.ActorId})")
            .Label($"Company ID present: {companyIdPresent}")
            .Label($"IP address present: {ipAddressPresent}")
            .Label($"User agent present: {userAgentPresent}")
            .Label($"Severity present: {severityPresent} (expected: {exceptionData.Severity}, actual: {capturedLog.Severity})")
            .Label($"Event category correct: {eventCategoryCorrect}")
            .Label($"Timestamp present: {timestampPresent}");
    }

    /// <summary>
    /// Checks if the metadata contains inner exception information.
    /// </summary>
    private bool ContainsInnerException(string? metadata, string? expectedInnerException = null)
    {
        if (string.IsNullOrEmpty(metadata))
            return false;

        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(metadata);
            var root = doc.RootElement;

            if (!root.TryGetProperty("InnerException", out var innerExProp))
                return false;

            if (expectedInnerException == null)
                return true;

            var innerExValue = innerExProp.GetString();
            return !string.IsNullOrEmpty(innerExValue) && innerExValue.Contains(expectedInnerException);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        // Stop the audit logger background service
        _auditLogger.StopAsync(CancellationToken.None).Wait();
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary exception events for property testing.
        /// Covers various exception types, severities, and scenarios including nested exceptions.
        /// </summary>
        public static Arbitrary<ExceptionEventData> ExceptionEventData()
        {
            var eventGenerator =
                from exceptionType in Gen.Elements(
                    "System.NullReferenceException",
                    "System.ArgumentNullException",
                    "System.InvalidOperationException",
                    "System.UnauthorizedAccessException",
                    "System.TimeoutException",
                    "Oracle.ManagedDataAccess.Client.OracleException",
                    "FluentValidation.ValidationException",
                    "System.DivideByZeroException",
                    "System.IO.IOException",
                    "System.Net.Http.HttpRequestException")
                from severity in Gen.Elements("Critical", "Error", "Warning", "Info")
                from actorType in Gen.Elements("SUPER_ADMIN", "COMPANY_ADMIN", "USER", "SYSTEM")
                from actorId in Gen.Choose(1, 10000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long?)i)
                from branchId in Gen.Choose(1, 500).Select(i => (long?)i)
                from entityType in Gen.Elements("User", "Company", "Branch", "Role", "Permission", "Order", "Invoice")
                from entityId in Gen.Choose(1, 10000).Select(i => (long?)i)
                from correlationId in Gen.Elements(
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString())
                from ipAddress in Gen.Elements(
                    "192.168.1.100",
                    "10.0.0.50",
                    "172.16.0.25",
                    "203.0.113.45",
                    null)
                from userAgent in Gen.Elements(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
                    "PostmanRuntime/7.29.2",
                    null)
                from hasInnerException in Gen.Elements(true, false)
                select CreateExceptionEvent(
                    exceptionType,
                    severity,
                    actorType,
                    actorId,
                    companyId,
                    branchId,
                    entityType,
                    entityId,
                    correlationId,
                    ipAddress,
                    userAgent,
                    hasInnerException);

            return Arb.From(eventGenerator);
        }

        private static ExceptionEventData CreateExceptionEvent(
            string exceptionType,
            string severity,
            string actorType,
            long actorId,
            long? companyId,
            long? branchId,
            string entityType,
            long? entityId,
            string correlationId,
            string? ipAddress,
            string? userAgent,
            bool hasInnerException)
        {
            var exceptionMessage = GenerateExceptionMessage(exceptionType);
            var stackTrace = GenerateStackTrace(exceptionType);
            var innerException = hasInnerException ? GenerateInnerException() : null;

            return new ExceptionEventData
            {
                ExceptionType = exceptionType,
                ExceptionMessage = exceptionMessage,
                StackTrace = stackTrace,
                InnerException = innerException,
                Severity = severity,
                ActorType = actorType,
                ActorId = actorId,
                CompanyId = companyId,
                BranchId = branchId,
                EntityType = entityType,
                EntityId = entityId,
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow
            };
        }

        private static string GenerateExceptionMessage(string exceptionType)
        {
            var messages = new Dictionary<string, string[]>
            {
                ["System.NullReferenceException"] = new[]
                {
                    "Object reference not set to an instance of an object.",
                    "Attempted to access a null object reference.",
                    "Value cannot be null."
                },
                ["System.ArgumentNullException"] = new[]
                {
                    "Value cannot be null. (Parameter 'userId')",
                    "Value cannot be null. (Parameter 'companyId')",
                    "Value cannot be null. (Parameter 'request')"
                },
                ["System.InvalidOperationException"] = new[]
                {
                    "Operation is not valid due to the current state of the object.",
                    "Sequence contains no elements.",
                    "Collection was modified; enumeration operation may not execute."
                },
                ["System.UnauthorizedAccessException"] = new[]
                {
                    "Access to the path is denied.",
                    "User does not have permission to perform this action.",
                    "Unauthorized access to resource."
                },
                ["System.TimeoutException"] = new[]
                {
                    "The operation has timed out.",
                    "Database connection timeout.",
                    "Request timeout after 30 seconds."
                },
                ["Oracle.ManagedDataAccess.Client.OracleException"] = new[]
                {
                    "ORA-00001: unique constraint violated",
                    "ORA-01017: invalid username/password; logon denied",
                    "ORA-12154: TNS:could not resolve the connect identifier specified"
                },
                ["FluentValidation.ValidationException"] = new[]
                {
                    "Validation failed: Email is required.",
                    "Validation failed: Password must be at least 8 characters.",
                    "Validation failed: Invalid date format."
                },
                ["System.DivideByZeroException"] = new[]
                {
                    "Attempted to divide by zero.",
                    "Division by zero is not allowed."
                },
                ["System.IO.IOException"] = new[]
                {
                    "The process cannot access the file because it is being used by another process.",
                    "Could not find a part of the path.",
                    "Disk full."
                },
                ["System.Net.Http.HttpRequestException"] = new[]
                {
                    "An error occurred while sending the request.",
                    "No connection could be made because the target machine actively refused it.",
                    "The remote server returned an error: (500) Internal Server Error."
                }
            };

            if (messages.TryGetValue(exceptionType, out var typeMessages))
            {
                return typeMessages[new System.Random().Next(typeMessages.Length)];
            }

            return $"An error occurred in {exceptionType}";
        }

        private static string GenerateStackTrace(string exceptionType)
        {
            var stackTraces = new[]
            {
                $"   at ThinkOnErp.Application.Features.Users.Commands.CreateUser.CreateUserCommandHandler.Handle(CreateUserCommand request, CancellationToken cancellationToken) in CreateUserCommandHandler.cs:line 45\n" +
                $"   at MediatR.Pipeline.RequestExceptionProcessorBehavior`2.Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate`1 next) in RequestExceptionProcessorBehavior.cs:line 32\n" +
                $"   at ThinkOnErp.Application.Behaviors.AuditLoggingBehavior`2.Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate`1 next) in AuditLoggingBehavior.cs:line 67",
                
                $"   at ThinkOnErp.Infrastructure.Repositories.UserRepository.CreateAsync(SysUser user, CancellationToken cancellationToken) in UserRepository.cs:line 123\n" +
                $"   at ThinkOnErp.Application.Features.Users.Commands.CreateUser.CreateUserCommandHandler.Handle(CreateUserCommand request, CancellationToken cancellationToken) in CreateUserCommandHandler.cs:line 52\n" +
                $"   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeActionMethodAsync() in ControllerActionInvoker.cs:line 234",
                
                $"   at Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteNonQuery() in OracleCommand.cs:line 456\n" +
                $"   at ThinkOnErp.Infrastructure.Data.OracleDbContext.SaveChangesAsync(CancellationToken cancellationToken) in OracleDbContext.cs:line 89\n" +
                $"   at ThinkOnErp.Infrastructure.Repositories.BaseRepository`1.SaveAsync(CancellationToken cancellationToken) in BaseRepository.cs:line 178",
                
                $"   at ThinkOnErp.API.Controllers.UsersController.CreateUser(CreateUserDto dto) in UsersController.cs:line 78\n" +
                $"   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.TaskOfIActionResultExecutor.Execute(IActionResultTypeMapper mapper, ObjectMethodExecutor executor, Object controller, Object[] arguments) in ActionMethodExecutor.cs:line 123\n" +
                $"   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeActionMethodAsync() in ControllerActionInvoker.cs:line 234"
            };

            return stackTraces[new System.Random().Next(stackTraces.Length)];
        }

        private static string GenerateInnerException()
        {
            var innerExceptions = new[]
            {
                "System.Data.Common.DbException: A network-related or instance-specific error occurred while establishing a connection to the database.",
                "System.ArgumentException: Invalid parameter value provided.",
                "System.InvalidCastException: Unable to cast object of type 'System.String' to type 'System.Int32'.",
                "System.FormatException: Input string was not in a correct format.",
                "System.IO.FileNotFoundException: Could not find file 'config.json'.",
                "System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it."
            };

            return innerExceptions[new System.Random().Next(innerExceptions.Length)];
        }
    }
}

/// <summary>
/// Represents exception event data for property-based testing.
/// </summary>
public class ExceptionEventData
{
    public string ExceptionType { get; set; } = string.Empty;
    public string ExceptionMessage { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string? InnerException { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
}
