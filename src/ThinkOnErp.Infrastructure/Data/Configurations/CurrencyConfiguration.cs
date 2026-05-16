using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysCurrency entity.
/// Maps to SYS_CURRENCY table in Oracle database.
/// </summary>
public class CurrencyConfiguration : IEntityTypeConfiguration<SysCurrency>
{
    public void Configure(EntityTypeBuilder<SysCurrency> builder)
    {
        // Table mapping
        builder.ToTable("SYS_CURRENCY");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_CURRENCY.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.RowDesc)
            .HasColumnName("ROW_DESC")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.RowDescE)
            .HasColumnName("ROW_DESC_E")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ShortDesc)
            .HasColumnName("SHORT_DESC")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ShortDescE)
            .HasColumnName("SHORT_DESC_E")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.SingulerDesc)
            .HasColumnName("SINGULER_DESC")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.SingulerDescE)
            .HasColumnName("SINGULER_DESC_E")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.DualDesc)
            .HasColumnName("DUAL_DESC")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.DualDescE)
            .HasColumnName("DUAL_DESC_E")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.SumDesc)
            .HasColumnName("SUM_DESC")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.SumDescE)
            .HasColumnName("SUM_DESC_E")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.FracDesc)
            .HasColumnName("FRAC_DESC")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.FracDescE)
            .HasColumnName("FRAC_DESC_E")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        // Optional properties
        builder.Property(e => e.CurrRate)
            .HasColumnName("CURR_RATE")
            .HasPrecision(18, 6);

        builder.Property(e => e.CurrRateDate)
            .HasColumnName("CURR_RATE_DATE");

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");
    }
}
