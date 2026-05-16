using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysSavedSearch entity.
/// Maps to SYS_SAVED_SEARCH table in Oracle database.
/// Configures saved search functionality allowing users to save frequently used search criteria.
/// </summary>
public class SavedSearchConfiguration : IEntityTypeConfiguration<SysSavedSearch>
{
    public void Configure(EntityTypeBuilder<SysSavedSearch> builder)
    {
        // Table mapping
        builder.ToTable("SYS_SAVED_SEARCH");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_SAVED_SEARCH.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.UserId)
            .HasColumnName("USER_ID")
            .IsRequired();

        builder.Property(e => e.SearchName)
            .HasColumnName("SEARCH_NAME")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.SearchCriteria)
            .HasColumnName("SEARCH_CRITERIA")
            .HasColumnType("CLOB")
            .IsRequired();

        builder.Property(e => e.UsageCount)
            .HasColumnName("USAGE_COUNT")
            .IsRequired();

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        // Optional properties
        builder.Property(e => e.SearchDescription)
            .HasColumnName("SEARCH_DESCRIPTION")
            .HasMaxLength(500);

        builder.Property(e => e.LastUsedDate)
            .HasColumnName("LAST_USED_DATE");

        // Boolean properties with Y/N conversion
        builder.Property(e => e.IsPublic)
            .HasColumnName("IS_PUBLIC")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.IsDefault)
            .HasColumnName("IS_DEFAULT")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        // Audit properties
        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // Foreign key relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .HasConstraintName("FK_SAVED_SEARCH_USER")
            .OnDelete(DeleteBehavior.Cascade);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IDX_SAVED_SEARCH_USER_ID");

        builder.HasIndex(e => new { e.UserId, e.SearchName })
            .IsUnique()
            .HasDatabaseName("UK_SAVED_SEARCH_USER_NAME");

        builder.HasIndex(e => e.IsPublic)
            .HasDatabaseName("IDX_SAVED_SEARCH_IS_PUBLIC");

        builder.HasIndex(e => new { e.UserId, e.IsDefault })
            .HasDatabaseName("IDX_SAVED_SEARCH_USER_DEFAULT");
    }
}
