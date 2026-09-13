using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosPrintTemplateConfiguration : IEntityTypeConfiguration<PosPrintTemplate>
{
    public void Configure(EntityTypeBuilder<PosPrintTemplate> builder)
    {
        builder.ToTable("POS_PRINT_TEMPLATE");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(p => p.TemplateCode).HasColumnName("TEMPLATE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(p => p.TemplateName).HasColumnName("TEMPLATE_NAME").HasMaxLength(100).IsRequired();
        builder.Property(p => p.TemplateType).HasColumnName("TEMPLATE_TYPE").HasMaxLength(30).IsRequired();
        builder.Property(p => p.RawEscPosPattern).HasColumnName("RAW_ESC_POS_PATTERN").HasColumnType("CLOB").IsRequired();
        builder.Property(p => p.IsDefault).HasColumnName("IS_DEFAULT").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.HasIndex(p => new { p.BranchId, p.TemplateCode }).IsUnique();
        builder.HasOne(p => p.Branch).WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PosPrinterRoutingConfiguration : IEntityTypeConfiguration<PosPrinterRouting>
{
    public void Configure(EntityTypeBuilder<PosPrinterRouting> builder)
    {
        builder.ToTable("POS_PRINTER_ROUTING");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(r => r.StationName).HasColumnName("STATION_NAME").HasMaxLength(50).IsRequired();
        builder.Property(r => r.PrinterNameOrIp).HasColumnName("PRINTER_NAME_OR_IP").HasMaxLength(150).IsRequired();
        builder.Property(r => r.ItemGroupId).HasColumnName("ITEM_GROUP_ID");
        builder.Property(r => r.Copies).HasColumnName("COPIES").HasDefaultValue(1);
        builder.Property(r => r.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.HasOne(r => r.Branch).WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ItemGroup).WithMany().HasForeignKey(r => r.ItemGroupId).OnDelete(DeleteBehavior.SetNull);
    }
}
