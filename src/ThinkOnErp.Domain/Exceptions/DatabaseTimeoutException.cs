namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when a database operation times out.
/// </summary>
public class DatabaseTimeoutException : DomainException
{
    public string Operation { get; }
    public int TimeoutSeconds { get; }

    public DatabaseTimeoutException(string operation, int timeoutSeconds, Exception innerException) 
        : base($"Database operation '{operation}' timed out after {timeoutSeconds} seconds", "DATABASE_TIMEOUT", innerException)
    {
        Operation = operation;
        TimeoutSeconds = timeoutSeconds;
        AddContext("Operation", operation);
        AddContext("TimeoutSeconds", timeoutSeconds);
    }

    public DatabaseTimeoutException(string operation, int timeoutSeconds) 
        : base($"Database operation '{operation}' timed out after {timeoutSeconds} seconds", "DATABASE_TIMEOUT")
    {
        Operation = operation;
        TimeoutSeconds = timeoutSeconds;
        AddContext("Operation", operation);
        AddContext("TimeoutSeconds", timeoutSeconds);
    }
}
