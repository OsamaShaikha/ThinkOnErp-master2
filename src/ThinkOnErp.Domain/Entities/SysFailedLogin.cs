namespace ThinkOnErp.Domain.Entities;

public class SysFailedLogin
{
    public long Id { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? FailureReason { get; set; }
    public DateTime AttemptDate { get; set; }
}
