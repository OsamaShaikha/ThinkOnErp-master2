namespace ThinkOnErp.Domain.Entities;

public class SysAuditLogArchive
{
    public long Id { get; set; }
    public string ActorType { get; set; } = null!;
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public long? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? HttpMethod { get; set; }
    public string? EndpointPath { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public long? ExecutionTimeMs { get; set; }
    public int? StatusCode { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? Severity { get; set; }
    public string? EventCategory { get; set; }
    public string? Metadata { get; set; }
    public long? SystemId { get; set; }
    public string? DeviceIdentifier { get; set; }
    public string? ErrorCode { get; set; }
    public string? BusinessDescription { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime ArchivedDate { get; set; }
    public long ArchiveBatchId { get; set; }
}
