namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Base exception for all domain-specific exceptions.
/// Provides a foundation for custom exception types with consistent error handling.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Error code for categorizing exceptions
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Additional context data for the exception
    /// </summary>
    public Dictionary<string, object> Context { get; }

    protected DomainException(string message, string errorCode) 
        : base(message)
    {
        ErrorCode = errorCode;
        Context = new Dictionary<string, object>();
    }

    protected DomainException(string message, string errorCode, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Adds context information to the exception
    /// </summary>
    public void AddContext(string key, object value)
    {
        Context[key] = value;
    }
}
