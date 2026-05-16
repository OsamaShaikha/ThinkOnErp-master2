namespace ThinkOnErp.Domain.Entities;

public class SysSlowQuery
{
    public long Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string SqlStatement { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public int RowsAffected { get; set; }
    public string? EndpointPath { get; set; }
    public long? UserId { get; set; }
    public long? CompanyId { get; set; }
    public DateTime CreationDate { get; set; }
}
