using ThinkOnErp.Domain.Entities.Audit;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for ExceptionAuditEvent class
/// </summary>
public class ExceptionAuditEventTests
{
    [Fact]
    public void ExceptionAuditEvent_Inherits_From_AuditEvent()
    {
        // Arrange & Act
        var auditEvent = new ExceptionAuditEvent();
        
        // Assert
        Assert.IsAssignableFrom<AuditEvent>(auditEvent);
    }

    [Fact]
    public void ExceptionAuditEvent_Critical_Exception_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var stackTrace = @"   at ThinkOnErp.Application.Features.Users.Commands.CreateUser.CreateUserCommandHandler.Handle(CreateUserCommand request, CancellationToken cancellationToken) in C:\Source\ThinkOnErp\src\ThinkOnErp.Application\Features\Users\Commands\CreateUser\CreateUserCommandHandler.cs:line 45
   at MediatR.Pipeline.RequestPostProcessorBehavior`2.Handle(TRequest request, RequestHandlerDelegate`1 next, CancellationToken cancellationToken)
   at ThinkOnErp.API.Controllers.UsersController.CreateUser(CreateUserDto createUserDto) in C:\Source\ThinkOnErp\src\ThinkOnErp.API\Controllers\UsersController.cs:line 78";
        
        var auditEvent = new ExceptionAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "EXCEPTION",
            EntityType = "User",
            EntityId = 456,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            ExceptionType = "System.ArgumentNullException",
            ExceptionMessage = "Value cannot be null. (Parameter 'email')",
            StackTrace = stackTrace,
            InnerException = null,
            Severity = "Critical"
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("EXCEPTION", auditEvent.Action);
        Assert.Equal("User", auditEvent.EntityType);
        Assert.Equal(456, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal("System.ArgumentNullException", auditEvent.ExceptionType);
        Assert.Equal("Value cannot be null. (Parameter 'email')", auditEvent.ExceptionMessage);
        Assert.Equal(stackTrace, auditEvent.StackTrace);
        Assert.Null(auditEvent.InnerException);
        Assert.Equal("Critical", auditEvent.Severity);
    }

    [Fact]
    public void ExceptionAuditEvent_Error_With_Inner_Exception()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var innerExceptionDetails = "Inner exception: System.Data.SqlClient.SqlException: Timeout expired. The timeout period elapsed prior to completion of the operation or the server is not responding.";
        
        var auditEvent = new ExceptionAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "SYSTEM",
            ActorId = 0,
            CompanyId = 1,
            BranchId = null,
            Action = "DATABASE_ERROR",
            EntityType = "Company",
            EntityId = 1,
            IpAddress = null,
            UserAgent = null,
            Timestamp = timestamp,
            ExceptionType = "ThinkOnErp.Infrastructure.Exceptions.DatabaseTimeoutException",
            ExceptionMessage = "Database operation timed out while creating company record",
            StackTrace = "   at ThinkOnErp.Infrastructure.Repositories.CompanyRepository.CreateAsync(Company company)",
            InnerException = innerExceptionDetails,
            Severity = "Error"
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("SYSTEM", auditEvent.ActorType);
        Assert.Equal(0, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Null(auditEvent.BranchId);
        Assert.Equal("DATABASE_ERROR", auditEvent.Action);
        Assert.Equal("Company", auditEvent.EntityType);
        Assert.Equal(1, auditEvent.EntityId);
        Assert.Null(auditEvent.IpAddress);
        Assert.Null(auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal("ThinkOnErp.Infrastructure.Exceptions.DatabaseTimeoutException", auditEvent.ExceptionType);
        Assert.Equal("Database operation timed out while creating company record", auditEvent.ExceptionMessage);
        Assert.Equal("   at ThinkOnErp.Infrastructure.Repositories.CompanyRepository.CreateAsync(Company company)", auditEvent.StackTrace);
        Assert.Equal(innerExceptionDetails, auditEvent.InnerException);
        Assert.Equal("Error", auditEvent.Severity);
    }

    [Fact]
    public void ExceptionAuditEvent_Warning_Validation_Exception()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new ExceptionAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = 789,
            CompanyId = 2,
            BranchId = 3,
            Action = "VALIDATION_ERROR",
            EntityType = "Branch",
            EntityId = null,
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
            Timestamp = timestamp,
            ExceptionType = "FluentValidation.ValidationException",
            ExceptionMessage = "Validation failed: Branch name must be between 2 and 100 characters",
            StackTrace = "   at ThinkOnErp.Application.Features.Branches.Commands.CreateBranch.CreateBranchCommandValidator.Validate(CreateBranchCommand command)",
            InnerException = null,
            Severity = "Warning"
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(789, auditEvent.ActorId);
        Assert.Equal(2, auditEvent.CompanyId);
        Assert.Equal(3, auditEvent.BranchId);
        Assert.Equal("VALIDATION_ERROR", auditEvent.Action);
        Assert.Equal("Branch", auditEvent.EntityType);
        Assert.Null(auditEvent.EntityId);
        Assert.Equal("10.0.0.1", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal("FluentValidation.ValidationException", auditEvent.ExceptionType);
        Assert.Equal("Validation failed: Branch name must be between 2 and 100 characters", auditEvent.ExceptionMessage);
        Assert.Equal("   at ThinkOnErp.Application.Features.Branches.Commands.CreateBranch.CreateBranchCommandValidator.Validate(CreateBranchCommand command)", auditEvent.StackTrace);
        Assert.Null(auditEvent.InnerException);
        Assert.Equal("Warning", auditEvent.Severity);
    }

    [Fact]
    public void ExceptionAuditEvent_Info_Level_Exception()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new ExceptionAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = 456,
            CompanyId = 1,
            BranchId = 1,
            Action = "BUSINESS_RULE_VIOLATION",
            EntityType = "Currency",
            EntityId = 5,
            IpAddress = "192.168.1.50",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            Timestamp = timestamp,
            ExceptionType = "ThinkOnErp.Domain.Exceptions.BusinessRuleException",
            ExceptionMessage = "Cannot delete currency that is in use by existing companies",
            StackTrace = "   at ThinkOnErp.Application.Features.Currencies.Commands.DeleteCurrency.DeleteCurrencyCommandHandler.Handle(DeleteCurrencyCommand request, CancellationToken cancellationToken)",
            InnerException = null,
            Severity = "Info"
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(456, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(1, auditEvent.BranchId);
        Assert.Equal("BUSINESS_RULE_VIOLATION", auditEvent.Action);
        Assert.Equal("Currency", auditEvent.EntityType);
        Assert.Equal(5, auditEvent.EntityId);
        Assert.Equal("192.168.1.50", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal("ThinkOnErp.Domain.Exceptions.BusinessRuleException", auditEvent.ExceptionType);
        Assert.Equal("Cannot delete currency that is in use by existing companies", auditEvent.ExceptionMessage);
        Assert.Equal("   at ThinkOnErp.Application.Features.Currencies.Commands.DeleteCurrency.DeleteCurrencyCommandHandler.Handle(DeleteCurrencyCommand request, CancellationToken cancellationToken)", auditEvent.StackTrace);
        Assert.Null(auditEvent.InnerException);
        Assert.Equal("Info", auditEvent.Severity);
    }

    [Fact]
    public void ExceptionAuditEvent_Default_Severity_Is_Error()
    {
        // Arrange & Act
        var auditEvent = new ExceptionAuditEvent();

        // Assert
        Assert.Equal("Error", auditEvent.Severity);
    }

    [Fact]
    public void ExceptionAuditEvent_Can_Handle_All_Severity_Levels()
    {
        // Arrange
        var severityLevels = new[] { "Critical", "Error", "Warning", "Info" };

        foreach (var severity in severityLevels)
        {
            // Act
            var auditEvent = new ExceptionAuditEvent
            {
                ExceptionType = "System.Exception",
                ExceptionMessage = $"Test exception for {severity} severity",
                StackTrace = "   at Test.Method()",
                Severity = severity
            };

            // Assert
            Assert.Equal("System.Exception", auditEvent.ExceptionType);
            Assert.Equal($"Test exception for {severity} severity", auditEvent.ExceptionMessage);
            Assert.Equal("   at Test.Method()", auditEvent.StackTrace);
            Assert.Equal(severity, auditEvent.Severity);
        }
    }

    [Fact]
    public void ExceptionAuditEvent_Can_Handle_Null_Optional_Properties()
    {
        // Arrange & Act
        var auditEvent = new ExceptionAuditEvent
        {
            ExceptionType = "System.NullReferenceException",
            ExceptionMessage = "Object reference not set to an instance of an object",
            StackTrace = "   at ThinkOnErp.Test.Method()",
            InnerException = null,
            Severity = "Error"
        };

        // Assert
        Assert.Equal("System.NullReferenceException", auditEvent.ExceptionType);
        Assert.Equal("Object reference not set to an instance of an object", auditEvent.ExceptionMessage);
        Assert.Equal("   at ThinkOnErp.Test.Method()", auditEvent.StackTrace);
        Assert.Null(auditEvent.InnerException);
        Assert.Equal("Error", auditEvent.Severity);
    }

    [Fact]
    public void ExceptionAuditEvent_Can_Handle_Empty_String_Properties()
    {
        // Arrange & Act
        var auditEvent = new ExceptionAuditEvent
        {
            ExceptionType = string.Empty,
            ExceptionMessage = string.Empty,
            StackTrace = string.Empty,
            InnerException = string.Empty,
            Severity = string.Empty
        };

        // Assert
        Assert.Equal(string.Empty, auditEvent.ExceptionType);
        Assert.Equal(string.Empty, auditEvent.ExceptionMessage);
        Assert.Equal(string.Empty, auditEvent.StackTrace);
        Assert.Equal(string.Empty, auditEvent.InnerException);
        Assert.Equal(string.Empty, auditEvent.Severity);
    }

    [Fact]
    public void ExceptionAuditEvent_Can_Handle_Long_Stack_Traces()
    {
        // Arrange
        var longStackTrace = @"   at ThinkOnErp.Application.Features.Users.Commands.CreateUser.CreateUserCommandHandler.Handle(CreateUserCommand request, CancellationToken cancellationToken) in C:\Source\ThinkOnErp\src\ThinkOnErp.Application\Features\Users\Commands\CreateUser\CreateUserCommandHandler.cs:line 45
   at MediatR.Pipeline.RequestPostProcessorBehavior`2.Handle(TRequest request, RequestHandlerDelegate`1 next, CancellationToken cancellationToken)
   at MediatR.Pipeline.RequestPreProcessorBehavior`2.Handle(TRequest request, RequestHandlerDelegate`1 next, CancellationToken cancellationToken)
   at ThinkOnErp.Application.Behaviors.AuditLoggingBehavior`2.Handle(TRequest request, RequestHandlerDelegate`1 next, CancellationToken cancellationToken) in C:\Source\ThinkOnErp\src\ThinkOnErp.Application\Behaviors\AuditLoggingBehavior.cs:line 67
   at ThinkOnErp.Application.Behaviors.ValidationBehavior`2.Handle(TRequest request, RequestHandlerDelegate`1 next, CancellationToken cancellationToken) in C:\Source\ThinkOnErp\src\ThinkOnErp.Application\Behaviors\ValidationBehavior.cs:line 23
   at MediatR.MediatorImpl.Send[TResponse](IRequest`1 request, CancellationToken cancellationToken)
   at ThinkOnErp.API.Controllers.UsersController.CreateUser(CreateUserDto createUserDto) in C:\Source\ThinkOnErp\src\ThinkOnErp.API\Controllers\UsersController.cs:line 78
   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.TaskOfIActionResultExecutor.Execute(IActionResultTypeMapper mapper, ObjectMethodExecutor executor, Object controller, Object[] arguments)
   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeActionMethodAsync()
   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)
   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeNextActionFilterAsync()";

        // Act
        var auditEvent = new ExceptionAuditEvent
        {
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "An error occurred while processing the request",
            StackTrace = longStackTrace,
            Severity = "Error"
        };

        // Assert
        Assert.Equal("System.InvalidOperationException", auditEvent.ExceptionType);
        Assert.Equal("An error occurred while processing the request", auditEvent.ExceptionMessage);
        Assert.Equal(longStackTrace, auditEvent.StackTrace);
        Assert.Equal("Error", auditEvent.Severity);
        Assert.True(auditEvent.StackTrace.Length > 1000); // Verify it's a long stack trace
    }

    [Fact]
    public void ExceptionAuditEvent_Can_Handle_Complex_Inner_Exception()
    {
        // Arrange
        var complexInnerException = @"System.Data.SqlClient.SqlException (0x80131904): Timeout expired. The timeout period elapsed prior to completion of the operation or the server is not responding.
 ---> System.ComponentModel.Win32Exception (0x80004005): The wait operation timed out
   at System.Data.SqlClient.SqlConnection.OnError(SqlException exception, Boolean breakConnection, Action`1 wrapCloseInAction)
   at System.Data.SqlClient.SqlInternalConnection.OnError(SqlException exception, Boolean breakConnection, Action`1 wrapCloseInAction)
   at System.Data.SqlClient.TdsParser.ThrowExceptionAndWarning(TdsParserStateObject stateObj, Boolean callerHasConnectionLock, Boolean asyncClose)
   at System.Data.SqlClient.TdsParser.TryRun(RunBehavior runBehavior, SqlCommand cmdHandler, SqlDataReader dataStream, BulkCopySimpleResultSet bulkCopyHandler, TdsParserStateObject stateObj, Boolean& dataReady)";

        // Act
        var auditEvent = new ExceptionAuditEvent
        {
            ExceptionType = "ThinkOnErp.Infrastructure.Exceptions.DatabaseException",
            ExceptionMessage = "Database operation failed",
            StackTrace = "   at ThinkOnErp.Infrastructure.Repositories.BaseRepository.ExecuteAsync(String sql, Object parameters)",
            InnerException = complexInnerException,
            Severity = "Critical"
        };

        // Assert
        Assert.Equal("ThinkOnErp.Infrastructure.Exceptions.DatabaseException", auditEvent.ExceptionType);
        Assert.Equal("Database operation failed", auditEvent.ExceptionMessage);
        Assert.Equal("   at ThinkOnErp.Infrastructure.Repositories.BaseRepository.ExecuteAsync(String sql, Object parameters)", auditEvent.StackTrace);
        Assert.Equal(complexInnerException, auditEvent.InnerException);
        Assert.Equal("Critical", auditEvent.Severity);
    }

    [Fact]
    public void ExceptionAuditEvent_Inherits_All_Base_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new ExceptionAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 999,
            CompanyId = 5,
            BranchId = 10,
            Action = "API_ERROR",
            EntityType = "Role",
            EntityId = 25,
            IpAddress = "172.16.0.1",
            UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36",
            Timestamp = timestamp,
            ExceptionType = "System.UnauthorizedAccessException",
            ExceptionMessage = "Access denied to role management",
            StackTrace = "   at ThinkOnErp.API.Controllers.RolesController.CreateRole(CreateRoleDto dto)",
            Severity = "Warning"
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(999, auditEvent.ActorId);
        Assert.Equal(5, auditEvent.CompanyId);
        Assert.Equal(10, auditEvent.BranchId);
        Assert.Equal("API_ERROR", auditEvent.Action);
        Assert.Equal("Role", auditEvent.EntityType);
        Assert.Equal(25, auditEvent.EntityId);
        Assert.Equal("172.16.0.1", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal("System.UnauthorizedAccessException", auditEvent.ExceptionType);
        Assert.Equal("Access denied to role management", auditEvent.ExceptionMessage);
        Assert.Equal("   at ThinkOnErp.API.Controllers.RolesController.CreateRole(CreateRoleDto dto)", auditEvent.StackTrace);
        Assert.Equal("Warning", auditEvent.Severity);
    }

    [Theory]
    [InlineData("Critical")]
    [InlineData("Error")]
    [InlineData("Warning")]
    [InlineData("Info")]
    public void ExceptionAuditEvent_Supports_All_Required_Severity_Levels(string severity)
    {
        // Arrange & Act
        var auditEvent = new ExceptionAuditEvent
        {
            ExceptionType = "System.Exception",
            ExceptionMessage = "Test exception",
            StackTrace = "   at Test.Method()",
            Severity = severity
        };

        // Assert
        Assert.Equal(severity, auditEvent.Severity);
    }

    [Fact]
    public void ExceptionAuditEvent_Can_Capture_Aggregate_Exception_Details()
    {
        // Arrange
        var aggregateExceptionDetails = @"System.AggregateException: One or more errors occurred. (Timeout expired.) (Connection failed.)
 ---> System.TimeoutException: Timeout expired.
   at ThinkOnErp.Infrastructure.Services.ExternalApiService.CallAsync()
 ---> System.Net.Http.HttpRequestException: Connection failed.
   at System.Net.Http.HttpClient.SendAsync(HttpRequestMessage request)";

        // Act
        var auditEvent = new ExceptionAuditEvent
        {
            ExceptionType = "System.AggregateException",
            ExceptionMessage = "Multiple operations failed during batch processing",
            StackTrace = "   at ThinkOnErp.Application.Services.BatchProcessingService.ProcessBatchAsync()",
            InnerException = aggregateExceptionDetails,
            Severity = "Error"
        };

        // Assert
        Assert.Equal("System.AggregateException", auditEvent.ExceptionType);
        Assert.Equal("Multiple operations failed during batch processing", auditEvent.ExceptionMessage);
        Assert.Equal("   at ThinkOnErp.Application.Services.BatchProcessingService.ProcessBatchAsync()", auditEvent.StackTrace);
        Assert.Equal(aggregateExceptionDetails, auditEvent.InnerException);
        Assert.Equal("Error", auditEvent.Severity);
    }
}