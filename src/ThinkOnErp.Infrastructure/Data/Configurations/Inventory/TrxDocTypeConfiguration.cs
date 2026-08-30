using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class TrxDocTypeConfiguration : IEntityTypeConfiguration<TrxDocType>
{
    public void Configure(EntityTypeBuilder<TrxDocType> builder)
    {
        builder.ToTable("TRX_DOC_TYPE");
        builder.HasKey(t => t.TypeCode);

        builder.Property(t => t.TypeCode).HasColumnName("TYPE_CODE").ValueGeneratedNever();
        builder.Property(t => t.TypeKey).HasColumnName("TYPE_KEY").HasMaxLength(50).IsRequired();
        builder.Property(t => t.TypeNameAr).HasColumnName("TYPE_NAME_AR").HasMaxLength(100).IsRequired();
        builder.Property(t => t.TypeNameEn).HasColumnName("TYPE_NAME_EN").HasMaxLength(100).IsRequired();
        builder.Property(t => t.ModuleCode).HasColumnName("MODULE_CODE").HasMaxLength(30).IsRequired();
        builder.Property(t => t.DocPrefix).HasColumnName("DOC_PREFIX").HasMaxLength(10).IsRequired();
        builder.Property(t => t.ResetPolicy).HasColumnName("RESET_POLICY").HasMaxLength(20).HasDefaultValue("YEARLY");
        builder.Property(t => t.IsSystemReserved).HasColumnName("IS_SYSTEM_RESERVED").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(t => t.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);
        builder.Property(t => t.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(t => t.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(t => t.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(t => t.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(t => t.TypeKey).IsUnique();
        builder.HasMany(t => t.TransactionTypes).WithOne(tt => tt.DocType).HasForeignKey(tt => tt.DocTypeCode);
    }
}
