namespace ThinkOnErp.Application.DTOs.Auth;

/// <summary>
/// Data transfer object for user login credentials.
/// Used for POST requests to /api/auth/login endpoint.
/// </summary>
public class LoginDto
{
    /// <summary>
    /// Username for authentication (required)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Password for authentication (required)
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
