namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Provides correlation ID context that flows through async calls.
/// Uses AsyncLocal to maintain correlation ID across async operations within a request.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>
    /// Gets or sets the current correlation ID for the async context.
    /// </summary>
    public static string? Current
    {
        get => _correlationId.Value;
        set => _correlationId.Value = value;
    }

    /// <summary>
    /// Gets the current correlation ID or creates a new one if none exists.
    /// </summary>
    /// <returns>The current or newly created correlation ID</returns>
    public static string GetOrCreate()
    {
        if (string.IsNullOrEmpty(_correlationId.Value))
        {
            _correlationId.Value = Guid.NewGuid().ToString();
        }
        return _correlationId.Value;
    }

    /// <summary>
    /// Creates a new correlation ID and sets it as current.
    /// </summary>
    /// <returns>The newly created correlation ID</returns>
    public static string CreateNew()
    {
        _correlationId.Value = Guid.NewGuid().ToString();
        return _correlationId.Value;
    }

    /// <summary>
    /// Clears the current correlation ID.
    /// </summary>
    public static void Clear()
    {
        _correlationId.Value = null;
    }
}