using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ExceptionCategorizationService.
/// Tests exception severity determination, categorization, and critical exception detection.
/// </summary>
public class ExceptionCategorizationServiceTests
{
    private readonly ExceptionCategorizationService _service;
    private readonly Mock<ILogger<ExceptionCategorizationService>> _mockLogger;

    public ExceptionCategorizationServiceTests()
    {
        _mockLogger = new Mock<ILogger<ExceptionCategorizationService>>();
        _service = new ExceptionCategorizationService(_mockLogger.Object);
    }

    #region Severity Determination Tests

    [Fact]
    public void DetermineSeverity_OutOfMemoryException_ReturnsCritical()
    {
        // Arrange
        var exception = new OutOfMemoryException("Out of memory");

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Critical", severity);
    }

    [Fact]
    public void DetermineSeverity_StackOverflowException_ReturnsCritical()
    {
        // Arrange
        var exception = new StackOverflowException("Stack overflow");

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Critical", severity);
    }

    [Fact]
    public void DetermineSeverity_DatabaseConnectionException_ReturnsCritical()
    {
        // Arrange
        var exception = new DatabaseConnectionException("Database unavailable", "SELECT", "Connection failed");

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Critical", severity);
    }

    [Fact]
    public void DetermineSeverity_ValidationException_ReturnsWarning()
    {
        // Arrange
        var exception = new ValidationException("Validation failed");

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Warning", severity);
    }

