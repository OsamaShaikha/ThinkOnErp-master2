using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysAuditLog entity.
/// Maps to SYS_AUDIT_LOG table in Oracle database.
/// Configures comprehensive audit logging for system events including data changes,
/// authentication events, permission changes, and exceptions.
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<SysAuditLog>
{
    public void Configure(EntityTypeBuilder<SysAuditLog> builder)
    {
        // Table mapping
        builder.ToTable("SYS_AUDIT_LOG");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_AUDIT_LOG.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.ActorType)
            .HasColumnName("ACTOR_TYPE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ActorId)
            .HasColumnName("ACTOR_ID")
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("ACTION")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.EntityType)
            .HasColumnName("ENTITY_TYPE")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Severity)
            .HasColumnName("SEVERITY")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EventCategory)
            .HasColumnName("EVENT_CATEGORY")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        // Optional properties - Multi-tenant context
        builder.Property(e => e.CompanyId)
            .HasColumnName("COMPANY_ID");

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID");

        builder.Property(e => e.EntityId)
            .HasColumnName("ENTITY_ID");

        // Optional properties - Change tracking
        builder.Property(e => e.OldValue)
            .HasColumnName("OLD_VALUE")
            .HasColumnType("CLOB");

        builder.Property(e => e.NewValue)
            .HasColumnName("NEW_VALUE")
            .HasColumnType("CLOB");

        // Optional properties - Request context
        builder.Property(e => e.IpAddress)
            .HasColumnName("IP_ADDRESS")
            .HasMaxLength(50);

        builder.Property(e => e.UserAgent)
            .HasColumnName("USER_AGENT")
            .HasMaxLength(500);

        builder.Property(e => e.CorrelationId)
            .HasColumnName("CORRELATION_ID")
            .HasMaxLength(100);

        builder.Property(e => e.HttpMethod)
            .HasColumnName("HTTP_METHOD")
            .HasMaxLength(10);

        builder.Property(e => e.EndpointPath)
            .HasColumnName("ENDPOINT_PATH")
            .HasMaxLength(500);

        builder.Property(e => e.RequestPayload)
            .HasColumnName("REQUEST_PAYLOAD")
            .HasColumnType("CLOB");

        builder.Property(e => e.ResponsePayload)
            .HasColumnName("RESPONSE_PAYLOAD")
            .HasColumnType("CLOB");

        builder.Property(e => e.ExecutionTimeMs)
            .HasColumnName("EXECUTION_TIME_MS");

        builder.Property(e => e.StatusCode)
            .HasColumnName("STATUS_CODE");

        // Optional properties - Exception tracking
        builder.Property(e => e.ExceptionType)
            .HasColumnName("EXCEPTION_TYPE")
            .HasMaxLength(200);

        builder.Property(e => e.ExceptionMessage)
            .HasColumnName("EXCEPTION_MESSAGE")
            .HasMaxLength(2000);

        builder.Property(e => e.StackTrace)
            .HasColumnName("STACK_TRACE")
            .HasColumnType("CLOB");

        // Optional properties - Extensibility and legacy compatibility
        builder.Property(e => e.Metadata)
            .HasColumnName("METADATA")
            .HasColumnType("CLOB");

        builder.Property(e => e.BusinessModule)
            .HasColumnName("BUSINESS_MODULE")
            .HasMaxLength(100);

        builder.Property(e => e.DeviceIdentifier)
            .HasColumnName("DEVICE_IDENTIFIER")
            .HasMaxLength(200);

        builder.Property(e => e.ErrorCode)
            .HasColumnName("ERROR_CODE")
            .HasMaxLength(50);

        builder.Property(e => e.BusinessDescription)
            .HasColumnName("BUSINESS_DESCRIPTION")
            .HasMaxLength(1000);

        // Indexes for performance
        builder.HasIndex(e => e.ActorId)
            .HasDatabaseName("IDX_AUDIT_LOG_ACTOR_ID");

        builder.HasIndex(e => e.CompanyId)
            .HasDatabaseName("IDX_AUDIT_LOG_COMPANY_ID");

        builder.HasIndex(e => e.BranchId)
            .HasDatabaseName("IDX_AUDIT_LOG_BRANCH_ID");

        builder.HasIndex(e => e.EntityType)
            .HasDatabaseName("IDX_AUDIT_LOG_ENTITY_TYPE");

        builder.HasIndex(e => e.EntityId)
            .HasDatabaseName("IDX_AUDIT_LOG_ENTITY_ID");

        builder.HasIndex(e => e.CreationDate)
            .HasDatabaseName("IDX_AUDIT_LOG_CREATION_DATE");

        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IDX_AUDIT_LOG_CORRELATION_ID");

        builder.HasIndex(e => e.EventCategory)
            .HasDatabaseName("IDX_AUDIT_LOG_EVENT_CATEGORY");

        builder.HasIndex(e => e.Severity)
            .HasDatabaseName("IDX_AUDIT_LOG_SEVERITY");
    }
}
