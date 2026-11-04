using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysSystemScreenConfiguration : IEntityTypeConfiguration<SysSystemScreen>
{
    public void Configure(EntityTypeBuilder<SysSystemScreen> builder)
    {
        builder.ToTable("SYS_SYSTEM_SCREEN");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        builder.Property(e => e.SystemId).HasColumnName("SYSTEM_ID").IsRequired();
        builder.Property(e => e.ScreenId).HasColumnName("SCREEN_ID").IsRequired();
        builder.Property(e => e.ParentScreenId).HasColumnName("PARENT_SCREEN_ID");
        builder.Property(e => e.DisplayOrder).HasColumnName("DISPLAY_ORDER").HasDefaultValue(0);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)");
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.System)
            .WithMany(s => s.SystemScreens)
            .HasForeignKey(e => e.SystemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Screen)
            .WithMany(s => s.SystemScreens)
            .HasForeignKey(e => e.ScreenId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ParentScreen)
            .WithMany()
            .HasForeignKey(e => e.ParentScreenId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.SystemId, e.ScreenId }).IsUnique().HasDatabaseName("IX_SYS_SYSTEM_SCREEN_UK");
        builder.HasIndex(e => e.ScreenId).HasDatabaseName("IDX_SYS_SYSTEM_SCREEN_SCREEN");
    }
}
