using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosOrderTaxConfiguration : IEntityTypeConfiguration<PosOrderTax>
{
    public void Configure(EntityTypeBuilder<PosOrderTax> builder)
    {
        builder.ToTable("POS_ORDER_TAX");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.OrderId).HasColumnName("ORDER_ID").IsRequired();
        builder.Property(t => t.OrderLineId).HasColumnName("ORDER_LINE_ID");
        builder.Property(t => t.TaxRateId).HasColumnName("TAX_RATE_ID").IsRequired();
        builder.Property(t => t.TaxRateCode).HasColumnName("TAX_RATE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(t => t.TaxPercent).HasColumnName("TAX_PERCENT").HasColumnType("NUMBER(8,4)").IsRequired();
        builder.Property(t => t.TaxableAmount).HasColumnName("TAXABLE_AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(t => t.TaxAmount).HasColumnName("TAX_AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(t => t.IsInclusive).HasColumnName("IS_INCLUSIVE").HasColumnType("NUMBER(1)").HasDefaultValue(false);

        builder.HasOne(t => t.Order).WithMany(o => o.Taxes).HasForeignKey(t => t.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