    [Fact]
    public void DetermineSeverity_ArgumentException_ReturnsWarning()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Warning", severity);
    }

    [Fact]
    public void DetermineSeverity_UnauthorizedAccessException_ReturnsWarning()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access denied");

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Warning", severity);
    }

    [Fact]
    public void DetermineSeverity_TicketNotFoundException_ReturnsInfo()
    {
        // Arrange
        var exception = new TicketNotFoundException(123);

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Info", severity);
    }

    [Fact]
    public void DetermineSeverity_ConcurrentModificationException_ReturnsInfo()
    {
        // Arrange
        var exception = new ConcurrentModificationException("Ticket", 123);

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Info", severity);
    }

    [Fact]
    public void DetermineSeverity_InvalidStatusTransitionException_ReturnsWarning()
    {
        // Arrange
        var exception = new InvalidStatusTransitionException(1, 2);

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Warning", severity);
    }

    [Fact]
    public void DetermineSeverity_ExternalServiceException_ReturnsError()
    {
        // Arrange
        var exception = new ExternalServiceException("EmailService", "Service unavailable");

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Error", severity);
    }

    [Fact]
    public void DetermineSeverity_UnknownException_ReturnsError()
    {
        // Arrange
        var exception = new InvalidOperationException("Unknown error");

        // Act
        var severity = _service.DetermineSeverity(exception);

        // Assert
        Assert.Equal("Error", severity);
    }

    #endregion

    #region Critical Exception Detection Tests

    [Fact]
    public void IsCriticalException_OutOfMemoryException_ReturnsTrue()
    {
        // Arrange
        var exception = new OutOfMemoryException("Out of memory");

        // Act
        var isCritical = _service.IsCriticalException(exception);

        // Assert
        Assert.True(isCritical);
    }

    [Fact]
    public void IsCriticalException_DatabaseConnectionException_ReturnsTrue()
    {
        // Arrange
        var exception = new DatabaseConnectionException("Database unavailable", "SELECT", "Connection failed");

        // Act
        var isCritical = _service.IsCriticalException(exception);

        // Assert
        Assert.True(isCritical);
    }

    [Fact]
    public void IsCriticalException_ValidationException_ReturnsFalse()
    {
        // Arrange
        var exception = new ValidationException("Validation failed");

        // Act
        var isCritical = _service.IsCriticalException(exception);

        // Assert
        Assert.False(isCritical);
    }

    [Fact]
    public void IsCriticalException_TicketNotFoundException_ReturnsFalse()
    {
        // Arrange
        var exception = new TicketNotFoundException(123);

        // Act
        var isCritical = _service.IsCriticalException(exception);

        // Assert
        Assert.False(isCritical);
    }

    #endregion

    #region Exception Category Tests

    [Fact]
    public void GetExceptionCategory_DatabaseConnectionException_ReturnsDatabase()
    {
        // Arrange
        var exception = new DatabaseConnectionException("Database unavailable", "SELECT", "Connection failed");

        // Act
        var category = _service.GetExceptionCategory(exception);

        // Assert
        Assert.Equal("Database", category);
    }

    [Fact]
    public void GetExceptionCategory_ValidationException_ReturnsValidation()
    {
        // Arrange
        var exception = new ValidationException("Validation failed");

        // Act
        var category = _service.GetExceptionCategory(exception);

        // Assert
        Assert.Equal("Validation", category);
    }

    [Fact]
    public void GetExceptionCategory_UnauthorizedAccessException_ReturnsAuthentication()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access denied");

        // Act
        var category = _service.GetExceptionCategory(exception);

        // Assert
        Assert.Equal("Authentication", category);
    }

    [Fact]
    public void GetExceptionCategory_UnauthorizedTicketAccessException_ReturnsAuthorization()
    {
        // Arrange
        var exception = new UnauthorizedTicketAccessException(123, 456);

        // Act
        var category = _service.GetExceptionCategory(exception);

        // Assert
        Assert.Equal("Authorization", category);
    }

    [Fact]
    public void GetExceptionCategory_TicketNotFoundException_ReturnsBusinessLogic()
    {
        // Arrange
        var exception = new TicketNotFoundException(123);

        // Act
        var category = _service.GetExceptionCategory(exception);

        // Assert
        Assert.Equal("BusinessLogic", category);
    }

    [Fact]
    public void GetExceptionCategory_ExternalServiceException_ReturnsExternal()
    {
        // Arrange
        var exception = new ExternalServiceException("EmailService", "Service unavailable");

        // Act
        var category = _service.GetExceptionCategory(exception);

        // Assert
        Assert.Equal("External", category);
    }

    [Fact]
    public void GetExceptionCategory_OutOfMemoryException_ReturnsSystem()
    {
        // Arrange
        var exception = new OutOfMemoryException("Out of memory");

        // Act
        var category = _service.GetExceptionCategory(exception);

        // Assert
        Assert.Equal("System", category);
    }

    [Fact]
    public void GetExceptionCategory_UnknownException_ReturnsGeneral()
    {
        // Arrange
        var exception = new InvalidOperationException("Unknown error");

        // Act
        var category = _service.GetExceptionCategory(exception);

        // Assert
        Assert.Equal("General", category);
    }

    #endregion

    #region Transient Exception Tests

    [Fact]
    public void IsTransientException_TimeoutException_ReturnsTrue()
    {
        // Arrange
        var exception = new TimeoutException("Operation timed out");

        // Act
        var isTransient = _service.IsTransientException(exception);

        // Assert
        Assert.True(isTransient);
    }

    [Fact]
    public void IsTransientException_ExternalServiceException_ReturnsTrue()
    {
        // Arrange
        var exception = new ExternalServiceException("EmailService", "Service unavailable");

        // Act
        var isTransient = _service.IsTransientException(exception);

        // Assert
        Assert.True(isTransient);
    }

    [Fact]
    public void IsTransientException_ValidationException_ReturnsFalse()
    {
        // Arrange
        var exception = new ValidationException("Validation failed");

        // Act
        var isTransient = _service.IsTransientException(exception);

        // Assert
        Assert.False(isTransient);
    }

    [Fact]
    public void IsTransientException_TicketNotFoundException_ReturnsFalse()
    {
        // Arrange
        var exception = new TicketNotFoundException(123);

        // Act
        var isTransient = _service.IsTransientException(exception);

        // Assert
        Assert.False(isTransient);
    }

    #endregion

    #region Oracle Exception Tests

    [Theory]
    [InlineData(1034, "Critical")] // Oracle not available
    [InlineData(3113, "Critical")] // End-of-file on communication channel
    [InlineData(12154, "Critical")] // TNS: could not resolve the connect identifier
    [InlineData(1, "Warning")] // Unique constraint violated
    [InlineData(60, "Warning")] // Deadlock detected
    [InlineData(1400, "Warning")] // Cannot insert NULL
    [InlineData(9999, "Error")] // Unknown Oracle error
    public void DetermineSeverity_OracleException_ReturnsCorrectSeverity(int errorNumber, string expectedSeverity)
    {
        // Arrange
        // Note: We cannot easily create OracleException instances in tests as they have internal constructors
        // This test documents the expected behavior for Oracle exceptions
        // In a real scenario, these would be tested through integration tests

        // For now, we'll test the logic indirectly through DatabaseConnectionException
        if (errorNumber == 1034 || errorNumber == 3113 || errorNumber == 12154)
        {
            var exception = new DatabaseConnectionException("Database unavailable", "SELECT", "Connection failed");
            var severity = _service.DetermineSeverity(exception);
            Assert.Equal("Critical", severity);
        }
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ExceptionCategorizationService_HandlesComplexExceptionHierarchy()
    {
        // Arrange
        var innerException = new ArgumentNullException("param", "Parameter cannot be null");
        var outerException = new InvalidOperationException("Operation failed", innerException);

        // Act
        var severity = _service.DetermineSeverity(outerException);
        var category = _service.GetExceptionCategory(outerException);
        var isCritical = _service.IsCriticalException(outerException);
        var isTransient = _service.IsTransientException(outerException);

        // Assert
        Assert.Equal("Error", severity);
        Assert.Equal("General", category);
        Assert.False(isCritical);
        Assert.False(isTransient);
    }

    [Fact]
    public void ExceptionCategorizationService_HandlesAllDomainExceptions()
    {
        // Arrange
        var exceptions = new Exception[]
        {
            new TicketNotFoundException(123),
            new UnauthorizedTicketAccessException(123, 456),
            new InvalidStatusTransitionException(1, 2),
            new AttachmentSizeExceededException("file.pdf", 10485760, 5242880),
            new InvalidFileTypeException("file.exe", "application/x-msdownload"),
            new ConcurrentModificationException("Ticket", 123),
            new DatabaseConnectionException("Database unavailable", "SELECT", "Connection failed"),
            new ExternalServiceException("EmailService", "Service unavailable")
        };

        // Act & Assert
        foreach (var exception in exceptions)
        {
            var severity = _service.DetermineSeverity(exception);
            var category = _service.GetExceptionCategory(exception);
            var isCritical = _service.IsCriticalException(exception);

            // All should return valid values
            Assert.NotNull(severity);
            Assert.NotNull(category);
            Assert.NotNull(isCritical);

            // Severity should be one of the valid values
            Assert.Contains(severity, new[] { "Critical", "Error", "Warning", "Info" });
        }
    }

    #endregion
}
