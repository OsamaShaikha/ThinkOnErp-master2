using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysSearchAnalytics entity.
/// Maps to SYS_SEARCH_ANALYTICS table in Oracle database.
/// Configures search analytics and query logging for performance optimization and analytics.
/// </summary>
public class SearchAnalyticsConfiguration : IEntityTypeConfiguration<SysSearchAnalytics>
{
    public void Configure(EntityTypeBuilder<SysSearchAnalytics> builder)
    {
        // Table mapping
        builder.ToTable("SYS_SEARCH_ANALYTICS");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_SEARCH_ANALYTICS.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.UserId)
            .HasColumnName("USER_ID")
            .IsRequired();

        builder.Property(e => e.ResultCount)
            .HasColumnName("RESULT_COUNT")
            .IsRequired();

        builder.Property(e => e.ExecutionTimeMs)
            .HasColumnName("EXECUTION_TIME_MS")
            .IsRequired();

        builder.Property(e => e.SearchDate)
            .HasColumnName("SEARCH_DATE")
            .IsRequired();

        // Optional properties
        builder.Property(e => e.SearchTerm)
            .HasColumnName("SEARCH_TERM")
            .HasMaxLength(500);

        builder.Property(e => e.SearchCriteria)
            .HasColumnName("SEARCH_CRITERIA")
            .HasColumnType("CLOB");

        builder.Property(e => e.FilterLogic)
            .HasColumnName("FILTER_LOGIC")
            .HasMaxLength(10);

        builder.Property(e => e.CompanyId)
            .HasColumnName("COMPANY_ID");

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID");

        // Foreign key relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .HasConstraintName("FK_SEARCH_ANALYTICS_USER")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .HasConstraintName("FK_SEARCH_ANALYTICS_COMPANY")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .HasConstraintName("FK_SEARCH_ANALYTICS_BRANCH")
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for performance and analytics queries
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IDX_SEARCH_ANALYTICS_USER_ID");

        builder.HasIndex(e => e.SearchDate)
            .HasDatabaseName("IDX_SEARCH_ANALYTICS_DATE");

        builder.HasIndex(e => e.CompanyId)
            .HasDatabaseName("IDX_SEARCH_ANALYTICS_COMPANY");

        builder.HasIndex(e => e.BranchId)
            .HasDatabaseName("IDX_SEARCH_ANALYTICS_BRANCH");

        builder.HasIndex(e => e.ExecutionTimeMs)
            .HasDatabaseName("IDX_SEARCH_ANALYTICS_EXEC_TIME");

        builder.HasIndex(e => e.SearchTerm)
            .HasDatabaseName("IDX_SEARCH_ANALYTICS_TERM");

        builder.HasIndex(e => new { e.UserId, e.SearchDate })
            .HasDatabaseName("IDX_SEARCH_ANALYTICS_USER_DATE");
    }
}
