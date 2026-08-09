using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public class GlAccountBranchConfiguration : IEntityTypeConfiguration<GlAccountBranch>
{
    public void Configure(EntityTypeBuilder<GlAccountBranch> builder)
    {
        builder.ToTable("GL_ACCOUNT_BRANCH", tableBuilder =>
        {
            tableBuilder.ExcludeFromMigrations();
            tableBuilder.HasCheckConstraint(
                "CK_GL_ACC_BRANCH_ACTIVE",
                "\"IS_ACTIVE\" IN (0, 1)");
        });

        builder.HasKey(link => new { link.GlAccountId, link.BranchId });
        builder.Property(link => link.GlAccountId)
            .HasColumnName("GL_ACCOUNT_ID")
            .HasColumnType("NUMBER(19)")
            .IsRequired();
        builder.Property(link => link.BranchId)
            .HasColumnName("BRANCH_ID")
            .HasColumnType("NUMBER(19)")
            .IsRequired();
        builder.Property(link => link.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .IsRequired();

        builder.HasOne(link => link.GlAccount)
            .WithMany(account => account.BranchLinks)
            .HasForeignKey(link => link.GlAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(link => link.Branch)
            .WithMany()
            .HasForeignKey(link => link.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => link.BranchId)
            .HasDatabaseName("IX_GL_ACC_BRANCH_BRANCH");
    }
}
