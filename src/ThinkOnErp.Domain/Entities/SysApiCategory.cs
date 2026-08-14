namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a Swagger API Category / Module stored in the database.
/// </summary>
public class SysApiCategory
{
    public string CategoryCode { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public string? ControllerNames { get; set; }
    public bool IsActive { get; set; } = true;
}
