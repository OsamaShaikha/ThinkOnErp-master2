using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysSystemScreenFeatureConfiguration : IEntityTypeConfiguration<SysSystemScreenFeature>
{
    public void Configure(EntityTypeBuilder<SysSystemScreenFeature> builder)
    {
        builder.ToTable("SYS_SYSTEM_SCREEN_FEATURE");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        builder.Property(e => e.SystemId).HasColumnName("SYSTEM_ID").IsRequired();
        builder.Property(e => e.ScreenId).HasColumnName("SCREEN_ID");
        builder.Property(e => e.FeatureId).HasColumnName("FEATURE_ID").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)");
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.System)
            .WithMany(s => s.SystemScreenFeatures)
            .HasForeignKey(e => e.SystemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Screen)
            .WithMany(s => s.SystemScreenFeatures)
            .HasForeignKey(e => e.ScreenId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Feature)
            .WithMany(f => f.SystemScreenFeatures)
            .HasForeignKey(e => e.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SystemId, e.ScreenId, e.FeatureId });
        builder.HasIndex(e => e.ScreenId);
        builder.HasIndex(e => e.FeatureId);
    }
}
