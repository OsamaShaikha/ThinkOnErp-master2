using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysFeatureConfiguration : IEntityTypeConfiguration<SysFeature>
{
    public void Configure(EntityTypeBuilder<SysFeature> builder)
    {
        builder.ToTable("SYS_FEATURE");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.FeatureCode).HasColumnName("FEATURE_CODE").HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.FeatureCode).IsUnique();
        builder.Property(e => e.FeatureName).HasColumnName("FEATURE_NAME").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FeatureNameE).HasColumnName("FEATURE_NAME_E").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
        builder.Property(e => e.DescriptionE).HasColumnName("DESCRIPTION_E").HasMaxLength(500);
        builder.Property(e => e.Icon).HasColumnName("ICON").HasMaxLength(100);
        builder.Property(e => e.DisplayOrder).HasColumnName("DISPLAY_ORDER");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasMaxLength(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}
