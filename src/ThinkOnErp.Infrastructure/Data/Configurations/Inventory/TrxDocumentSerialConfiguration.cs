using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class TrxDocumentSerialConfiguration : IEntityTypeConfiguration<TrxDocumentSerial>
{
    public void Configure(EntityTypeBuilder<TrxDocumentSerial> builder)
    {
        builder.ToTable("TRX_DOCUMENT_SERIAL");

        builder.HasKey(s => new { s.BranchId, s.DocYear, s.DocMonth, s.DocType })
            .HasName("PK_TRX_DOC_SERIAL");

        builder.Property(s => s.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(s => s.DocYear)
            .HasColumnName("DOC_YEAR")
            .IsRequired();

        builder.Property(s => s.DocMonth)
            .HasColumnName("DOC_MONTH")
            .IsRequired();

        builder.Property(s => s.DocType)
            .HasColumnName("DOC_TYPE")
            .IsRequired();

        builder.Property(s => s.LastSerialNo)
            .HasColumnName("LAST_SERIAL_NO")
            .HasDefaultValue(0L);

        builder.Property(s => s.UpdateDate)
            .HasColumnName("UPDATE_DATE");
    }
}
