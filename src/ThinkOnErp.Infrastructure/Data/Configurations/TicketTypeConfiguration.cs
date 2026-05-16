using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysTicketType entity.
/// Maps to SYS_TICKET_TYPE table in Oracle database.
/// </summary>
public class TicketTypeConfiguration : IEntityTypeConfiguration<SysTicketType>
{
    public void Configure(EntityTypeBuilder<SysTicketType> builder)
    {
        // Table mapping
        builder.ToTable("SYS_TICKET_TYPE");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_TICKET_TYPE.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.TypeNameAr)
            .HasColumnName("TYPE_NAME_AR")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.TypeNameEn)
            .HasColumnName("TYPE_NAME_EN")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.DefaultPriorityId)
            .HasColumnName("DEFAULT_PRIORITY_ID")
            .IsRequired();

        builder.Property(e => e.SlaTargetHours)
            .HasColumnName("SLA_TARGET_HOURS")
            .HasPrecision(10, 2)
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

        // Foreign key relationships
        builder.HasOne(e => e.DefaultPriority)
            .WithMany(p => p.TicketTypesWithDefaultPriority)
            .HasForeignKey(e => e.DefaultPriorityId)
            .HasConstraintName("FK_TICKET_TYPE_DEFAULT_PRIORITY")
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-many relationships
        builder.HasMany(e => e.Tickets)
            .WithOne(t => t.TicketType)
            .HasForeignKey(t => t.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.TypeNameAr)
            .HasDatabaseName("IDX_TICKET_TYPE_NAME_AR");

        builder.HasIndex(e => e.TypeNameEn)
            .HasDatabaseName("IDX_TICKET_TYPE_NAME_EN");
    }
}
