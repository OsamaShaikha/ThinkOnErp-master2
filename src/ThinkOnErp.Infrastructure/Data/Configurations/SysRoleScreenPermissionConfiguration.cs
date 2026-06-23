using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysRoleScreenPermissionConfiguration : IEntityTypeConfiguration<SysRoleScreenPermission>
{
    public void Configure(EntityTypeBuilder<SysRoleScreenPermission> builder)
    {
        builder.ToTable("SYS_ROLE_SCREEN_PERMISSIONS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.RoleId).HasColumnName("ROLE_ID").IsRequired();
        builder.Property(e => e.ScreenId).HasColumnName("SCREEN_ID").IsRequired();
        builder.Property(e => e.FeatureId).HasColumnName("FEATURE_ID").IsRequired();
        builder.Property(e => e.IsGranted).HasColumnName("IS_GRANTED").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Screen)
            .WithMany()
            .HasForeignKey(e => e.ScreenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Feature)
            .WithMany()
            .HasForeignKey(e => e.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.BranchId, e.RoleId, e.ScreenId, e.FeatureId }).IsUnique().HasDatabaseName("IX_SYS_ROLE_SCREEN_PERM_UK");
    }
}
