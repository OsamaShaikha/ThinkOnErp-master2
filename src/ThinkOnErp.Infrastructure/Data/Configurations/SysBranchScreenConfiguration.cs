using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysBranchScreenConfiguration : IEntityTypeConfiguration<SysBranchScreen>
{
    public void Configure(EntityTypeBuilder<SysBranchScreen> builder)
    {
        builder.ToTable("SYS_BRANCH_SCREENS", t => t.ExcludeFromMigrations());
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.ScreenId).HasColumnName("SCREEN_ID").IsRequired();
        builder.Property(e => e.RevokedBy).HasColumnName("REVOKED_BY");
        builder.Property(e => e.RevokedDate).HasColumnName("REVOKED_DATE");
        builder.Property(e => e.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Screen)
            .WithMany()
            .HasForeignKey(e => e.ScreenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RevokedBySuperAdmin)
            .WithMany()
            .HasForeignKey(e => e.RevokedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.BranchId, e.ScreenId }).IsUnique().HasDatabaseName("IX_SYS_BRANCH_SCREENS_UK");
    }
}
