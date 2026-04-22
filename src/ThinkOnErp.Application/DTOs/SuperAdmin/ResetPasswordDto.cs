namespace ThinkOnErp.Application.DTOs.SuperAdmin;

/// <summary>
/// Data transfer object for password reset response
/// Contains the temporary password that should be sent to the user
/// </summary>
public class ResetPasswordDto
{
    public string TemporaryPassword { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
