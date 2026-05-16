using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysTicketPriority entity.
/// Maps to SYS_TICKET_PRIORITY table in Oracle database.
/// </summary>
public class TicketPriorityConfiguration : IEntityTypeConfiguration<SysTicketPriority>
{
    public void Configure(EntityTypeBuilder<SysTicketPriority> builder)
    {
        // Table mapping
        builder.ToTable("SYS_TICKET_PRIORITY");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_TICKET_PRIORITY.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.PriorityNameAr)
            .HasColumnName("PRIORITY_NAME_AR")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.PriorityNameEn)
            .HasColumnName("PRIORITY_NAME_EN")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.PriorityLevel)
            .HasColumnName("PRIORITY_LEVEL")
            .IsRequired();

        builder.Property(e => e.SlaTargetHours)
            .HasColumnName("SLA_TARGET_HOURS")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(e => e.EscalationThresholdHours)
            .HasColumnName("ESCALATION_THRESHOLD_HOURS")
            .HasPrecision(10, 2)
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
            .WithOne(t => t.TicketPriority)
            .HasForeignKey(t => t.TicketPriorityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.TicketTypesWithDefaultPriority)
            .WithOne(tt => tt.DefaultPriority)
            .HasForeignKey(tt => tt.DefaultPriorityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.PriorityLevel)
            .HasDatabaseName("IDX_TICKET_PRIORITY_LEVEL");

        // Ignore computed properties
        builder.Ignore(e => e.IsCritical);
        builder.Ignore(e => e.IsHigh);
        builder.Ignore(e => e.RequiresImmediateAttention);
    }
}
