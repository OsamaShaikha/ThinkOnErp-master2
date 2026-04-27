namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when an external service call fails.
/// </summary>
public class ExternalServiceException : DomainException
{
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, string message) 
        : base($"External service '{serviceName}' failed: {message}", "EXTERNAL_SERVICE_ERROR")
    {
        ServiceName = serviceName;
        AddContext("ServiceName", serviceName);
    }

    public ExternalServiceException(string serviceName, string message, Exception innerException) 
        : base($"External service '{serviceName}' failed: {message}", "EXTERNAL_SERVICE_ERROR", innerException)
    {
        ServiceName = serviceName;
        AddContext("ServiceName", serviceName);
    }
}
