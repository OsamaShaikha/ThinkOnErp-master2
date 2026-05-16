using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysAuditLogConfiguration : IEntityTypeConfiguration<SysAuditLog>
{
    public void Configure(EntityTypeBuilder<SysAuditLog> builder)
    {
        builder.ToTable("SYS_AUDIT_LOG");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CorrelationId).HasColumnName("CORRELATION_ID").HasMaxLength(100);
        builder.Property(e => e.ActorType).HasColumnName("ACTOR_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("ACTOR_ID").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID");
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(e => e.Action).HasColumnName("ACTION").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityType).HasColumnName("ENTITY_TYPE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("ENTITY_ID");
        builder.Property(e => e.OldValue).HasColumnName("OLD_VALUE").HasColumnType("CLOB");
        builder.Property(e => e.NewValue).HasColumnName("NEW_VALUE").HasColumnType("CLOB");
        builder.Property(e => e.IpAddress).HasColumnName("IP_ADDRESS").HasMaxLength(50);
        builder.Property(e => e.UserAgent).HasColumnName("USER_AGENT").HasMaxLength(500);
        builder.Property(e => e.HttpMethod).HasColumnName("HTTP_METHOD").HasMaxLength(10);
        builder.Property(e => e.EndpointPath).HasColumnName("ENDPOINT_PATH").HasMaxLength(500);
        builder.Property(e => e.RequestPayload).HasColumnName("REQUEST_PAYLOAD").HasColumnType("CLOB");
        builder.Property(e => e.ResponsePayload).HasColumnName("RESPONSE_PAYLOAD").HasColumnType("CLOB");
        builder.Property(e => e.ExecutionTimeMs).HasColumnName("EXECUTION_TIME_MS");
        builder.Property(e => e.StatusCode).HasColumnName("STATUS_CODE");
        builder.Property(e => e.ExceptionType).HasColumnName("EXCEPTION_TYPE").HasMaxLength(200);
        builder.Property(e => e.ExceptionMessage).HasColumnName("EXCEPTION_MESSAGE").HasMaxLength(2000);
        builder.Property(e => e.StackTrace).HasColumnName("STACK_TRACE").HasColumnType("CLOB");
        builder.Property(e => e.Severity).HasColumnName("SEVERITY").HasMaxLength(20);
        builder.Property(e => e.EventCategory).HasColumnName("EVENT_CATEGORY").HasMaxLength(50);
        builder.Property(e => e.Metadata).HasColumnName("METADATA").HasColumnType("CLOB");
        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20);
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.BusinessModule).HasColumnName("BUSINESS_MODULE").HasMaxLength(100);
        builder.Property(e => e.DeviceIdentifier).HasColumnName("DEVICE_IDENTIFIER").HasMaxLength(100);
        builder.Property(e => e.ErrorCode).HasColumnName("ERROR_CODE").HasMaxLength(100);
        builder.Property(e => e.BusinessDescription).HasColumnName("BUSINESS_DESCRIPTION").HasMaxLength(500);
    }
}
