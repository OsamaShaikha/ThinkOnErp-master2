using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysSecurityThreatConfiguration : IEntityTypeConfiguration<SysSecurityThreat>
{
    public void Configure(EntityTypeBuilder<SysSecurityThreat> builder)
    {
        builder.ToTable("SYS_SECURITY_THREATS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.ThreatType).HasColumnName("THREAT_TYPE").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Severity).HasColumnName("SEVERITY").HasMaxLength(50).IsRequired();
        builder.Property(e => e.IpAddress).HasColumnName("IP_ADDRESS").HasMaxLength(50);
        builder.Property(e => e.UserId).HasColumnName("USER_ID");
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID");
        builder.Property(e => e.Description).HasColumnName("DESCRIPTION").HasColumnType("CLOB").IsRequired();
        builder.Property(e => e.DetectionDate).HasColumnName("DETECTION_DATE").IsRequired();
        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Metadata).HasColumnName("METADATA").HasColumnType("CLOB");
        builder.Property(e => e.AcknowledgedBy).HasColumnName("ACKNOWLEDGED_BY");
        builder.Property(e => e.AcknowledgedDate).HasColumnName("ACKNOWLEDGED_DATE");
        builder.Property(e => e.ResolvedDate).HasColumnName("RESOLVED_DATE");
    }
}

public class SysFailedLoginConfiguration : IEntityTypeConfiguration<SysFailedLogin>
{
    public void Configure(EntityTypeBuilder<SysFailedLogin> builder)
    {
        builder.ToTable("SYS_FAILED_LOGINS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.IpAddress).HasColumnName("IP_ADDRESS").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Username).HasColumnName("USERNAME").HasMaxLength(200);
        builder.Property(e => e.FailureReason).HasColumnName("FAILURE_REASON").HasMaxLength(500);
        builder.Property(e => e.AttemptDate).HasColumnName("ATTEMPT_DATE").IsRequired();
    }
}

public class SysSlowQueryConfiguration : IEntityTypeConfiguration<SysSlowQuery>
{
    public void Configure(EntityTypeBuilder<SysSlowQuery> builder)
    {
        builder.ToTable("SYS_SLOW_QUERIES");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CorrelationId).HasColumnName("CORRELATION_ID").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SqlStatement).HasColumnName("SQL_STATEMENT").HasColumnType("CLOB").IsRequired();
        builder.Property(e => e.ExecutionTimeMs).HasColumnName("EXECUTION_TIME_MS").IsRequired();
        builder.Property(e => e.RowsAffected).HasColumnName("ROWS_AFFECTED").IsRequired();
        builder.Property(e => e.EndpointPath).HasColumnName("ENDPOINT_PATH").HasMaxLength(500);
        builder.Property(e => e.UserId).HasColumnName("USER_ID");
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID");
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
    }
}

public class SysPerformanceMetricConfiguration : IEntityTypeConfiguration<SysPerformanceMetric>
{
    public void Configure(EntityTypeBuilder<SysPerformanceMetric> builder)
    {
        builder.ToTable("SYS_PERFORMANCE_METRICS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.EndpointPath).HasColumnName("ENDPOINT_PATH").HasMaxLength(500).IsRequired();
        builder.Property(e => e.HourTimestamp).HasColumnName("HOUR_TIMESTAMP").IsRequired();
        builder.Property(e => e.RequestCount).HasColumnName("REQUEST_COUNT").IsRequired();
        builder.Property(e => e.AvgExecutionTimeMs).HasColumnName("AVG_EXECUTION_TIME_MS").HasColumnType("NUMBER").IsRequired();
        builder.Property(e => e.MinExecutionTimeMs).HasColumnName("MIN_EXECUTION_TIME_MS").HasColumnType("NUMBER").IsRequired();
        builder.Property(e => e.MaxExecutionTimeMs).HasColumnName("MAX_EXECUTION_TIME_MS").HasColumnType("NUMBER").IsRequired();
        builder.Property(e => e.P50ExecutionTimeMs).HasColumnName("P50_EXECUTION_TIME_MS").HasColumnType("NUMBER").IsRequired();
        builder.Property(e => e.P95ExecutionTimeMs).HasColumnName("P95_EXECUTION_TIME_MS").HasColumnType("NUMBER").IsRequired();
        builder.Property(e => e.P99ExecutionTimeMs).HasColumnName("P99_EXECUTION_TIME_MS").HasColumnType("NUMBER").IsRequired();
        builder.Property(e => e.AvgDatabaseTimeMs).HasColumnName("AVG_DATABASE_TIME_MS").HasColumnType("NUMBER").IsRequired();
        builder.Property(e => e.AvgQueryCount).HasColumnName("AVG_QUERY_COUNT").HasColumnType("NUMBER").IsRequired();
        builder.Property(e => e.ErrorCount).HasColumnName("ERROR_COUNT").HasColumnType("NUMBER").IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
    }
}

public class SysReportScheduleConfiguration : IEntityTypeConfiguration<SysReportSchedule>
{
    public void Configure(EntityTypeBuilder<SysReportSchedule> builder)
    {
        builder.ToTable("SYS_REPORT_SCHEDULE", t => t.ExcludeFromMigrations());
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.ReportType).HasColumnName("REPORT_TYPE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Frequency).HasColumnName("FREQUENCY").HasMaxLength(20).IsRequired();
        builder.Property(e => e.DayOfWeek).HasColumnName("DAY_OF_WEEK");
        builder.Property(e => e.DayOfMonth).HasColumnName("DAY_OF_MONTH");
        builder.Property(e => e.TimeOfDay).HasColumnName("TIME_OF_DAY").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Recipients).HasColumnName("RECIPIENTS").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ExportFormat).HasColumnName("EXPORT_FORMAT").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Parameters).HasColumnName("PARAMETERS").HasMaxLength(2000);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnName("CREATED_BY_USER_ID").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        builder.Property(e => e.LastGeneratedAt).HasColumnName("LAST_GENERATED_AT");
        builder.Property(e => e.LastGenerationStatus).HasColumnName("LAST_GENERATION_STATUS").HasMaxLength(20);
        builder.Property(e => e.LastErrorMessage).HasColumnName("LAST_ERROR_MESSAGE").HasMaxLength(2000);
    }
}
