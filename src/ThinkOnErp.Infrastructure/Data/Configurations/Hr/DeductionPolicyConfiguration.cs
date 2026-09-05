using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class DeductionPolicyConfiguration : IEntityTypeConfiguration<DeductionPolicy>
{
    public void Configure(EntityTypeBuilder<DeductionPolicy> builder)
    {
        builder.ToTable("HR_DEDUCTION_POLICY");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(p => p.Code).HasColumnName("CODE").HasMaxLength(50).IsRequired();
        builder.Property(p => p.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(p => p.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(p => p.MaxDeductionPercentage).HasColumnName("MAX_DEDUCTION_PERCENTAGE").HasColumnType("NUMBER(5,2)").HasDefaultValue(50.0m);
        builder.Property(p => p.MinimumNetPayGuarantee).HasColumnName("MIN_NET_PAY_GUARANTEE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0.0m);
        builder.Property(p => p.AllowNegativeNetPay).HasColumnName("ALLOW_NEGATIVE_NET_PAY").HasDefaultValue(false);
        builder.Property(p => p.AutoCapAndCarryForward).HasColumnName("AUTO_CAP_AND_CARRY_FORWARD").HasDefaultValue(true);
        builder.Property(p => p.IsDefault).HasColumnName("IS_DEFAULT").HasDefaultValue(true);
        builder.Property(p => p.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(p => p.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
    }
}
