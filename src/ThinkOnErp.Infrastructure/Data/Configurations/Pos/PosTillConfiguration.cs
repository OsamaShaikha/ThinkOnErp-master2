using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosTillConfiguration : IEntityTypeConfiguration<PosTill>
{
    public void Configure(EntityTypeBuilder<PosTill> builder)
    {
        builder.ToTable("POS_TILL");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(t => t.TillCode).HasColumnName("TILL_CODE").HasMaxLength(30).IsRequired();
        builder.Property(t => t.TillName).HasColumnName("TILL_NAME").HasMaxLength(100).IsRequired();
        builder.Property(t => t.MachineIdentifier).HasColumnName("MACHINE_IDENTIFIER").HasMaxLength(100);
        builder.Property(t => t.IpAddress).HasColumnName("IP_ADDRESS").HasMaxLength(50);
        builder.Property(t => t.DefaultFloatAmount).HasColumnName("DEFAULT_FLOAT_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(t => t.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.Property(t => t.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(t => t.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(t => t.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(t => t.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(t => new { t.BranchId, t.TillCode }).IsUnique();
        builder.HasOne(t => t.Branch).WithMany().HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
