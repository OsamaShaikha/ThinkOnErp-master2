namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when database connection or operation fails.
/// </summary>
public class DatabaseConnectionException : DomainException
{
    public string Operation { get; }

    public DatabaseConnectionException(string operation, Exception innerException) 
        : base($"Database operation '{operation}' failed", "DATABASE_CONNECTION_ERROR", innerException)
    {
        Operation = operation;
        AddContext("Operation", operation);
    }

    public DatabaseConnectionException(string operation, string message) 
        : base($"Database operation '{operation}' failed: {message}", "DATABASE_CONNECTION_ERROR")
    {
        Operation = operation;
        AddContext("Operation", operation);
    }
}
