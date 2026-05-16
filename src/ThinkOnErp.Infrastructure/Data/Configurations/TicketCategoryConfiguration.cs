using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysTicketCategory entity.
/// Maps to SYS_TICKET_CATEGORY table in Oracle database.
/// </summary>
public class TicketCategoryConfiguration : IEntityTypeConfiguration<SysTicketCategory>
{
    public void Configure(EntityTypeBuilder<SysTicketCategory> builder)
    {
        // Table mapping
        builder.ToTable("SYS_TICKET_CATEGORY");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_TICKET_CATEGORY.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.CategoryNameAr)
            .HasColumnName("CATEGORY_NAME_AR")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CategoryNameEn)
            .HasColumnName("CATEGORY_NAME_EN")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.DisplayOrder)
            .HasColumnName("DISPLAY_ORDER")
            .IsRequired();

        // Optional properties
        builder.Property(e => e.DescriptionAr)
            .HasColumnName("DESCRIPTION_AR")
            .HasMaxLength(500);

        builder.Property(e => e.DescriptionEn)
            .HasColumnName("DESCRIPTION_EN")
            .HasMaxLength(500);

        // Audit properties
        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // One-to-many relationships
        builder.HasMany(e => e.Tickets)
            .WithOne(t => t.TicketCategory)
            .HasForeignKey(t => t.TicketCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.CategoryNameAr)
            .HasDatabaseName("IDX_TICKET_CATEGORY_NAME_AR");

        builder.HasIndex(e => e.CategoryNameEn)
            .HasDatabaseName("IDX_TICKET_CATEGORY_NAME_EN");

        builder.HasIndex(e => e.DisplayOrder)
            .HasDatabaseName("IDX_TICKET_CATEGORY_DISPLAY_ORDER");
    }
}
