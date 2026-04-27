using System;
using ThinkOnErp.Domain.Entities.Audit;

namespace ThinkOnErp.Examples;

/// <summary>
/// Example usage of ExceptionAuditEvent class for different exception scenarios
/// </summary>
public class ExceptionAuditEventUsageExample
{
    /// <summary>
    /// Example: Critical database timeout exception
    /// </summary>
    public static ExceptionAuditEvent CreateDatabaseTimeoutExample()
    {
        return new ExceptionAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "DATABASE_TIMEOUT",
            EntityType = "Company",
            EntityId = 1,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = DateTime.UtcNow,
            
            // Exception-specific properties
            ExceptionType = "System.Data.SqlClient.SqlException",
            ExceptionMessage = "Timeout expired. The timeout period elapsed prior to completion of the operation or the server is not responding.",
            StackTrace = @"   at System.Data.SqlClient.SqlConnection.OnError(SqlException exception, Boolean breakConnection, Action`1 wrapCloseInAction)
   at ThinkOnErp.Infrastructure.Repositories.CompanyRepository.CreateAsync(Company company) in C:\Source\ThinkOnErp\src\ThinkOnErp.Infrastructure\Repositories\CompanyRepository.cs:line 67
   at ThinkOnErp.Application.Features.Companies.Commands.CreateCompany.CreateCompanyCommandHandler.Handle(CreateCompanyCommand request, CancellationToken cancellationToken)",
            InnerException = "System.ComponentModel.Win32Exception (0x80004005): The wait operation timed out",
            Severity = "Critical"
        };
    }

    /// <summary>
    /// Example: Validation error exception
    /// </summary>
    public static ExceptionAuditEvent CreateValidationErrorExample()
    {
        return new ExceptionAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 456,
            CompanyId = 2,
            BranchId = 3,
            Action = "VALIDATION_ERROR",
            EntityType = "User",
            EntityId = null, // No entity created due to validation failure
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
            Timestamp = DateTime.UtcNow,
            
            // Exception-specific properties
            ExceptionType = "FluentValidation.ValidationException",
            ExceptionMessage = "Validation failed: Email address is required; Password must be at least 8 characters long",
            StackTrace = @"   at ThinkOnErp.Application.Features.Users.Commands.CreateUser.CreateUserCommandValidator.Validate(CreateUserCommand command)
   at ThinkOnErp.Application.Behaviors.ValidationBehavior`2.Handle(TRequest request, RequestHandlerDelegate`1 next, CancellationToken cancellationToken)",
            InnerException = null,
            Severity = "Warning"
        };
    }

    /// <summary>
    /// Example: Authorization exception
    /// </summary>
    public static ExceptionAuditEvent CreateAuthorizationErrorExample()
    {
        return new ExceptionAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 789,
            CompanyId = 3,
            BranchId = 4,
            Action = "AUTHORIZATION_ERROR",
            EntityType = "Role",
            EntityId = 25,
            IpAddress = "172.16.0.1",
            UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36",
            Timestamp = DateTime.UtcNow,
            
            // Exception-specific properties
            ExceptionType = "System.UnauthorizedAccessException",
            ExceptionMessage = "User does not have permission to modify roles in this company",
            StackTrace = @"   at ThinkOnErp.Application.Features.Roles.Commands.UpdateRole.UpdateRoleCommandHandler.Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
   at ThinkOnErp.API.Controllers.RolesController.UpdateRole(long id, UpdateRoleDto updateRoleDto)",
            InnerException = null,
            Severity = "Error"
        };
    }

    /// <summary>
    /// Example: Business rule violation exception
    /// </summary>
    public static ExceptionAuditEvent CreateBusinessRuleViolationExample()
    {
        return new ExceptionAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "COMPANY_ADMIN",
            ActorId = 101,
            CompanyId = 1,
            BranchId = 1,
            Action = "BUSINESS_RULE_VIOLATION",
            EntityType = "Currency",
            EntityId = 5,
            IpAddress = "192.168.1.50",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            Timestamp = DateTime.UtcNow,
            
            // Exception-specific properties
            ExceptionType = "ThinkOnErp.Domain.Exceptions.BusinessRuleException",
            ExceptionMessage = "Cannot delete currency that is currently in use by existing companies",
            StackTrace = @"   at ThinkOnErp.Application.Features.Currencies.Commands.DeleteCurrency.DeleteCurrencyCommandHandler.Handle(DeleteCurrencyCommand request, CancellationToken cancellationToken)
   at ThinkOnErp.API.Controllers.CurrencyController.DeleteCurrency(long id)",
            InnerException = null,
            Severity = "Info" // Business rule violations are informational
        };
    }

    /// <summary>
    /// Example: System exception with aggregate inner exceptions
    /// </summary>
    public static ExceptionAuditEvent CreateAggregateExceptionExample()
    {
        return new ExceptionAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "SYSTEM",
            ActorId = 0, // System-generated
            CompanyId = null, // System-level operation
            BranchId = null,
            Action = "BATCH_PROCESSING_ERROR",
            EntityType = "BatchJob",
            EntityId = 12345,
            IpAddress = null, // System operation
            UserAgent = null,
            Timestamp = DateTime.UtcNow,
            
            // Exception-specific properties
            ExceptionType = "System.AggregateException",
            ExceptionMessage = "One or more errors occurred during batch processing of user imports",
            StackTrace = @"   at ThinkOnErp.Application.Services.BatchProcessingService.ProcessUserImportBatchAsync(BatchImportRequest request)
   at ThinkOnErp.Infrastructure.BackgroundServices.BatchProcessingBackgroundService.ExecuteAsync(CancellationToken stoppingToken)",
            InnerException = @"System.AggregateException: One or more errors occurred. (Timeout expired.) (Validation failed for user record 15.) (Duplicate email address found.)
 ---> System.TimeoutException: Timeout expired.
   at ThinkOnErp.Infrastructure.Services.ExternalValidationService.ValidateUserAsync(User user)
 ---> FluentValidation.ValidationException: Validation failed for user record 15.
   at ThinkOnErp.Application.Validators.UserValidator.Validate(User user)
 ---> ThinkOnErp.Domain.Exceptions.DuplicateEmailException: Duplicate email address found.
   at ThinkOnErp.Infrastructure.Repositories.UserRepository.CreateAsync(User user)",
            Severity = "Error"
        };
    }

    /// <summary>
    /// Example: API integration exception
    /// </summary>
    public static ExceptionAuditEvent CreateApiIntegrationErrorExample()
    {
        return new ExceptionAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 555,
            CompanyId = 4,
            BranchId = 8,
            Action = "EXTERNAL_API_ERROR",
            EntityType = "Integration",
            EntityId = null,
            IpAddress = "203.0.113.1",
            UserAgent = "ThinkOnErp-API-Client/1.0",
            Timestamp = DateTime.UtcNow,
            
            // Exception-specific properties
            ExceptionType = "System.Net.Http.HttpRequestException",
            ExceptionMessage = "The remote server returned an error: (503) Service Unavailable",
            StackTrace = @"   at System.Net.Http.HttpClient.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
   at ThinkOnErp.Infrastructure.Services.ExternalApiService.SyncCompanyDataAsync(long companyId)
   at ThinkOnErp.Application.Features.Companies.Commands.SyncCompanyData.SyncCompanyDataCommandHandler.Handle(SyncCompanyDataCommand request, CancellationToken cancellationToken)",
            InnerException = "System.Net.Sockets.SocketException (0x80004005): No connection could be made because the target machine actively refused it",
            Severity = "Error"
        };
    }

    /// <summary>
    /// Demonstrates how to create an ExceptionAuditEvent from a caught exception
    /// </summary>
    public static ExceptionAuditEvent CreateFromException(Exception exception, string correlationId, long userId, long? companyId, long? branchId, string action, string entityType, long? entityId, string? ipAddress, string? userAgent)
    {
        return new ExceptionAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = userId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Extract exception details
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            ExceptionMessage = exception.Message,
            StackTrace = exception.StackTrace ?? string.Empty,
            InnerException = exception.InnerException?.ToString(),
            Severity = DetermineSeverity(exception)
        };
    }

    /// <summary>
    /// Helper method to determine severity based on exception type
    /// </summary>
    private static string DetermineSeverity(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException => "Critical",
            NullReferenceException => "Critical",
            OutOfMemoryException => "Critical",
            StackOverflowException => "Critical",
            System.Data.SqlClient.SqlException => "Critical",
            UnauthorizedAccessException => "Error",
            InvalidOperationException => "Error",
            NotSupportedException => "Error",
            ArgumentException => "Warning",
            FormatException => "Warning",
            _ when exception.GetType().Name.Contains("Validation") => "Warning",
            _ when exception.GetType().Name.Contains("BusinessRule") => "Info",
            _ => "Error"
        };
    }
}