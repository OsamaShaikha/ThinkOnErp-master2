namespace ThinkOnErp.Application.DTOs.Accounting;

public sealed class GlAccountStatusDto
{
    public string AccountCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

