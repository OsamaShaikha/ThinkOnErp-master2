using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysTicketStatus entity.
/// Maps to SYS_TICKET_STATUS table in Oracle database.
/// </summary>
public class TicketStatusConfiguration : IEntityTypeConfiguration<SysTicketStatus>
{
    public void Configure(EntityTypeBuilder<SysTicketStatus> builder)
    {
        // Table mapping
        builder.ToTable("SYS_TICKET_STATUS");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_TICKET_STATUS.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.StatusNameAr)
            .HasColumnName("STATUS_NAME_AR")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.StatusNameEn)
            .HasColumnName("STATUS_NAME_EN")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.StatusCode)
            .HasColumnName("STATUS_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.DisplayOrder)
            .HasColumnName("DISPLAY_ORDER")
            .IsRequired();

        builder.Property(e => e.IsFinalStatus)
            .HasColumnName("IS_FINAL_STATUS")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

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

        // One-to-many relationships
        builder.HasMany(e => e.Tickets)
            .WithOne(t => t.TicketStatus)
            .HasForeignKey(t => t.TicketStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.StatusCode)
            .IsUnique()
            .HasDatabaseName("UK_TICKET_STATUS_CODE");

        builder.HasIndex(e => e.DisplayOrder)
            .HasDatabaseName("IDX_TICKET_STATUS_DISPLAY_ORDER");

        // Ignore computed properties
        builder.Ignore(e => e.AllowsModification);
        builder.Ignore(e => e.IsResolvedStatus);
    }
}
