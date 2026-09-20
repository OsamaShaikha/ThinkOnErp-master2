using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvItemAttributeConfiguration : IEntityTypeConfiguration<InvItemAttribute>
{
    public void Configure(EntityTypeBuilder<InvItemAttribute> builder)
    {
        builder.ToTable("INV_ITEM_ATTRIBUTE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(e => e.AttributeCode)
            .HasColumnName("ATTRIBUTE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.NameLocal)
            .HasColumnName("NAME_LOCAL")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(100);

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .HasColumnType("TIMESTAMP");

        builder.HasMany(e => e.Values)
            .WithOne(v => v.Attribute)
            .HasForeignKey(v => v.AttributeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
