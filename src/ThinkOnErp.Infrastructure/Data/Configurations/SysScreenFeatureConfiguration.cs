using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysScreenFeatureConfiguration : IEntityTypeConfiguration<SysScreenFeature>
{
    public void Configure(EntityTypeBuilder<SysScreenFeature> builder)
    {
        builder.ToTable("SYS_SCREEN_FEATURE");
        builder.HasKey(e => new { e.ScreenId, e.FeatureId });
        builder.Property(e => e.ScreenId).HasColumnName("SCREEN_ID").IsRequired();
        builder.Property(e => e.FeatureId).HasColumnName("FEATURE_ID").IsRequired();

        builder.HasOne(e => e.Screen)
            .WithMany(e => e.ScreenFeatures)
            .HasForeignKey(e => e.ScreenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Feature)
            .WithMany(e => e.ScreenFeatures)
            .HasForeignKey(e => e.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
