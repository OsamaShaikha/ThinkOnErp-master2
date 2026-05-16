using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysTicketConfig entity.
/// Maps to SYS_TICKET_CONFIG table in Oracle database.
/// </summary>
public class TicketConfigConfiguration : IEntityTypeConfiguration<SysTicketConfig>
{
    public void Configure(EntityTypeBuilder<SysTicketConfig> builder)
    {
        // Table mapping
        builder.ToTable("SYS_TICKET_CONFIG");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_TICKET_CONFIG.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.ConfigKey)
            .HasColumnName("CONFIG_KEY")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ConfigValue)
            .HasColumnName("CONFIG_VALUE")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.ConfigType)
            .HasColumnName("CONFIG_TYPE")
            .HasMaxLength(50)
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

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.ConfigKey)
            .IsUnique()
            .HasDatabaseName("UK_TICKET_CONFIG_KEY");

        builder.HasIndex(e => e.ConfigType)
            .HasDatabaseName("IDX_TICKET_CONFIG_TYPE");
    }
}
