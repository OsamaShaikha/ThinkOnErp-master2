namespace ThinkOnErp.Application.Common;

/// <summary>
/// Common non-generic interface for uniform API response inspection and localization.
/// </summary>
public interface IApiResponse
{
    bool Success { get; set; }
    int StatusCode { get; set; }
    string Message { get; set; }
    List<string>? Errors { get; set; }
    DateTime Timestamp { get; set; }
    string TraceId { get; set; }
}

public class ApiResponse<T> : IApiResponse
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; }
    public string TraceId { get; set; } = string.Empty;

    public static ApiResponse<T> CreateSuccess(T data, string message = "", int statusCode = 200)
    {
        return new ApiResponse<T>
        {
            Success = true,
            StatusCode = statusCode,
            Message = message,
            Data = data,
            Timestamp = DateTime.UtcNow,
            TraceId = Guid.NewGuid().ToString()
        };
    }

    public static ApiResponse<T> CreateFailure(string message, List<string>? errors = null, int statusCode = 400)
    {
        return new ApiResponse<T>
        {
            Success = false,
            StatusCode = statusCode,
            Message = message,
            Data = default,
            Errors = errors,
            Timestamp = DateTime.UtcNow,
            TraceId = Guid.NewGuid().ToString()
        };
    }
}
