namespace ThinkOnErp.Application.DTOs.User;

/// <summary>
/// Data transfer object for changing a user's password.
/// Used for PUT requests to /api/users/{id}/change-password endpoint.
/// </summary>
public class ChangePasswordDto
{
    /// <summary>
    /// Current password for verification (required)
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// New password (will be hashed using SHA-256) (required)
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// Confirmation of new password (must match NewPassword) (required)
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}
