using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlCostCenterConfiguration : IEntityTypeConfiguration<GlCostCenter>
{
    public void Configure(EntityTypeBuilder<GlCostCenter> builder)
    {
        builder.ToTable("GL_COST_CENTER");

        builder.HasKey(c => c.CostCenterCode);

        builder.Property(c => c.CostCenterCode)
            .HasColumnName("COST_CENTER_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ParentCostCenterCode)
            .HasColumnName("PARENT_COST_CENTER_CODE")
            .HasMaxLength(50);

        builder.Property(c => c.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.CostCenterLevel)
            .HasColumnName("COST_CENTER_LEVEL")
            .HasDefaultValue(1);

        builder.Property(c => c.CostCenterType)
            .HasColumnName("COST_CENTER_TYPE")
            .HasMaxLength(20)
            .HasDefaultValue("DETAIL");

        builder.Property(c => c.IsPostable)
            .HasColumnName("IS_POSTABLE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        builder.Property(c => c.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        builder.Property(c => c.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(c => c.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(c => c.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasOne(c => c.ParentCostCenter)
            .WithMany(c => c.InverseParentCostCenter)
            .HasForeignKey(c => c.ParentCostCenterCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
