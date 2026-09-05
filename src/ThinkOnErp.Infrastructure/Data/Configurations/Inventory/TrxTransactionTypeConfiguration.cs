using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class TrxTransactionTypeConfiguration : IEntityTypeConfiguration<TrxTransactionType>
{
    public void Configure(EntityTypeBuilder<TrxTransactionType> builder)
    {
        builder.ToTable("TRX_TRANSACTION_TYPE");
        builder.HasKey(t => t.TrxCode);

        builder.Property(t => t.TrxCode).HasColumnName("TRX_CODE").HasColumnType("NUMBER(10)").ValueGeneratedNever();
        builder.Property(t => t.DocTypeCode).HasColumnName("DOC_TYPE_CODE").HasColumnType("NUMBER(10)").IsRequired();
        builder.Property(t => t.TrxKey).HasColumnName("TRX_KEY").HasMaxLength(50).IsRequired();
        builder.Property(t => t.TrxNameLocal).HasColumnName("TRX_NAME_LOCAL").HasMaxLength(100).IsRequired();
        builder.Property(t => t.TrxNameEn).HasColumnName("TRX_NAME_EN").HasMaxLength(100).IsRequired();
        builder.Property(t => t.AffectsStock).HasColumnName("AFFECTS_STOCK").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(t => t.StockDirection).HasColumnName("STOCK_DIRECTION").HasColumnType("NUMBER(10)").HasDefaultValue(0);
        builder.Property(t => t.RequiresWarehouse).HasColumnName("REQUIRES_WAREHOUSE").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(t => t.AffectsGl).HasColumnName("AFFECTS_GL").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(t => t.AffectsPartyBalance).HasColumnName("AFFECTS_PARTY_BALANCE").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(t => t.PostingRuleCode).HasColumnName("POSTING_RULE_CODE").HasMaxLength(50);
        builder.Property(t => t.RequiresParty).HasColumnName("REQUIRES_PARTY").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(t => t.RequiresPrice).HasColumnName("REQUIRES_PRICE").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(t => t.RequiresCost).HasColumnName("REQUIRES_COST").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(t => t.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(t => t.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(t => t.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(t => t.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(t => t.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(t => t.TrxKey).IsUnique();
    }
}
